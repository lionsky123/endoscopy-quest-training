using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.VisitorPrologue.Frontend
{
    /// <summary>One invitation surface. A palm hold and gaze fallback share one commit latch.</summary>
    public sealed class FieldbookInvitationRitual : MonoBehaviour
    {
        [SerializeField] Transform _seal;
        [SerializeField] MeshFilter _bookMesh;
        [SerializeField] Transform _bookVisual;
        [SerializeField] ParticleSystem _pageLight;
        VisitorHandReadinessAdapter _hands;
        VisitorPrologueThemeAsset _theme;
        Mesh _ownedMesh, _ownedSealMesh, _sourceSealMesh;
        MeshFilter _sealFilter;
        Material _handprintMaterial, _sourceSealMaterial;
        Vector3 _sourceSealScale;
        Mesh _sourceMesh;
        Vector3[] _vertices, _normals, _folded, _foldedNormals;
        Vector3 _restScale, _sealRestScale;
        readonly InvitationHold _hold = new InvitationHold();
        bool _available, _opening, _configured, _bookOpened;
        float _openElapsed, _clearElapsed;
        const float AppearSeconds = 1.4f;
        float _appearElapsed;
        bool _appearSoundStarted;
        Vector3 _bookRestPosition;
        AudioSource _appearAudio, _contactAudio;
        public Vector3 InvitationWorldOrigin => _seal.position;
        internal bool HasOpened => _bookOpened;
        public event Action<VisitorPrologueInputModality> InvitationRequested;
        public event Action BookOpened;
        public float HoldProgress => _hold.Progress;
        public string Instruction => _hold.Progress > 0
            ? $"手掌已对准，保持不动 · {Mathf.FloorToInt(_hold.Progress * 100)}%\n手印亮满后，魔法书会自动打开。"
            : "① 张开一只手，掌心朝向发光手印。\n② 靠近保持约1秒，等手印亮满。\n看不到手时，把双手放到视野前方。";

        public void Configure(VisitorPrologueThemeAsset theme)
        {
            if (_configured || _seal == null || _bookMesh == null || _bookMesh.sharedMesh == null || _bookVisual == null || _pageLight == null)
                throw new InvalidOperationException("Fieldbook invitation requires its authored book, seal and page light.");
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _sourceMesh = _bookMesh.sharedMesh;
            _ownedMesh = Instantiate(_sourceMesh);
            _ownedMesh.name = "Invitation book instance";
            _bookMesh.sharedMesh = _ownedMesh;
            _vertices = _sourceMesh.vertices; _normals = _sourceMesh.normals;
            _folded = new Vector3[_vertices.Length]; _foldedNormals = new Vector3[_normals.Length];
            _sealFilter = _seal.GetComponent<MeshFilter>();
            _sourceSealMesh = _sealFilter.sharedMesh;
            _ownedSealMesh = CreateHandprint();
            _sealFilter.sharedMesh = _ownedSealMesh;
            var sealRenderer = _seal.GetComponent<MeshRenderer>();
            _sourceSealMaterial = sealRenderer.sharedMaterial;
            _handprintMaterial = new Material(_sourceSealMaterial) { name = "Invitation handprint glow" };
            _handprintMaterial.EnableKeyword("_EMISSION");
            _handprintMaterial.SetFloat("_Cull", 0);
            _handprintMaterial.SetColor("_BaseColor", new Color(.45f, 1, .83f));
            sealRenderer.sharedMaterial = _handprintMaterial;
            _sourceSealScale = _seal.localScale;
            _seal.localScale = new Vector3(.12f, .16f, .008f);
            _restScale = _bookVisual.localScale;
            _sealRestScale = _seal.localScale;
            _bookRestPosition = _bookVisual.localPosition;
            _appearAudio = gameObject.AddComponent<AudioSource>();
            _contactAudio = gameObject.AddComponent<AudioSource>();
            foreach (var audio in new[] { _appearAudio, _contactAudio })
            { audio.playOnAwake = false; audio.spatialBlend = .35f; audio.minDistance = 1f; audio.maxDistance = 5f; audio.dopplerLevel = 0f; }
            _contactAudio.loop = true;
            _contactAudio.clip = _theme.SealContactAudio;
            _configured = true;
            PresentHidden();
        }
        public void BindHands(VisitorHandReadinessAdapter hands) => _hands = hands ?? throw new ArgumentNullException(nameof(hands));
        public void PresentAvailable()
        {
            if (_available) return;
            _opening = false; _available = true; _bookOpened = false; _clearElapsed = 0f; _hold.Reset();
            _appearElapsed = 0f; _appearSoundStarted = false;
            _bookVisual.gameObject.SetActive(true);
            _seal.gameObject.SetActive(false);
            _bookVisual.localScale = Vector3.zero;
            _bookVisual.localPosition = _bookRestPosition;
            _seal.localScale = _sealRestScale;
            Fold(1f);
        }
        public void PresentOpening()
        {
            if (_bookOpened) return;
            _bookVisual.gameObject.SetActive(true);
            if (_opening) return;
            _available = false; _opening = true; _openElapsed = 0;
            _contactAudio.Stop();
            _seal.gameObject.SetActive(false);
            _pageLight.Play(true);
        }
        public void PresentHidden()
        {
            _available = _opening = false; _hold.Reset();
            if (_bookVisual != null) _bookVisual.gameObject.SetActive(false);
            if (_seal != null) _seal.gameObject.SetActive(false);
            if (_pageLight != null) _pageLight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_appearAudio != null) _appearAudio.Stop();
            if (_contactAudio != null) _contactAudio.Stop();
        }
        public void BeginGazeInvitation()
        { if (_available && _appearElapsed >= AppearSeconds) Commit(VisitorPrologueInputModality.GazeFallback); }
        void Update() => Tick(Time.unscaledDeltaTime);
        internal void Tick(float seconds)
        {
            if (!_configured || !isActiveAndEnabled || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (_bookOpened)
            {
                if (_clearElapsed >= .55f) return;
                _clearElapsed += seconds;
                var clear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_clearElapsed / .55f));
                _bookVisual.localScale = _restScale * (1f - clear);
                _bookVisual.localPosition = _bookRestPosition + Vector3.down * (.14f + .14f * clear);
                if (clear >= 1f)
                {
                    _bookVisual.gameObject.SetActive(false);
                    _pageLight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                return;
            }
            if (_available)
            {
                _appearElapsed += Application.isPlaying && !_appearSoundStarted ? 0f : seconds;
                if (!_appearSoundStarted)
                {
                    _appearSoundStarted = true;
                    if (Application.isPlaying && _theme.BookAppearAudio != null) _appearAudio.PlayOneShot(_theme.BookAppearAudio, .85f);
                    _pageLight.Play(true);
                }
                var appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_appearElapsed - .25f) / (AppearSeconds - .25f)));
                _bookVisual.localScale = _restScale * appear;
                _bookVisual.localPosition = _bookRestPosition + Vector3.down * (.06f * (1f - appear));
                if (_appearElapsed < AppearSeconds) return;
                _seal.gameObject.SetActive(true);
                var candidate = _hands != null ? _hands.FindPalmAtSeal(_seal, _theme.PalmContactRadius,
                    _theme.MinimumPalmAlignment, _hold.HandId) : -1;
                if (candidate != _hold.HandId) _contactAudio.Stop();
                if (_hold.Step(candidate, seconds, _theme.PalmHoldSeconds)) Commit(VisitorPrologueInputModality.PalmHold);
                _bookVisual.localScale = _restScale * (1f + .035f * _hold.Progress);
                _seal.localScale = _sealRestScale * (1f + .04f * Mathf.Sin(_appearElapsed * 3));
                var glow = 1.15f + .4f * Mathf.Sin(_appearElapsed * 3) + _hold.Progress * 2;
                _handprintMaterial.SetColor("_EmissionColor", new Color(.22f, .9f, .62f) * glow);
                if (_available && _hold.Progress > 0f)
                {
                    _contactAudio.volume = .55f + .4f * _hold.Progress;
                    if (Application.isPlaying && !_contactAudio.isPlaying && _contactAudio.clip != null) _contactAudio.Play();
                }
                else _contactAudio.Stop();
            }
            if (!_opening) return;
            _openElapsed += seconds;
            var progress = Mathf.Clamp01(_openElapsed / _theme.BookOpenSeconds);
            Fold(1f - progress * progress * (3f - 2f * progress));
            _bookVisual.localPosition = _bookRestPosition + Vector3.down * (.14f * progress);
            if (progress < 1f) return;
            _opening = false;
            _bookOpened = true;
            BookOpened?.Invoke();
        }
        void Commit(VisitorPrologueInputModality modality)
        {
            _available = false;
            _contactAudio.Stop();
            InvitationRequested?.Invoke(modality);
        }
        void Fold(float closed)
        {
            // The approved prop is a CLOSED book. Open its front half about the spine,
            // retaining the back cover and original UVs instead of folding the whole book in half.
            var bounds = _sourceMesh.bounds;
            var spine = new Vector3(bounds.max.x - bounds.size.x * .08f, 0f, 0f);
            var rotation = Quaternion.AngleAxis(-(1f - closed) * 118f, Vector3.up);
            for (int i = 0; i < _vertices.Length; i++)
            {
                var front = closed < 1f && _vertices[i].z > bounds.center.z + .001f;
                _folded[i] = front ? rotation * (_vertices[i] - spine) + spine : _vertices[i];
                if (i < _normals.Length) _foldedNormals[i] = front ? rotation * _normals[i] : _normals[i];
            }
            _ownedMesh.vertices = _folded;
            if (_normals.Length == _vertices.Length) _ownedMesh.normals = _foldedNormals;
            _ownedMesh.RecalculateBounds();
        }
        static Mesh CreateHandprint()
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            void Line(Vector3 a, Vector3 b, float width)
            {
                var n = Vector3.Cross((b - a).normalized, Vector3.forward) * width;
                var index = vertices.Count;
                vertices.Add(a - n); vertices.Add(a + n); vertices.Add(b + n); vertices.Add(b - n);
                triangles.AddRange(new[] { index, index + 2, index + 1, index, index + 3, index + 2 });
            }
            // Recognisable open palm: four separate rounded fingers, an outward
            // thumb and a wrist. The mesh lies on the actual contact plane.
            var contour = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-.18f, -.48f), new Vector3(.18f, -.48f),
                new Vector3(.20f, -.29f), new Vector3(.32f, -.14f)
            };
            void Finger(float x, float top, float bottom)
            {
                const float radius = .054f;
                contour.Add(new Vector3(x + radius, bottom));
                for (int i = 0; i <= 12; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    contour.Add(new Vector3(x + Mathf.Cos(angle) * radius, top + Mathf.Sin(angle) * radius));
                }
                contour.Add(new Vector3(x - radius, bottom));
            }
            Finger(.267f, .23f, -.02f); Finger(.11f, .40f, .05f);
            Finger(-.05f, .48f, .08f); Finger(-.21f, .37f, -.12f);
            contour.AddRange(new[] { new Vector3(-.35f, .01f), new Vector3(-.43f, .05f),
                new Vector3(-.48f, .015f), new Vector3(-.47f, -.04f),
                new Vector3(-.35f, -.27f), new Vector3(-.20f, -.37f) });
            for (int i = 0; i < contour.Count; i++) Line(contour[i], contour[(i + 1) % contour.Count], .014f);
            Line(new Vector3(-.14f, -.13f), new Vector3(.12f, -.09f), .009f);
            Line(new Vector3(-.13f, -.19f), new Vector3(.08f, -.25f), .009f);
            var mesh = new Mesh { name = "Invitation open palm handprint" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public void Unconfigure()
        {
            if (!_configured) return;
            PresentHidden();
            if (_bookMesh != null) _bookMesh.sharedMesh = _sourceMesh;
            if (_sealFilter != null) _sealFilter.sharedMesh = _sourceSealMesh;
            if (_seal != null)
            {
                _seal.localScale = _sourceSealScale;
                _seal.GetComponent<MeshRenderer>().sharedMaterial = _sourceSealMaterial;
            }
            if (_handprintMaterial != null) Release(_handprintMaterial);
            _handprintMaterial = null;
            if (Application.isPlaying) Destroy(_ownedSealMesh); else DestroyImmediate(_ownedSealMesh);
            _ownedSealMesh = null;
            if (Application.isPlaying) Destroy(_ownedMesh); else DestroyImmediate(_ownedMesh);
            _ownedMesh = null; _configured = false; _hands = null;
            if (_appearAudio != null) Release(_appearAudio);
            if (_contactAudio != null) Release(_contactAudio);
        }
        static void Release(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDisable() { if (_configured) PresentHidden(); }
        void OnDestroy() => Unconfigure();
    }
    internal sealed class InvitationHold
    {
        float _seconds;
        public int HandId { get; private set; } = -1;
        public float Progress { get; private set; }
        public bool Step(int candidate, float seconds, float requiredSeconds)
        {
            if (candidate < 0 || candidate != HandId) { Reset(); HandId = candidate; }
            if (candidate < 0) return false;
            _seconds += Mathf.Clamp(seconds, 0f, .1f);
            Progress = Mathf.Clamp01(_seconds / requiredSeconds);
            return Progress >= 1f;
        }
        public void Reset() { HandId = -1; _seconds = Progress = 0; }
    }
}
