using System;
using System.Collections;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorPrologue.Frontend
{
    [DisallowMultipleComponent]
    public sealed class VisitorProloguePresenter : MonoBehaviour, IVisitorPrologueStateSink, IDisposable
    {
        [SerializeField] GameObject _presentationRoot;
        [SerializeField] GameObject _failureRoot;
        [SerializeField] GameObject _invitationRoot;
        [SerializeField] Canvas _failureCanvas;
        [SerializeField] CanvasGroup _failureGroup;
        [SerializeField] Button _retryButton;
        [SerializeField] TMP_Text _retryActionLabel;
        [SerializeField] TMP_Text _failureTitle;
        [SerializeField] TMP_Text _failureDetail;
        [SerializeField] Canvas _invitationCanvas;
        [SerializeField] CanvasGroup _invitationGroup;
        [SerializeField] FieldbookInvitationRitual _invitationRitual;
        [SerializeField] Button _gazeInvitationButton;
        [SerializeField] TMP_Text _gazeInvitationLabel;
        [SerializeField] TMP_Text _invitationTitle;
        [SerializeField] TMP_Text _invitationDetail;
        [SerializeField] AudioSource _audioSource;
        IVisitorPrologue _prologue;
        VisitorPrologueThemeAsset _theme;
        Transform _viewer;
        IDisposable _subscription;
        IFrontendGazeSurfaceRegistration _failureGaze, _invitationGaze;
        Coroutine _placement;
        long _placedEpoch = -1, _pendingEpoch = -1, _gazeProvenEpoch = -1;
        bool _configured, _disposed, _visible;
        VisitorPrologueViewState _previous;
        public event Action GazeDwellProven;
        public event Action<bool> SurfaceVisibilityChanged;
        public bool IsSurfaceVisible => _visible;
        public Vector3 InvitationWorldOrigin => _invitationRitual.InvitationWorldOrigin;

        public void Configure(Transform viewer, IFrontendGazeSurfaceRegistry gazeSurfaces, VisitorPrologueThemeAsset theme)
        {
            if (_configured || _disposed) throw new InvalidOperationException("Invitation presenter cannot be configured twice.");
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            if (!_theme.IsValid(out var error)) throw new InvalidOperationException(error);
            if (_presentationRoot == null || _failureRoot == null || _invitationRoot == null || _failureCanvas == null ||
                _failureGroup == null || _retryButton == null || _retryActionLabel == null || _failureTitle == null ||
                _failureDetail == null || _invitationCanvas == null || _invitationGroup == null || _invitationRitual == null ||
                _gazeInvitationButton == null || _gazeInvitationLabel == null || _invitationTitle == null ||
                _invitationDetail == null || _audioSource == null) throw new InvalidOperationException("Invitation prefab bindings are incomplete.");
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            _failureCanvas.worldCamera = _invitationCanvas.worldCamera = viewer.GetComponent<Camera>();
            _failureGaze = gazeSurfaces.RegisterGazeSurface(_failureCanvas.transform, 460, "InvitationFailure");
            _invitationGaze = gazeSurfaces.RegisterGazeSurface(_invitationCanvas.transform, 460, "FieldbookInvitation");
            _invitationRitual.Configure(theme);
            _audioSource.spatialBlend = .35f;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 5f;
            _audioSource.dopplerLevel = 0f;
            _audioSource.volume = .75f;
            _invitationRitual.InvitationRequested += OnInvitation;
            _invitationRitual.BookOpened += OnBookOpened;
            _gazeInvitationButton.onClick.AddListener(OnGazeInvitation);
            _retryButton.onClick.AddListener(OnRetry);
            _configured = true;
            Hide();
        }
        public void BindHands(VisitorHandReadinessAdapter hands) => _invitationRitual.BindHands(hands);
        public void Bind(IVisitorPrologue prologue)
        {
            if (!_configured || _subscription != null) throw new InvalidOperationException("Invitation presenter binding is invalid.");
            _prologue = prologue ?? throw new ArgumentNullException(nameof(prologue));
            _subscription = prologue.Observe(this);
        }
        public void OnVisitorPrologueStateChanged(VisitorPrologueViewState state)
        {
            if (_disposed || state == null) return;
            if (_previous == null || _previous.Epoch != state.Epoch || _previous.Phase != state.Phase ||
                _previous.CanInviteWithGaze != state.CanInviteWithGaze)
            { _failureGaze.Invalidate(); _invitationGaze.Invalidate(); }
            _previous = state;
            var invitation = state.Phase == VisitorProloguePhase.Invitation;
            var arrival = state.Phase == VisitorProloguePhase.Arrival;
            var failure = state.Phase == VisitorProloguePhase.Failed;
            if (!invitation && !arrival && !failure) { Hide(); return; }
            _failureRoot.SetActive(failure);
            _invitationRoot.SetActive(!failure);
            SetGroup(_failureGroup, failure);
            SetGroup(_invitationGroup, !failure && (!arrival || !_invitationRitual.HasOpened));
            _gazeInvitationButton.gameObject.SetActive(state.CanInviteWithGaze);
            _gazeInvitationButton.interactable = state.CanInviteWithGaze;
            if (failure)
            {
                _invitationRitual.PresentHidden();
                _failureTitle.text = "联系暂时中断";
                _failureDetail.text = state.Fault;
                _retryActionLabel.text = _theme.Copy.RetryAction;
                _retryButton.gameObject.SetActive(true);
            }
            else
            {
                _invitationTitle.text = invitation ? _theme.Copy.InvitationTitle : _theme.Copy.ArrivalTitle;
                _invitationDetail.text = invitation ? _theme.Copy.InvitationDetail : _theme.Copy.ArrivalDetail;
                _gazeInvitationLabel.text = _theme.Copy.GazeInvitationAction;
                if (invitation) _invitationRitual.PresentAvailable(); else _invitationRitual.PresentOpening();
            }
            EnsurePlaced(state.Epoch);
        }
        void OnInvitation(VisitorPrologueInputModality modality)
        {
            if (_prologue == null || !_visible) return;
            var state = _prologue.CurrentState;
            if (_prologue.RequestInvitation(state.Epoch, modality).Succeeded)
                _audioSource.PlayOneShot(_theme.InvitationAudio);
            else if (_prologue.CurrentState.Phase == VisitorProloguePhase.Invitation)
                _invitationRitual.PresentAvailable();
        }
        void OnGazeInvitation()
        {
            if (_prologue?.CurrentState.CanInviteWithGaze == true) _invitationRitual.BeginGazeInvitation();
        }
        void OnBookOpened()
        {
            if (_prologue != null) _prologue.ReportBookOpened(_prologue.CurrentState.Epoch);
            SetGroup(_invitationGroup, false);
        }
        void OnRetry() => _prologue?.Retry();
        public void ReportDialogueGazeAccepted(long epoch)
        {
            if (_disposed || _prologue == null || epoch != _prologue.CurrentState.Epoch || _gazeProvenEpoch == epoch) return;
            _gazeProvenEpoch = epoch;
            GazeDwellProven?.Invoke();
        }
        void EnsurePlaced(long epoch)
        {
            if (_placedEpoch == epoch) { SetVisible(true); return; }
            if (_pendingEpoch == epoch) return;
            if (_placement != null) StopCoroutine(_placement);
            _pendingEpoch = epoch;
            SetVisible(false);
            _placement = StartCoroutine(PlaceAfterTracking(epoch));
        }
        IEnumerator PlaceAfterTracking(long epoch)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(_theme.InitialPlacementDelaySeconds);
            _placement = null; _pendingEpoch = -1;
            if (_disposed || _prologue == null || _prologue.CurrentState.Epoch != epoch ||
                (_prologue.CurrentState.Phase != VisitorProloguePhase.Invitation && _prologue.CurrentState.Phase != VisitorProloguePhase.Failed)) yield break;
            WorldSurfacePlacement.PlaceViewerFrontAtMinimumHeight(_presentationRoot.transform, _viewer,
                _theme.ViewerDistance, _theme.VerticalOffset, _theme.MinimumStandingPanelCenterHeight);
            _placedEpoch = epoch;
            SetVisible(true);
            if (_prologue.CurrentState.Phase == VisitorProloguePhase.Invitation) _invitationRitual.PresentAvailable();
        }
        void SetVisible(bool visible)
        {
            _presentationRoot.SetActive(visible);
            if (_visible == visible) return;
            _visible = visible; SurfaceVisibilityChanged?.Invoke(visible);
        }
        void Hide()
        {
            _invitationRitual.PresentHidden();
            SetGroup(_failureGroup, false); SetGroup(_invitationGroup, false);
            _failureRoot.SetActive(false); _invitationRoot.SetActive(false);
            _audioSource.Stop(); SetVisible(false);
        }
        static void SetGroup(CanvasGroup group, bool visible)
        { group.alpha = visible ? 1 : 0; group.interactable = group.blocksRaycasts = visible; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_placement != null) StopCoroutine(_placement);
            _subscription?.Dispose(); _failureGaze?.Dispose(); _invitationGaze?.Dispose();
            if (!_configured) return;
            _invitationRitual.InvitationRequested -= OnInvitation; _invitationRitual.BookOpened -= OnBookOpened;
            _gazeInvitationButton.onClick.RemoveListener(OnGazeInvitation); _retryButton.onClick.RemoveListener(OnRetry);
            Hide(); _invitationRitual.Unconfigure();
            GazeDwellProven = null; SurfaceVisibilityChanged = null;
        }
        void OnDestroy() => Dispose();
    }
}
