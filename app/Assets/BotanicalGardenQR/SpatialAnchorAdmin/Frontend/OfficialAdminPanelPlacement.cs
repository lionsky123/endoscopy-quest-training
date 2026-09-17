using UnityEngine;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Official
{
    /// <summary>Places the administrator panel in front of the current viewer.</summary>
    public sealed class OfficialAdminPanelPlacement : MonoBehaviour
    {
        [SerializeField] MonoBehaviour _officialAnchorAdminSource;
        [SerializeField] Transform _viewer;
        [SerializeField] float _distance = 1.25f;
        [SerializeField] float _horizontalOffset;
        [SerializeField] float _verticalOffset = 0.15f;
        [SerializeField, Min(0f)] float _initialSummonDelay = 0.45f;
        [SerializeField, Min(0.01f)] float _worldScale = 0.22f;
        [SerializeField] bool _keepUpright = true;
        [SerializeField] bool _summonOnEnable = true;

        bool _hasSummoned;
        bool _isVisible;
        float _summonAllowedAt;
        Canvas[] _canvases;
        bool[] _canvasStates;
        Renderer[] _renderers;
        bool[] _rendererStates;
        IOfficialSpatialAnchorAdminCommands _officialAnchorAdmin;

        public bool HasSummoned => _hasSummoned;
        public bool IsVisible => _isVisible;
        public bool ViewerUnavailable { get; private set; }

        void Awake()
        {
            CacheVisualState();
            SetVisualsVisible(false);
        }

        void OnEnable()
        {
            Bind();
            SetVisualsVisible(false);
            _hasSummoned = false;
            _summonAllowedAt = Time.unscaledTime + Mathf.Max(0f, _initialSummonDelay);
        }

        void OnDisable() => Unbind();

        void Update()
        {
            if (_summonOnEnable &&
                !_hasSummoned &&
                Time.unscaledTime >= _summonAllowedAt)
                Summon();
        }

        public bool Summon()
        {
            CacheVisualState();
            if (!_viewer) _viewer = ResolveViewer();
            if (!_viewer)
            {
                ViewerUnavailable = true;
                Debug.LogWarning("[OfficialAnchorAdmin] admin.panel.summon viewer_unavailable");
                return false;
            }

            ViewerUnavailable = false;
            var pose = CalculateTargetPose(
                _viewer.position, _viewer.rotation, _distance,
                _horizontalOffset, _verticalOffset, _keepUpright);
            if (transform.parent) transform.SetParent(null, true);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, _worldScale);
            RestoreVisualState();
            _hasSummoned = true;
            return true;
        }

        public void Hide()
        {
            SetVisualsVisible(false);
            _hasSummoned = false;
        }

        public void SetViewer(Transform viewer) => _viewer = viewer;

        void Bind()
        {
            if (_officialAnchorAdmin != null) return;
            _officialAnchorAdmin = _officialAnchorAdminSource as IOfficialSpatialAnchorAdminCommands;
            if (_officialAnchorAdmin != null)
                _officialAnchorAdmin.WorkspaceVisibilityRequested += HandleWorkspaceVisibilityRequested;
        }

        void Unbind()
        {
            if (_officialAnchorAdmin != null)
                _officialAnchorAdmin.WorkspaceVisibilityRequested -= HandleWorkspaceVisibilityRequested;
            _officialAnchorAdmin = null;
        }

        void HandleWorkspaceVisibilityRequested(bool visible)
        {
            if (visible) Summon();
            else Hide();
        }

        void CacheVisualState()
        {
            if (_canvases != null) return;
            _canvases = GetComponentsInChildren<Canvas>(true);
            _canvasStates = new bool[_canvases.Length];
            for (var i = 0; i < _canvases.Length; i++) _canvasStates[i] = _canvases[i].enabled;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++) _rendererStates[i] = _renderers[i].enabled;
        }

        void SetVisualsVisible(bool visible)
        {
            CacheVisualState();
            for (var i = 0; i < _canvases.Length; i++) _canvases[i].enabled = visible && _canvasStates[i];
            for (var i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible && _rendererStates[i];
            _isVisible = visible;
        }

        void RestoreVisualState()
        {
            for (var i = 0; i < _canvases.Length; i++) _canvases[i].enabled = _canvasStates[i];
            for (var i = 0; i < _renderers.Length; i++) _renderers[i].enabled = _rendererStates[i];
            _isVisible = true;
        }

        static Transform ResolveViewer()
        {
            var mainCamera = Camera.main;
            return mainCamera ? mainCamera.transform : null;
        }

        public static Pose CalculateTargetPose(
            Vector3 viewerPosition,
            Quaternion viewerRotation,
            float distance,
            float horizontalOffset,
            float verticalOffset,
            bool keepUpright)
        {
            var forward = keepUpright
                ? Vector3.ProjectOnPlane(viewerRotation * Vector3.forward, Vector3.up).normalized
                : viewerRotation * Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            var right = keepUpright
                ? Vector3.Cross(Vector3.up, forward).normalized
                : viewerRotation * Vector3.right;
            var position = viewerPosition
                         + forward.normalized * Mathf.Max(0.2f, distance)
                         + right.normalized * horizontalOffset
                         + Vector3.up * verticalOffset;
            var toTarget = position - viewerPosition;
            if (keepUpright) toTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            var rotation = toTarget.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                : Quaternion.identity;
            return new Pose(position, rotation);
        }
    }
}
