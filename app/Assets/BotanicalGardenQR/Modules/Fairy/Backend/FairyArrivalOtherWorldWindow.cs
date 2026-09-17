using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>
    /// Adapts The World Beyond's virtual-room reveal to a local automatic window.
    /// The garden remains stencil-clipped; the single skinned guide transitions
    /// continuously through the same fixed world-space boundary.
    /// </summary>
    public sealed class FairyArrivalOtherWorldWindow : MonoBehaviour
    {
        const string StencilComparisonProperty = "_PortalStencilComp";
        const string StencilReferenceProperty = "_PortalStencilRef";
        const string StencilReadMaskProperty = "_PortalStencilReadMask";
        const string IntensityProperty = "_Intensity";

        readonly List<CharacterRendererState> _characterRenderers = new();
        readonly List<Material> _ownedCharacterMaterials = new();

        [SerializeField] AudioSource _ambience;
        bool _audioDucked;
        internal void SetAudioDuck(bool ducked) => _audioDucked = ducked;
        [SerializeField] GameObject _pollenPrefab;
        [SerializeField] Material _ritualSigilMaterial;
        internal Material RitualSigilMaterial => _ritualSigilMaterial;
        ParticleSystem _pollen;
        Material _pollenMaterial;
        Transform _target;
        Transform _environment;
        Vector3 _environmentPosition;
        Vector3 _windowPosition;
        Quaternion _windowRotation;
        FairyArrivalFlashlightAperture _aperture;
        MeshRenderer _rim;
        Material _rimMaterial;
        AudioLowPassFilter _windowFilter;
        float _ritualTime;
        Transform _portalSlice;
        Transform _portalStencil;
        GameObject _passthroughShell;
        Material _portalStencilMaterial;
        bool _characterVisibleInWindow;
        bool _characterRevealed;
        bool _initialized;
        float _windowStrength;
        float _anticipation;

        internal void Initialize(Transform target, FairyArrivalFlashlightAperture aperture)
        {
            if (_initialized)
                throw new InvalidOperationException("Fairy other-world window is already initialized.");
            _target = target != null ? target : throw new ArgumentNullException(nameof(target));
            if (aperture == null) throw new ArgumentNullException(nameof(aperture));

            _aperture = aperture;
            _windowPosition = transform.position;
            _windowRotation = transform.rotation;
            _portalSlice = aperture.Plane;
            _portalStencil = transform.Find("OtherWorldPortalStencil");
            var environment = transform.Find("OppyOtherWorldEnvironment");
            var shell = transform.Find("FairyPassthroughShell");
            if (_portalSlice == null || _portalStencil == null || environment == null || shell == null)
                throw new InvalidOperationException(
                    "Fairy arrival mask requires the official outer slice, other-world environment, portal stencil, and PassthroughShell.");

            var stencilRenderer = _portalStencil.GetComponent<MeshRenderer>();
            _portalStencil.GetComponent<MeshFilter>().sharedMesh = _portalSlice.GetComponent<MeshFilter>().sharedMesh;
            if (stencilRenderer == null || stencilRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Fairy other-world portal stencil has no material.");
            _portalStencilMaterial = new Material(stencilRenderer.sharedMaterial)
            {
                name = $"{stencilRenderer.sharedMaterial.name} (Arrival Instance)",
                hideFlags = HideFlags.DontSave
            };
            stencilRenderer.sharedMaterial = _portalStencilMaterial;
            var rimObject = new GameObject("SpatialBoundaryEdge", typeof(MeshFilter), typeof(MeshRenderer));
            rimObject.transform.SetParent(transform, false);
            rimObject.GetComponent<MeshFilter>().sharedMesh = _portalStencil.GetComponent<MeshFilter>().sharedMesh;
            _rim = rimObject.GetComponent<MeshRenderer>();
            _rimMaterial = new Material(_portalStencilMaterial) { name = "Spatial boundary edge", renderQueue = 3000 };
            _rimMaterial.SetFloat("_Rim", 1);
            _rimMaterial.SetFloat("_ColorMask", 15);
            _rimMaterial.SetFloat("_StencilWriteMask", 0);
            _rimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            _rim.sharedMaterial = _rimMaterial;

            _passthroughShell = shell.gameObject;
            _passthroughShell.SetActive(false);
            environment.SetPositionAndRotation(_target.position, Quaternion.identity);
            _environment = environment;
            _environmentPosition = environment.position;
            ValidateAndLimitEnvironment(environment);
            CreateLocalPollen(environment);
            CaptureCharacterRenderers();
            _initialized = true;
            if (_ambience != null)
            {
                _windowFilter = _ambience.gameObject.AddComponent<AudioLowPassFilter>();
                _windowFilter.cutoffFrequency = 650f;
                _ambience.spatialize = true;
                _ambience.spatialBlend = .65f;
                _ambience.minDistance = 1.5f;
                _ambience.dopplerLevel = 0f;
                if (Application.isPlaying)
                {
                    _ambience.Play();
                    StartCoroutine(FairyAudioPlaybackProbe.Observe(_ambience, _ambience.clip, null, "window"));
                }
            }
            SetStrength(aperture.CurrentStrength);
            AlignStencilToOuterSlice();
        }

        void CreateLocalPollen(Transform environment)
        {
            if (_pollenPrefab == null) return;
            var instance = Instantiate(_pollenPrefab, environment, false);
            instance.name = "WindowPollen";
            instance.transform.localPosition = new Vector3(0, .38f, .15f);
            _pollen = instance.GetComponent<ParticleSystem>();
            _pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _pollen.main;
            main.maxParticles = 48; main.loop = true; main.playOnAwake = false;
            main.startLifetime = 4f; main.startSpeed = .025f;
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .025f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = _pollen.emission; emission.rateOverTime = 8f;
            var shape = _pollen.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1f, .7f, .6f);
            var renderer = instance.GetComponent<ParticleSystemRenderer>();
            _pollenMaterial = new Material(Shader.Find("TheWorldBeyond/OppyParticlesShader")) { name = "Window pollen instance" };
            _pollenMaterial.mainTexture = renderer.sharedMaterial.mainTexture;
            _pollenMaterial.SetFloat(StencilComparisonProperty, (float)CompareFunction.Equal);
            _pollenMaterial.SetFloat(StencilReferenceProperty, 1f);
            _pollenMaterial.SetFloat(StencilReadMaskProperty, 255f);
            renderer.sharedMaterial = _pollenMaterial;
            if (Application.isPlaying) _pollen.Play(true); else _pollen.Simulate(2f, true, true);
        }

        internal void SetStrength(float strength)
        {
            if (_portalStencilMaterial == null)
                throw new InvalidOperationException("Fairy other-world portal stencil is not initialized.");
            _portalStencilMaterial.SetFloat(IntensityProperty, Mathf.Clamp01(strength));
            if (_rimMaterial != null) _rimMaterial.SetFloat(IntensityProperty, Mathf.Clamp01(strength));
            _windowStrength = Mathf.Clamp01(strength);
            UpdateAmbienceVolume();
            if (_pollenMaterial != null) _pollenMaterial.SetColor("_Color", new Color(.8f, 1f, .58f, .65f * Mathf.Clamp01(strength)));
        }

        internal void SetRitualTime(float seconds)
        {
            if (_portalStencilMaterial == null) return;
            _ritualTime = seconds;
            foreach (var material in new[] { _portalStencilMaterial, _rimMaterial })
            {
                material.SetFloat("_RitualTime", seconds);
                material.SetFloat("_RiftAmount", _aperture.Opening);
            }
        }

        internal void SetAnticipation(float progress)
        {
            _anticipation = Mathf.Clamp01(progress);
            if (_windowFilter != null) _windowFilter.cutoffFrequency = Mathf.Lerp(650f, 14000f, _anticipation);
            UpdateAmbienceVolume();
        }

        void UpdateAmbienceVolume()
        {
            if (_ambience != null)
                _ambience.volume = _windowStrength * Mathf.Lerp(.08f, .22f, _anticipation) * (_audioDucked ? .2f : 1f);
        }

        internal void ShowCharacterInWindow()
        {
            EnsureInitialized();
            if (_characterVisibleInWindow || _characterRevealed) return;
            _characterVisibleInWindow = true;
            foreach (var state in _characterRenderers)
            {
                if (state.Renderer != null && state.SupportsPortalStencil)
                    state.Renderer.enabled = state.WasEnabled;
            }
        }

        internal void RevealCharacter()
        {
            EnsureInitialized();
            RestoreCharacterRenderers();
            _characterRevealed = true;
        }

        internal void BeginCrossing()
        {
            foreach (var material in _ownedCharacterMaterials)
            {
                if (material == null || !material.HasProperty("_ArrivalCrossing")) continue;
                material.SetFloat(StencilComparisonProperty, (float)CompareFunction.Always);
                material.SetFloat("_ArrivalCrossing", 1);
                material.SetTexture("_BoundaryAtlas", _portalStencilMaterial.GetTexture("_BoundaryAtlas"));
                material.SetTexture("_FlowAtlas", _portalStencilMaterial.GetTexture("_FlowAtlas"));
            }
        }

        internal bool HasBodyClearedBoundary()
        {
            SynchronizeSpatialPose();
            foreach (var state in _characterRenderers)
            {
                if (state.Renderer == null || !state.WasEnabled) continue;
                if (!(state.Renderer is MeshRenderer) && !(state.Renderer is SkinnedMeshRenderer)) continue;
                var bounds = state.Renderer.bounds;
                var normal = _portalSlice.forward;
                var radius = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)));
                if (Vector3.Dot(bounds.center - _portalSlice.position, normal) + radius > -.025f) return false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (!_initialized) return;
            SynchronizeSpatialPose();
            // Imported animation can write renderer visibility. The window owns the unrevealed phase.
            if (!_characterVisibleInWindow && !_characterRevealed)
                foreach (var state in _characterRenderers)
                    if (state.Renderer != null) state.Renderer.enabled = false;
            AlignStencilToOuterSlice();
            foreach (var material in _ownedCharacterMaterials)
            {
                if (material == null || !material.HasProperty("_ArrivalCrossing")) continue;
                material.SetVector("_ArrivalCenter", _portalSlice.position);
                material.SetVector("_ArrivalNormal", _portalSlice.forward);
                material.SetVector("_ArrivalRight", _portalSlice.right);
                material.SetVector("_ArrivalSize", _portalSlice.lossyScale);
                material.SetFloat("_ArrivalOpening", _aperture.Opening * _windowStrength);
                material.SetFloat("_ArrivalTime", _ritualTime);
            }
        }

        void AlignStencilToOuterSlice()
        {
            _portalStencil.SetPositionAndRotation(_portalSlice.position, _portalSlice.rotation);
            _portalStencil.localScale = Vector3.Scale(
                _portalSlice.lossyScale,
                InverseLossyScale(_portalStencil.parent));
            if (_rim != null)
            {
                _rim.transform.SetPositionAndRotation(_portalSlice.position - _portalSlice.forward * .002f, _portalSlice.rotation);
                _rim.transform.localScale = Vector3.Scale(_portalSlice.lossyScale, InverseLossyScale(_rim.transform.parent));
            }
        }

        void SynchronizeSpatialPose()
        {
            transform.SetPositionAndRotation(_windowPosition, _windowRotation);
            _aperture.RefreshPose();
            if (_environment != null) _environment.SetPositionAndRotation(_environmentPosition, Quaternion.identity);
        }

        void CaptureCharacterRenderers()
        {
            foreach (var renderer in _target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.transform.IsChildOf(transform)) continue;
                var originalMaterials = renderer.sharedMaterials;
                var portalMaterials = new Material[originalMaterials.Length];
                var supportsPortalStencil = originalMaterials.Length > 0;
                for (var index = 0; index < originalMaterials.Length; index++)
                {
                    var source = originalMaterials[index];
                    if (source == null || !source.HasProperty(StencilComparisonProperty))
                    {
                        supportsPortalStencil = false;
                        break;
                    }

                    var material = new Material(source)
                    {
                        name = $"{source.name} (Other World Instance)",
                        hideFlags = HideFlags.DontSave
                    };
                    material.SetFloat(StencilReferenceProperty, 1f);
                    material.SetFloat(StencilReadMaskProperty, 255f);
                    material.SetFloat(StencilComparisonProperty, (float)CompareFunction.Equal);
                    portalMaterials[index] = material;
                    _ownedCharacterMaterials.Add(material);
                }

                if (supportsPortalStencil)
                    renderer.sharedMaterials = portalMaterials;
                else
                    DestroyMaterials(portalMaterials);

                _characterRenderers.Add(new CharacterRendererState(
                    renderer,
                    originalMaterials,
                    renderer.enabled,
                    supportsPortalStencil));
                renderer.enabled = false;
            }

            if (_characterRenderers.Count == 0)
                throw new InvalidOperationException("Fairy other-world window found no character renderers to reveal.");
        }

        static void ValidateAndLimitEnvironment(Transform environment)
        {
            var visibleRendererCount = 0;
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                var supportsPortalStencil = materials.Length > 0;
                foreach (var material in materials)
                {
                    if (material == null || !material.HasProperty(StencilComparisonProperty))
                    {
                        supportsPortalStencil = false;
                        break;
                    }
                }

                renderer.enabled &= supportsPortalStencil;
                if (renderer.enabled) visibleRendererCount++;
            }

            if (visibleRendererCount == 0)
                throw new InvalidOperationException(
                    "Fairy other-world environment has no stencil-compatible visible renderer.");
        }

        void RestoreCharacterRenderers()
        {
            foreach (var state in _characterRenderers)
            {
                if (state.Renderer == null) continue;
                state.Renderer.sharedMaterials = state.OriginalMaterials;
                state.Renderer.enabled = state.WasEnabled;
            }
        }

        void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("Fairy other-world window is not initialized.");
        }

        void OnDisable()
        {
            if (_ambience != null) _ambience.Stop();
            if (_pollen != null) _pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDestroy()
        {
            if (!_characterRevealed) RestoreCharacterRenderers();
            foreach (var material in _ownedCharacterMaterials)
                DestroyOwnedMaterial(material);
            DestroyOwnedMaterial(_portalStencilMaterial);
            DestroyOwnedMaterial(_pollenMaterial);
            DestroyOwnedMaterial(_rimMaterial);
            _characterRenderers.Clear();
            _ownedCharacterMaterials.Clear();
            _portalStencilMaterial = null;
        }

        static Vector3 InverseLossyScale(Transform owner)
        {
            if (owner == null) return Vector3.one;
            var scale = owner.lossyScale;
            return new Vector3(
                Mathf.Abs(scale.x) > 0.000001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.000001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.000001f ? 1f / scale.z : 1f);
        }

        static void DestroyMaterials(IEnumerable<Material> materials)
        {
            foreach (var material in materials) DestroyOwnedMaterial(material);
        }

        static void DestroyOwnedMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        readonly struct CharacterRendererState
        {
            internal CharacterRendererState(
                Renderer renderer,
                Material[] originalMaterials,
                bool wasEnabled,
                bool supportsPortalStencil)
            {
                Renderer = renderer;
                OriginalMaterials = originalMaterials;
                WasEnabled = wasEnabled;
                SupportsPortalStencil = supportsPortalStencil;
            }

            internal Renderer Renderer { get; }
            internal Material[] OriginalMaterials { get; }
            internal bool WasEnabled { get; }
            internal bool SupportsPortalStencil { get; }
        }
    }
}
