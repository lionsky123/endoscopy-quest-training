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
            _ownedSealMesh = CreateLeafSeal();
            _sealFilter.sharedMesh = _ownedSealMesh;
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
                _seal.localScale = _sealRestScale * (1f + .35f * _hold.Progress);
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
        static Mesh CreateLeafSeal()
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
            for (int side = -1; side <= 1; side += 2)
            {
                var previous = new Vector3(0, -.5f, 0);
                for (int i = 1; i <= 24; i++)
                {
                    var t = i / 24f;
                    var next = new Vector3(side * .42f * Mathf.Sin(Mathf.PI * t), t - .5f, 0);
                    Line(previous, next, .013f); previous = next;
                }
                for (int i = 1; i <= 3; i++)
                {
                    var y = -.36f + i * .19f;
                    Line(new Vector3(0, y - .10f, 0), new Vector3(side * .29f, y + .08f, 0), .009f);
                }
            }
            Line(new Vector3(0, -.55f, 0), new Vector3(0, .45f, 0), .012f);
            var mesh = new Mesh { name = "Invitation leaf outline" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public void Unconfigure()
        {
            if (!_configured) return;
            PresentHidden();
            if (_bookMesh != null) _bookMesh.sharedMesh = _sourceMesh;
            if (_sealFilter != null) _sealFilter.sharedMesh = _sourceSealMesh;
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
