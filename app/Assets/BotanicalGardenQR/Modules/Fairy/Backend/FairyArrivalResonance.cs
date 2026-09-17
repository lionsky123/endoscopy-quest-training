using System;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace BotanicalGardenQR.Fairy.Backend
{
    // One touch-owned arrival scope. Geometry/materials come from explicitly authored assets.
    public sealed class FairyArrivalResonance : MonoBehaviour
    {
        [SerializeField] GameObject _artifactPrefab;
        [SerializeField] Material _material;
        [SerializeField] Shader _lightningShader, _speedLinesShader;
        [SerializeField] TMP_FontAsset _font;
        TextMeshPro _hint;
        Transform _viewer, _marker;
        Material _instanceMaterial, _glassMaterial, _handsMaterial;
        FairyArrivalRupture _rupture;
        Func<Vector3, Vector3?> _handPosition;
        Vector3 _home, _door;
        Quaternion _facing;
        float _noHandSeconds, _gazeSeconds, _elapsed, _cinematicSeconds;
        bool _armed, _touched, _completed, _conduitPlayed, _rupturePlayed;
        public bool Completed => _completed;
        public bool Touched => _touched;
        public Vector3 Position => _marker != null ? _marker.position : _home;
        public float Progress { get; private set; }
        internal float WorldShift => !_touched ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(_cinematicSeconds / .4f));
        internal float Flash => !_touched ? 0 : FairyController.ArrivalPulse(_cinematicSeconds, 1.35f, 1.55f, 1.95f);
        internal event Action<string> Changed;

        internal void Initialize(Transform viewer, Vector3 door, Func<Vector3, Vector3?> handPosition)
        {
            if (_artifactPrefab == null || _material == null || _lightningShader == null || _speedLinesShader == null)
                throw new InvalidOperationException("Arrival artifact or sourced effect binding is missing.");
            _viewer = viewer; _door = door; _handPosition = handPosition;
            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
            _home = viewer.position + forward * .48f - Vector3.up * .22f;
            _facing = Quaternion.LookRotation(-forward);
            _marker = new GameObject("ArrivalPocketWatch").transform;
            _marker.SetParent(transform, false);
            var model = Instantiate(_artifactPrefab, _marker);
            model.name = "PolyHavenPocketWatch";
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Arrival model has no renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var scale = .24f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.position -= bounds.center - _marker.position;
            model.transform.localPosition *= scale; model.transform.localScale *= scale;
            _instanceMaterial = new Material(_material);
            _handsMaterial = new Material(_material);
            _handsMaterial.SetColor("_BaseColor", new Color(.08f, .065f, .04f));
            _handsMaterial.SetColor("_EmissionColor", Color.black);
            _glassMaterial = new Material(_material);
            _glassMaterial.SetTexture("_BaseMap", null); _glassMaterial.SetTexture("_BumpMap", null);
            _glassMaterial.SetTexture("_MetallicGlossMap", null); _glassMaterial.SetTexture("_EmissionMap", null);
            _glassMaterial.DisableKeyword("_NORMALMAP"); _glassMaterial.DisableKeyword("_METALLICSPECGLOSSMAP"); _glassMaterial.DisableKeyword("_EMISSION");
            _glassMaterial.SetColor("_BaseColor", new Color(.85f, .95f, 1, .045f));
            _glassMaterial.SetFloat("_Surface", 1); _glassMaterial.SetFloat("_ZWrite", 0);
            _glassMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _glassMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _glassMaterial.renderQueue = 3000;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var name = materials[i] != null ? materials[i].name.ToLowerInvariant() : "";
                    materials[i] = name.Contains("glass") ? _glassMaterial : name.Contains("hands") ? _handsMaterial : _instanceMaterial;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            _marker.gameObject.SetActive(false);
            var hint = new GameObject("ArrivalInteractionHint"); hint.transform.SetParent(transform, false);
            _hint = hint.AddComponent<TextMeshPro>(); _hint.font = _font; _hint.fontSize = 2.4f;
            _hint.alignment = TextAlignmentOptions.Center; _hint.rectTransform.sizeDelta = new Vector2(5, 1.4f);
            _hint.transform.localScale = Vector3.one * .1f; hint.SetActive(false);
            _rupture = new FairyArrivalRupture(transform, viewer, door, _lightningShader, _speedLinesShader);
        }

        internal void Tick(float dt, bool armed)
        {
            if (!armed || _marker == null || _completed) return;
            _elapsed += dt;
            if (!_armed) { _armed = true; _marker.position = _door; _marker.gameObject.SetActive(true); Changed?.Invoke("artifact-escaped"); }
            if (_touched)
            {
                _cinematicSeconds += dt;
                _hint.gameObject.SetActive(false);
                var returnProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_cinematicSeconds - .35f) / 2.3f));
                _marker.position = Vector3.Lerp(_home, _door, returnProgress);
                _marker.localScale = Vector3.one * (1 - Mathf.Clamp01((_cinematicSeconds - 2.2f) / .65f));
                _marker.rotation = _facing * Quaternion.Euler(0, 0, _cinematicSeconds * 280);
                _instanceMaterial.SetColor("_EmissionColor", new Color(.25f, .18f, .06f) * (1 + Flash * 3));
                _rupture.Tick(_cinematicSeconds, Position);
                if (!_conduitPlayed && _cinematicSeconds >= .3f) { _conduitPlayed = true; Changed?.Invoke("artifact-conduit"); }
                if (!_rupturePlayed && _cinematicSeconds >= 1.35f) { _rupturePlayed = true; Changed?.Invoke("space-rupture"); }
                if (_cinematicSeconds >= 3f)
                {
                    _completed = true; _marker.gameObject.SetActive(false); Progress = 1;
                    Changed?.Invoke("echo-connected");
                }
                return;
            }
            var flight = Mathf.Clamp01(_elapsed / .85f);
            _marker.position = Vector3.Lerp(_door, _home, Mathf.SmoothStep(0, 1, flight)) + Vector3.up * (Mathf.Sin(flight * Mathf.PI) * .18f);
            _marker.rotation = _facing * Quaternion.Euler(12 * Mathf.Sin(_elapsed), 360 * (1 - flight), 12 * Mathf.Sin(_elapsed * 1.6f));
            if (flight >= 1) Sample(dt, _handPosition?.Invoke(Position));
            _hint.gameObject.SetActive(!_touched && _elapsed >= 2);
            _hint.text = _noHandSeconds >= 8 ? "注视怀表，唤醒它" : "轻触怀表";
            _hint.transform.position = _home - Vector3.up * .18f;
            _hint.transform.rotation = Quaternion.LookRotation(_hint.transform.position - _viewer.position);
        }

        internal void Sample(float dt, Vector3? hand)
        {
            if (_touched || _completed || _elapsed < .85f) return;
            if (hand.HasValue)
            {
                _noHandSeconds = 0; _gazeSeconds = 0; Progress = 0;
                var position = hand.Value;
                if (!float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) && Vector3.Distance(position, Position) <= .16f) Touch();
            }
            else
            {
                _noHandSeconds += dt;
                if (_noHandSeconds >= 8 && Vector3.Angle(_viewer.forward, Position - _viewer.position) < 8)
                { _gazeSeconds += dt; Progress = Mathf.Clamp01(_gazeSeconds / 1.5f); if (Progress >= 1) Touch(); }
                else { _gazeSeconds = 0; Progress = 0; }
            }
        }
        void Touch() { _touched = true; Progress = 1; Changed?.Invoke("artifact-touched"); }
        void OnDestroy()
        {
            _rupture?.Dispose(); _rupture = null;
            foreach (var material in new[] { _instanceMaterial, _glassMaterial, _handsMaterial })
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
