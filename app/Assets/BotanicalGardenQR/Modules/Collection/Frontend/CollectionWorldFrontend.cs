using System;
using System.Collections;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Oculus.Interaction.Input;

namespace BotanicalGardenQR.Collection.Frontend
{
    public enum CollectionCompanionCueKind
    {
        Idle = 0,
        WaitAtArtifact = 1,
        ReturnWithArtifact = 2,
        Celebrate = 3
    }

    public enum CollectionArtifactTutorialStage
    {
        Hidden = 0,
        Available = 1,
        Held = 2,
        PlacementMissed = 3,
        Placed = 4,
        Deferred = 5
    }

    public readonly struct CollectionCompanionCue
    {
        public CollectionCompanionCue(
            CollectionCompanionCueKind kind,
            Vector3 worldPosition,
            float durationSeconds = 0f)
        {
            Kind = kind;
            WorldPosition = worldPosition;
            DurationSeconds = durationSeconds;
        }

        public CollectionCompanionCueKind Kind { get; }
        public Vector3 WorldPosition { get; }
        public float DurationSeconds { get; }
        public static CollectionCompanionCue Idle =>
            new CollectionCompanionCue(CollectionCompanionCueKind.Idle, Vector3.zero);
    }

    [Serializable]
    public sealed class CollectionBrowseEntryView
    {
        [SerializeField] GameObject _root;
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _state;
        [SerializeField] Image _silhouette;
        [SerializeField] Image _discoveredMark;

        public GameObject Root => _root;
        public Button Button => _button;
        public TMP_Text Title => _title;
        public TMP_Text State => _state;
        public Image Silhouette => _silhouette;
        public Image DiscoveredMark => _discoveredMark;

        public void Validate(int index)
        {
            if (_root == null || _button == null || _title == null || _state == null ||
                _silhouette == null || _discoveredMark == null)
                throw new InvalidOperationException($"Collection browse entry slot {index} is incomplete.");
        }
    }

    /// <summary>
    /// Thin runtime binding for the authored CollectionWorld prefab. Only the
    /// configured Artifact prefab and data-bound entry content are dynamic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectionWorldFrontend : MonoBehaviour,
        ICollectionProgressStateSink,
        ICollectionBrowseMode,
        IDisposable
    {
        const float SurfaceOpeningSeconds = 0.24f;
        const float SurfaceClosingSeconds = 0.16f;
        FieldbookFoldout _foldout;
        CollectionInstanceToken _offeredFoldoutToken;
        float _foldoutOutsideSince = -1;
        bool _foldoutRecovered;
        IHand[] _foldoutHands = Array.Empty<IHand>();
        public void BindFoldoutHands(Transform rig)
        { _foldoutHands = rig != null ? rig.GetComponentsInChildren<Hand>(true) : Array.Empty<IHand>(); }

        [Header("World roots")]
        [SerializeField] GameObject _worldRoot;
        [SerializeField] GameObject _artifactOfferRoot;
        [SerializeField] GameObject _rewardRoot;
        [SerializeField] GameObject _pageSealRoot;
        [SerializeField] GameObject _browseRoot;
        [SerializeField] GameObject _compendiumRoot;
        [SerializeField] GameObject _detailRoot;

        [Header("Artifact motion")]
        [SerializeField] Transform _artifactSpawn;
        [SerializeField] Transform _artifactCatch;
        [SerializeField] Transform _artifactReturnSlot;

        [Header("Authored canvases")]
        [SerializeField] Canvas _artifactCanvas;
        [SerializeField] CanvasGroup _artifactCanvasGroup;
        [SerializeField] Button _deferArtifactButton;
        [SerializeField] TMP_Text _artifactTitle;
        [SerializeField] TMP_Text _artifactHint;
        [SerializeField] Canvas _rewardCanvas;
        [SerializeField] CanvasGroup _rewardCanvasGroup;
        [SerializeField] Button _continueRewardButton;
        [SerializeField] TMP_Text _rewardTitle;
        [SerializeField] TMP_Text _rewardCount;
        [SerializeField] TMP_Text _rewardSummary;
        [SerializeField] Canvas _browseCanvas;
        [SerializeField] CanvasGroup _browseCanvasGroup;
        [SerializeField] Button _closeBrowseButton;
        [SerializeField] Button _toggleCompendiumButton;
        [SerializeField] Button _closeDetailButton;
        [SerializeField] TMP_Text _browseCount;
        [SerializeField] TMP_Text _detailTitle;
        [SerializeField] TMP_Text _detailSummary;
        [SerializeField] TMP_Text _detailCategory;
        [SerializeField] CollectionBrowseEntryView[] _entryViews = Array.Empty<CollectionBrowseEntryView>();

        [Header("World response")]
        [SerializeField] Transform _bookResponseRoot;
        [SerializeField] Transform _recordDisplayRoot;
        [SerializeField] ParticleSystem _confetti;
        [SerializeField] ParticleSystem _pageSealParticles;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] Color _availableTint = new Color(0.95f, 0.68f, 0.28f, 1f);
        [SerializeField] Color _collectedTint = new Color(0.27f, 0.92f, 0.63f, 1f);
        [SerializeField] Color _unseenTint = new Color(0.22f, 0.3f, 0.3f, 0.82f);

        [Header("Input priority")]
        [SerializeField] int _artifactGazePriority = 440;
        [SerializeField] int _rewardGazePriority = 445;
        [SerializeField] int _browseGazePriority = 420;

        ICollectionProgress _progress;
        ICollectionPresentationIntentSink _intents;
        IDisposable _stateSubscription;
        IFrontendGazeSurfaceRegistration _artifactGazeRegistration;
        IFrontendGazeSurfaceRegistration _rewardGazeRegistration;
        IFrontendGazeSurfaceRegistration _browseGazeRegistration;
        CollectionCatalogAsset _catalogAsset;
        CollectionProgressViewState _state;
        CollectionPendingPresentation _activePresentation;
        CollectionArtifactRecord _activeArtifactConfig;
        CollectionArtifactGrabRelay _activeArtifact;
        GameObject _activeArtifactObject;
        Transform _viewer;
        Coroutine _motion;
        Coroutine _surfaceTransition;
        UnityAction[] _entryActions = Array.Empty<UnityAction>();
        CollectionPresentationSurfaceKind _surfaceKind;
        CollectionPresentationPhase _presentationPhase;
        Vector3 _worldBaseScale;
        Vector3 _bookBaseLocalPosition;
        Vector3 _recordBaseLocalPosition;
        bool _browseAfterCollection;
        bool _browseOpeningAllowed = true;
        IFrontendGazeSurfaceRegistry _gazeInput;
        bool _foldoutAccepted;
        bool _configured;
        bool _disposed;

        public event Action<CollectionPresentationSurfaceKind> SurfaceChanged;
        public event Action<CollectionPresentationPhase> PresentationPhaseChanged;
        public event Action<CollectionCompanionCue> CompanionCueChanged;
        public event Action<CollectionArtifactTutorialStage> ArtifactTutorialStageChanged;

        public CollectionPresentationSurfaceKind SurfaceKind => _surfaceKind;
        public CollectionPresentationPhase PresentationPhase => _presentationPhase;
        public bool CanOpenBrowseMode => !_disposed && _configured && _state != null &&
                                         _browseOpeningAllowed &&
                                         _surfaceKind == CollectionPresentationSurfaceKind.Hidden &&
                                         _presentationPhase == CollectionPresentationPhase.Hidden;

        /// <summary>
        /// Application-owned foreground availability. Collection remains input-neutral;
        /// the application decides whether another foreground experience is still active.
        /// </summary>
        public void SetBrowseOpeningAllowed(bool allowed)
        {
            if (_disposed || _browseOpeningAllowed == allowed) return;
            _browseOpeningAllowed = allowed;
        }

        public void SetArtifactTutorialHint(string copy)
        {
            if (_disposed || _artifactHint == null) return;
            _artifactHint.text = copy?.Trim() ?? string.Empty;
        }

        public void Configure(
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            CollectionCatalogAsset catalogAsset)
        {
            _gazeInput = gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces));
            if (_disposed) throw new ObjectDisposedException(nameof(CollectionWorldFrontend));
            if (_configured) throw new InvalidOperationException("Collection world presenter is already configured.");
            ValidateBindings();
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _catalogAsset = catalogAsset != null ? catalogAsset : throw new ArgumentNullException(nameof(catalogAsset));
            if (!_catalogAsset.TryValidatePresentation(out var error))
                throw new InvalidOperationException(error);
            if (!WorldSurfacePlacement.HasPositiveScaleChain(_worldRoot.transform))
                throw new InvalidOperationException("Collection world requires a positive scale chain.");
            _worldBaseScale = _worldRoot.transform.localScale;
            _bookBaseLocalPosition = _bookResponseRoot.localPosition;
            _recordBaseLocalPosition = _recordDisplayRoot.localPosition;

            var camera = viewer.GetComponent<Camera>();
            PrepareCanvas(_artifactCanvas, camera, 440);
            PrepareCanvas(_rewardCanvas, camera, 445);
            PrepareCanvas(_browseCanvas, camera, 420);
            var registry = gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces));
            _artifactGazeRegistration = registry.RegisterGazeSurface(
                _artifactCanvas.transform,
                _artifactGazePriority,
                "ArtifactDropPresentation");
            _rewardGazeRegistration = registry.RegisterGazeSurface(
                _rewardCanvas.transform,
                _rewardGazePriority,
                "CollectionRewardMode");
            _browseGazeRegistration = registry.RegisterGazeSurface(
                _browseCanvas.transform,
                _browseGazePriority,
                "CollectionBrowseMode");
            BindButtons();
            _configured = true;
            HideAllSurfaces();
        }

        public void Bind(ICollectionProgress progress, ICollectionPresentationIntentSink intents)
        {
            RequireConfigured();
            if (_stateSubscription != null)
                throw new InvalidOperationException("Collection world presenter is already bound.");
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _stateSubscription = _progress.Observe(this);
        }

        public void OnCollectionProgressStateChanged(CollectionProgressViewState state)
        {
            if (_disposed || state == null) return;
            _state = state;
            RenderBrowseState(state);

            var pending = state.PendingPresentation;
            if (pending == null)
            {
                _activePresentation = null;
                PublishCompanionCue(CollectionCompanionCue.Idle);
                if (_activeArtifactObject != null)
                {
                    Debug.LogWarning(
                        "[CollectionWorld] pending presentation cleared externally while an artifact instance exists; destroying it.");
                    StopMotion();
                    DestroyArtifact();
                }
                if (_surfaceKind != CollectionPresentationSurfaceKind.Browse && _motion == null)
                    SetSurface(CollectionPresentationSurfaceKind.Hidden);
                return;
            }
            if (_activePresentation != null && _activePresentation.Id == pending.Id) return;

            _activePresentation = pending;
            Debug.Log($"[CollectionWorld] presenting kind={pending.Kind} artifact={pending.ArtifactId} id={pending.Id.Value}.");
            switch (pending.Kind)
            {
                case CollectionPresentationKind.ArtifactOffered:
                    BeginArtifactOffer(pending);
                    break;
                case CollectionPresentationKind.Reward:
                case CollectionPresentationKind.LocalReturn:
                    BeginArtifactReturn(pending);
                    break;
            }
        }

        public void OpenBrowseMode()
        {
            if (!CanOpenBrowseMode) return;
            _browseAfterCollection = false;
            OpenBrowseWorld();
        }

        void OpenBrowseWorld()
        {
            ResetArtifactOfferLayout();
            PlaceWorld();
            _compendiumRoot.SetActive(true);
            _detailRoot.SetActive(false);
            SetSurface(CollectionPresentationSurfaceKind.Browse);
        }

        public void CloseBrowseMode()
        {
            if (_surfaceKind != CollectionPresentationSurfaceKind.Browse) return;
            _compendiumRoot.SetActive(false);
            _detailRoot.SetActive(false);
            SetSurface(CollectionPresentationSurfaceKind.Hidden);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopMotion();
            StopSurfaceTransition();
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _artifactGazeRegistration?.Dispose();
            _rewardGazeRegistration?.Dispose();
            _browseGazeRegistration?.Dispose();
            _artifactGazeRegistration = null;
            _rewardGazeRegistration = null;
            _browseGazeRegistration = null;
            UnbindButtons();
            DestroyArtifact();
            HideAllSurfaces();
            _progress = null;
            _intents = null;
            _state = null;
            _catalogAsset = null;
            _viewer = null;
            SurfaceChanged = null;
            PresentationPhaseChanged = null;
            PublishCompanionCue(CollectionCompanionCue.Idle);
            CompanionCueChanged = null;
            ArtifactTutorialStageChanged = null;
        }

        void OnDestroy() => Dispose();

        void BeginArtifactOffer(CollectionPendingPresentation pending)
        {
            StopMotion();
            DestroyArtifact();
            if (!TryResolveArtifact(pending.ArtifactId, out var artifactState, out var config))
            {
                Debug.LogWarning(
                    $"[CollectionWorld] cannot resolve presentation for '{pending.ArtifactId}'; deferring the offer.");
                PublishCompanionCue(CollectionCompanionCue.Idle);
                _intents.RequestDeferAvailableArtifactPresentation(pending.Id);
                return;
            }

            _activeArtifactConfig = config;
            _offeredFoldoutToken = artifactState.InstanceToken;
            _foldoutOutsideSince = -1;
            _foldoutRecovered = false;
            PlaceWorld();
            _foldoutAccepted = false;
            _artifactTitle.text = artifactState.Definition.VisitorTitle;
            SetArtifactTutorialHint("捏住发光页边，向外拉开；松手后可以接着抓");
            _activeArtifactObject = Instantiate(config.PresentationPrefab, _worldRoot.transform, true);
            _activeArtifactObject.name = "ArtifactPresentation_" + artifactState.Definition.ArtifactId;
            _activeArtifactObject.GetComponent<FieldbookPageView>()?.Present(artifactState.Definition.VisitorTitle, artifactState.Definition.Category);
            _activeArtifactObject.transform.localScale = config.PresentationScale;
            _activeArtifactObject.transform.SetPositionAndRotation(_artifactSpawn.position, _artifactSpawn.rotation);
            _foldout = _activeArtifactObject.GetComponent<FieldbookFoldout>();
            if (_foldout == null) throw new InvalidOperationException("The collection page requires its authored foldout transformer.");
            _foldout.Configure(config.FoldoutImage, config.FoldoutDetailImage);
            _foldout.BindHands(_foldoutHands);
            _foldout.Closed += HandleFoldoutClosed;
            _foldout.Changed += HandleFoldoutChanged;
            _foldout.Touched += HandleFoldoutTouched;
            _activeArtifact = _activeArtifactObject.GetComponent<CollectionArtifactGrabRelay>();
            if (_activeArtifact == null)
                throw new InvalidOperationException($"Artifact prefab '{config.PresentationPrefab.name}' has no CollectionArtifactGrabRelay.");
            _activeArtifact.Configure(
                artifactState.Definition.ArtifactId,
                artifactState.InstanceToken,
                _catalogAsset.PresentationTheme.ArtifactMotion,
                config.AccentColor,
                HandleArtifactGrabStarted,
                HandleArtifactReleased,
                _gazeInput);
            _activeArtifact.SetInteractionEnabled(false);
            Debug.Log($"[CollectionWorld] drop started artifact={artifactState.Definition.ArtifactId} duration={config.DropDuration}.");
            PlayAudio(config.DropAudio);
            SetSurface(CollectionPresentationSurfaceKind.ArtifactOffer);
            PublishCompanionCue(new CollectionCompanionCue(
                CollectionCompanionCueKind.WaitAtArtifact,
                _artifactCatch.position));
            _motion = StartCoroutine(DropArtifact(
                config.DropDuration,
                _catalogAsset.PresentationTheme.ArtifactMotion));
            PublishArtifactTutorialStage(CollectionArtifactTutorialStage.Available);
        }

        IEnumerator DropArtifact(
            float duration,
            CollectionArtifactMotionThemeRecord motionTheme)
        {
            var start = _artifactSpawn.position;
            var end = _artifactCatch.position;
            var control = Vector3.Lerp(start, end, 0.5f) + (Vector3.up * 0.12f);
            var baseScale = _activeArtifactConfig.PresentationScale;
            var anticipationStartScale = baseScale * 0.82f;
            var anticipationEndScale = baseScale * 1.08f;
            _activeArtifactObject.transform.localScale = anticipationStartScale;
            var elapsed = 0f;
            while (elapsed < motionTheme.AnticipationSeconds && _activeArtifactObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Smooth01(elapsed / motionTheme.AnticipationSeconds);
                _activeArtifactObject.transform.localScale = Vector3.LerpUnclamped(
                    anticipationStartScale,
                    anticipationEndScale,
                    t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration && _activeArtifactObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Smooth01(elapsed / duration);
                _activeArtifactObject.transform.position = Quadratic(start, control, end, t);
                _activeArtifactObject.transform.rotation = Quaternion.Slerp(
                    _artifactCatch.rotation * Quaternion.Euler(-12f, 28f, -8f), _artifactCatch.rotation, t);
                _activeArtifactObject.transform.localScale = Vector3.LerpUnclamped(
                    anticipationEndScale,
                    baseScale,
                    t);
                yield return null;
            }
            if (_activeArtifactObject != null)
            {
                _activeArtifactObject.transform.SetPositionAndRotation(_artifactCatch.position, _artifactCatch.rotation);
                var settleStartScale = baseScale * 1.06f;
                _activeArtifactObject.transform.localScale = settleStartScale;
                elapsed = 0f;
                while (elapsed < motionTheme.ReadySettleSeconds && _activeArtifactObject != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Smooth01(elapsed / motionTheme.ReadySettleSeconds);
                    _activeArtifactObject.transform.localScale = Vector3.LerpUnclamped(
                        settleStartScale,
                        baseScale,
                        t);
                    yield return null;
                }
            }
            if (_activeArtifactObject != null)
            {
                _activeArtifactObject.transform.localScale = baseScale;
                _activeArtifact.SetInteractionEnabled(true);
                _foldout?.SetActiveInput(true);
                HandleFoldoutChanged();
            }
            _motion = null;
        }

        void HandleArtifactGrabStarted(string artifactId, CollectionInstanceToken instanceToken)
        {
            if (_disposed || _activePresentation == null ||
                _activePresentation.Kind != CollectionPresentationKind.ArtifactOffered ||
                !string.Equals(_activePresentation.ArtifactId, artifactId, StringComparison.Ordinal))
                return;
            var artifact = FindArtifact(artifactId);
            if (artifact == null || artifact.State != CollectionArtifactStatus.Available ||
                artifact.InstanceToken != instanceToken)
                return;
            SetArtifactTutorialHint("向外拉开这一页，我们一起看看");
            _foldoutRecovered = false;
            PublishArtifactTutorialStage(CollectionArtifactTutorialStage.Held);
        }

        void HandleArtifactReleased()
        {
            HandleFoldoutChanged();
        }

        void HandleFoldoutChanged()
        {
            if (_foldout == null || _activePresentation?.Kind != CollectionPresentationKind.ArtifactOffered) return;
            SetArtifactTutorialHint(_foldout.Revealed
                ? "可以轻拨页中的图片；捏住页边合拢，就能入册"
                : "捏住发光页边，向外拉开；松手后可以接着抓");
        }

        void Update()
        {
            TickFoldoutRecovery(Time.unscaledTime);
        }

        void TickFoldoutRecovery(float now)
        {
            if (_foldout == null || _viewer == null || _motion != null ||
                _activePresentation?.Kind != CollectionPresentationKind.ArtifactOffered) return;
            if (_foldout.IsHeld || _foldoutRecovered ||
                Vector3.Distance(_activeArtifactObject.transform.position, _viewer.position) <= 1.5f)
            { _foldoutOutsideSince = -1; return; }
            if (_foldoutOutsideSince < 0) { _foldoutOutsideSince = now; return; }
            if (now - _foldoutOutsideSince < 1f) return;
            // One bounded recovery per grab attempt; never follow the visitor continuously.
            _foldout.SetActiveInput(false);
            _activeArtifact.SetInteractionEnabled(false);
            PlaceWorld();
            _activeArtifactObject.transform.SetPositionAndRotation(_artifactCatch.position, _artifactCatch.rotation);
            _activeArtifact.SetInteractionEnabled(true);
            _foldout.SetActiveInput(true);
            _foldoutRecovered = true;
            _foldoutOutsideSince = -1;
            HandleFoldoutChanged();
            SetArtifactTutorialHint("这一页已回到你面前。重新捏住页边，就能接着打开。");
        }

        void HandleFoldoutTouched()
        {
            if (_foldout == null) return;
            PlayAudio(_activeArtifactConfig?.CollectAudio);
        }

        void HandleFoldoutClosed()
        {
            if (_disposed || _activePresentation?.Kind != CollectionPresentationKind.ArtifactOffered || _foldout == null) return;
            var artifact = FindArtifact(_activePresentation.ArtifactId);
            if (artifact == null || artifact.State != CollectionArtifactStatus.Available || artifact.InstanceToken != _offeredFoldoutToken) return;
            _foldout.SetActiveInput(false);
            _foldoutAccepted = true;
            _activeArtifact.SetInteractionEnabled(false);
            _intents.RequestCollectArtifact(artifact.Definition.ArtifactId, artifact.InstanceToken);
            var current = FindArtifact(artifact.Definition.ArtifactId);
            if (current?.State != CollectionArtifactStatus.Collected)
            {
                _foldoutAccepted = false;
                if (_foldout != null && _activePresentation?.Kind == CollectionPresentationKind.ArtifactOffered && current?.InstanceToken == _offeredFoldoutToken)
                { _activeArtifact.SetInteractionEnabled(true); _foldout.RetryAfterRejectedClose(); }
                return;
            }
            PublishArtifactTutorialStage(CollectionArtifactTutorialStage.Placed);
        }

        void BeginArtifactReturn(CollectionPendingPresentation pending)
        {
            StopMotion();
            var placedByVisitor = _foldoutAccepted && _activeArtifactObject != null;
            if (_activeArtifactObject == null || _activeArtifactConfig == null)
            {
                ResetArtifactOfferLayout();
                if (!TryResolveArtifact(pending.ArtifactId, out var artifactState, out var config))
                {
                    FinishPendingPresentation(pending.Id);
                    return;
                }
                _activeArtifactConfig = config;
                _activeArtifactObject = Instantiate(config.PresentationPrefab, _artifactReturnSlot.position, _artifactReturnSlot.rotation);
                _activeArtifactObject.GetComponent<FieldbookPageView>()?.Present(artifactState.Definition.VisitorTitle, artifactState.Definition.Category);
                _activeArtifactObject.GetComponent<FieldbookFoldout>()?.Configure(config.FoldoutImage, config.FoldoutDetailImage);
                _activeArtifactObject.transform.localScale = config.PresentationScale;
                _activeArtifact = _activeArtifactObject.GetComponent<CollectionArtifactGrabRelay>();
                if (_activeArtifact != null) _activeArtifact.SetInteractionEnabled(false);
            }
            if (_activeArtifact != null) _activeArtifact.SetInteractionEnabled(false);
            _deferArtifactButton.interactable = false;
            SetSurface(CollectionPresentationSurfaceKind.Reward);
            var fullReward = pending.Kind == CollectionPresentationKind.Reward || pending.ShowFullWorld;
            _rewardTitle.text = RewardTitle(_state, pending, fullReward);
            _rewardCount.text = _state != null ? $"{_state.CollectedCount} / {_state.TotalCount}" : string.Empty;
            if (_rewardSummary != null) _rewardSummary.text = RewardSummary(_state);
            _continueRewardButton.GetComponentInChildren<TMP_Text>().text =
                _state?.IsComplete == true ? "收好见闻册" : "继续探索";
            _rewardRoot.SetActive(fullReward);
            _pageSealRoot.SetActive(!fullReward);
            if (!fullReward && _pageSealParticles != null) _pageSealParticles.Play(true);
            _motion = StartCoroutine(ReturnArtifact(pending.Id, fullReward, placedByVisitor));
        }

        IEnumerator ReturnArtifact(
            CollectionPresentationId presentationId,
            bool fullReward,
            bool placedByVisitor)
        {
            var start = _activeArtifactObject != null ? _activeArtifactObject.transform.position : _artifactCatch.position;
            var end = _artifactReturnSlot.position;
            var control = Vector3.Lerp(start, end, 0.5f) +
                          (placedByVisitor ? Vector3.up * 0.025f : _activeArtifactConfig.ReturnControlOffset);
            var duration = placedByVisitor
                ? Mathf.Max(.65f, _catalogAsset.PresentationTheme.ArtifactMotion.PlacementSettleSeconds)
                : _activeArtifactConfig.ReturnDuration;
            var elapsed = 0f;
            PlayAudio(_activeArtifactConfig.ReturnAudio);
            while (elapsed < duration && _activeArtifactObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _activeArtifactObject.transform.position = Quadratic(start, control, end, t);
                _activeArtifactObject.transform.localScale = Vector3.Lerp(
                    _activeArtifactConfig.PresentationScale,
                    Vector3.Scale(_activeArtifactConfig.PresentationScale, new Vector3(.32f, .65f, .2f)),
                    t);
                PublishCompanionCue(new CollectionCompanionCue(
                    CollectionCompanionCueKind.ReturnWithArtifact,
                    _activeArtifactObject.transform.position,
                    Mathf.Max(0.05f, duration - elapsed)));
                yield return null;
            }

            if (_activeArtifactObject != null)
                _activeArtifactObject.transform.SetPositionAndRotation(end, _artifactReturnSlot.rotation);
            var celebrationPosition = _artifactReturnSlot.position;
            PublishCompanionCue(new CollectionCompanionCue(
                CollectionCompanionCueKind.Celebrate,
                celebrationPosition,
                fullReward
                    ? _catalogAsset.PresentationTheme.RewardHoldSeconds
                    : _catalogAsset.PresentationTheme.PageSealSeconds));
            if (fullReward)
            {
                if (_confetti != null) _confetti.Play(true);
                var rewardHold = _catalogAsset.PresentationTheme.RewardHoldSeconds;
                var growthDuration = Mathf.Min(0.78f, rewardHold * 0.65f);
                yield return AnimateBookResponse(growthDuration);
                var remainingHold = rewardHold - growthDuration;
                if (remainingHold > 0f) yield return new WaitForSecondsRealtime(remainingHold);
            }
            else
            {
                yield return new WaitForSecondsRealtime(_catalogAsset.PresentationTheme.PageSealSeconds);
            }
            _motion = null;
            if (fullReward && _state?.IsComplete == true)
            {
                // Keep the final record visible until an explicit close. Acknowledgment
                // owns deduplication; celebration and audio never own collection state.
                DestroyArtifact();
                PublishCompanionCue(CollectionCompanionCue.Idle);
                yield break;
            }
            FinishPendingPresentation(presentationId);
        }

        void FinishPendingPresentation(CollectionPresentationId presentationId)
        {
            StopMotion();
            PublishCompanionCue(CollectionCompanionCue.Idle);
            DestroyArtifact();
            SetSurface(CollectionPresentationSurfaceKind.Hidden);
            _intents.RequestAcknowledgePresentation(presentationId);
            if (_browseAfterCollection && _state != null && _state.PendingPresentation == null)
            {
                _browseAfterCollection = false;
                OpenBrowseWorld();
            }
        }

        void DeferArtifact()
        {
            if (_activePresentation == null || _activePresentation.Kind != CollectionPresentationKind.ArtifactOffered) return;
            var presentationId = _activePresentation.Id;
            var returnToBrowse = _browseAfterCollection;
            StopMotion();
            PublishCompanionCue(CollectionCompanionCue.Idle);
            DestroyArtifact();
            SetSurface(CollectionPresentationSurfaceKind.Hidden);
            _intents.RequestDeferAvailableArtifactPresentation(presentationId);
            PublishArtifactTutorialStage(CollectionArtifactTutorialStage.Deferred);
            if (returnToBrowse && _state != null && _state.PendingPresentation == null)
            {
                _browseAfterCollection = false;
                OpenBrowseWorld();
            }
            else
            {
                _browseAfterCollection = false;
            }
        }

        void ContinueReward()
        {
            if (_activePresentation == null ||
                (_activePresentation.Kind != CollectionPresentationKind.Reward &&
                 _activePresentation.Kind != CollectionPresentationKind.LocalReturn))
                return;
            FinishPendingPresentation(_activePresentation.Id);
        }

        void ToggleCompendium()
        {
            if (_surfaceKind != CollectionPresentationSurfaceKind.Browse) return;
            _detailRoot.SetActive(false);
            _compendiumRoot.SetActive(!_compendiumRoot.activeSelf);
        }

        void ShowEntry(int index)
        {
            if (_surfaceKind != CollectionPresentationSurfaceKind.Browse || _state == null ||
                index < 0 || index >= _state.Artifacts.Count)
                return;
            var artifact = _state.Artifacts[index];
            if (artifact.State == CollectionArtifactStatus.Available)
            {
                _browseAfterCollection = true;
                _intents.RequestPresentAvailableArtifact(artifact.Definition.ArtifactId);
                return;
            }
            if (artifact.State != CollectionArtifactStatus.Collected) return;
            _detailTitle.text = artifact.Definition.VisitorTitle;
            _detailSummary.text = artifact.Definition.Summary;
            _detailCategory.text = artifact.Definition.Category;
            _compendiumRoot.SetActive(false);
            _detailRoot.SetActive(true);
        }

        void RenderBrowseState(CollectionProgressViewState state)
        {
            _browseCount.text = $"{state.CollectedCount} / {state.TotalCount}";
            var rewardPending = state.PendingPresentation != null &&
                                state.PendingPresentation.Kind == CollectionPresentationKind.Reward &&
                                state.PendingPresentation.ShowFullWorld;
            if (!rewardPending) ResetBookScale();

            for (var index = 0; index < _entryViews.Length; index++)
            {
                var view = _entryViews[index];
                var active = index < state.Artifacts.Count;
                view.Root.SetActive(active);
                if (!active) continue;
                var artifact = state.Artifacts[index];
                var collected = artifact.State == CollectionArtifactStatus.Collected;
                var available = artifact.State == CollectionArtifactStatus.Available;
                view.Title.text = collected ? artifact.Definition.VisitorTitle : "尚未发现";
                view.State.text = collected ? "已收藏" : available ? "待收取 · 触碰重现" : "继续探索";
                view.State.color = collected ? _collectedTint : available ? _availableTint : _unseenTint;
                view.Silhouette.color = collected ? _collectedTint : _unseenTint;
                view.DiscoveredMark.gameObject.SetActive(collected);
                view.Button.interactable = collected || available;
            }
        }

        internal static string RewardTitle(
            CollectionProgressViewState state,
            CollectionPendingPresentation pending,
            bool fullReward)
        {
            if (!fullReward) return "见闻页已入册";
            if (state?.IsComplete == true) return "这趟见闻已完整收录";
            if (state?.CollectedCount == 1 && pending?.UnlockedMilestones.Count > 0)
                return "第一张见闻页已入册";
            return "见闻册又丰富了一些";
        }

        internal static string RewardSummary(CollectionProgressViewState state)
        {
            if (state?.IsComplete == true)
                return $"从相遇同行，到共同记录。\n{state.CollectedCount} 张学习页，收好了这一路的发现。";
            return "这一页已入册，随时可以翻开回看。";
        }

        IEnumerator AnimateBookResponse(float duration)
        {
            // The book stays full-size and readable. Only a brief completion pulse is animated.
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _recordDisplayRoot.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * .025f);
                yield return null;
            }
            ResetBookScale();
        }

        void ResetBookScale()
        {
            _bookResponseRoot.localScale = Vector3.one;
            _recordDisplayRoot.localScale = Vector3.one;
        }

        void ResetArtifactOfferLayout()
        {
            if (_bookResponseRoot != null) _bookResponseRoot.localPosition = _bookBaseLocalPosition;
            if (_recordDisplayRoot != null)
                _recordDisplayRoot.localPosition = _recordBaseLocalPosition;
            _foldoutAccepted = false;
        }

        void PlaceWorld()
        {
            var theme = _catalogAsset.PresentationTheme;
            WorldSurfacePlacement.PlaceViewerFront(
                _worldRoot.transform,
                _viewer,
                theme.ViewerDistance,
                theme.VerticalOffset);
        }

        void SetSurface(CollectionPresentationSurfaceKind kind)
        {
            if (kind == CollectionPresentationSurfaceKind.Hidden)
            {
                BeginSurfaceClosing();
                return;
            }
            if (_surfaceKind == kind &&
                (_presentationPhase == CollectionPresentationPhase.Opening ||
                 _presentationPhase == CollectionPresentationPhase.Open))
                return;

            var opensFromHidden = !_worldRoot.activeSelf ||
                                  _presentationPhase == CollectionPresentationPhase.Hidden ||
                                  _presentationPhase == CollectionPresentationPhase.Closing;
            StopSurfaceTransition();
            _surfaceKind = kind;
            _worldRoot.SetActive(true);
            _artifactOfferRoot.SetActive(kind == CollectionPresentationSurfaceKind.ArtifactOffer);
            _rewardRoot.SetActive(kind == CollectionPresentationSurfaceKind.Reward &&
                                  _activePresentation != null && _activePresentation.ShowFullWorld);
            _browseRoot.SetActive(kind == CollectionPresentationSurfaceKind.Browse);
            _bookResponseRoot.gameObject.SetActive(false);
            _recordDisplayRoot.gameObject.SetActive(kind == CollectionPresentationSurfaceKind.Reward);
            SetCanvas(_artifactCanvasGroup, kind == CollectionPresentationSurfaceKind.ArtifactOffer);
            SetCanvas(_rewardCanvasGroup, kind == CollectionPresentationSurfaceKind.Reward);
            SetCanvas(_browseCanvasGroup, kind == CollectionPresentationSurfaceKind.Browse);
            SurfaceChanged?.Invoke(kind);

            if (!opensFromHidden)
            {
                _worldRoot.transform.localScale = _worldBaseScale;
                SetPresentationPhase(CollectionPresentationPhase.Open);
                return;
            }

            _surfaceTransition = StartCoroutine(AnimateSurfaceOpening(kind));
        }

        void BeginSurfaceClosing()
        {
            if (_presentationPhase == CollectionPresentationPhase.Hidden ||
                _presentationPhase == CollectionPresentationPhase.Closing)
                return;
            if (!_worldRoot.activeSelf)
            {
                ApplyHiddenSurfaceState();
                SetPresentationPhase(CollectionPresentationPhase.Hidden);
                return;
            }

            var closingGroup = ActiveCanvasGroup(_surfaceKind);
            StopSurfaceTransition();
            SetPresentationPhase(CollectionPresentationPhase.Closing);
            _surfaceTransition = StartCoroutine(AnimateSurfaceClosing(closingGroup));
        }

        IEnumerator AnimateSurfaceOpening(CollectionPresentationSurfaceKind kind)
        {
            var group = ActiveCanvasGroup(kind);
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            var startScale = _worldBaseScale * 0.94f;
            _worldRoot.transform.localScale = startScale;
            SetPresentationPhase(CollectionPresentationPhase.Opening);

            const float duration = SurfaceOpeningSeconds;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _worldRoot.transform.localScale = Vector3.Lerp(startScale, _worldBaseScale, t);
                if (group != null) group.alpha = t;
                yield return null;
            }

            _worldRoot.transform.localScale = _worldBaseScale;
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            _surfaceTransition = null;
            SetPresentationPhase(CollectionPresentationPhase.Open);
        }

        IEnumerator AnimateSurfaceClosing(CanvasGroup group)
        {
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            var startScale = _worldRoot.transform.localScale;
            var endScale = _worldBaseScale * 0.96f;
            var startAlpha = group != null ? group.alpha : 1f;
            const float duration = SurfaceClosingSeconds;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _worldRoot.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                if (group != null) group.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            ApplyHiddenSurfaceState();
            _surfaceTransition = null;
            SetPresentationPhase(CollectionPresentationPhase.Hidden);
        }

        CanvasGroup ActiveCanvasGroup(CollectionPresentationSurfaceKind kind)
        {
            switch (kind)
            {
                case CollectionPresentationSurfaceKind.ArtifactOffer:
                    return _artifactCanvasGroup;
                case CollectionPresentationSurfaceKind.Reward:
                    return _rewardCanvasGroup;
                case CollectionPresentationSurfaceKind.Browse:
                    return _browseCanvasGroup;
                default:
                    return null;
            }
        }

        void SetPresentationPhase(CollectionPresentationPhase phase)
        {
            if (_presentationPhase == phase) return;
            _presentationPhase = phase;
            PresentationPhaseChanged?.Invoke(phase);
        }

        void PublishCompanionCue(CollectionCompanionCue cue)
            => CompanionCueChanged?.Invoke(cue);

        void PublishArtifactTutorialStage(CollectionArtifactTutorialStage stage)
            => ArtifactTutorialStageChanged?.Invoke(stage);

        void HideAllSurfaces()
        {
            StopSurfaceTransition();
            ApplyHiddenSurfaceState();
            SetPresentationPhase(CollectionPresentationPhase.Hidden);
        }

        void ApplyHiddenSurfaceState()
        {
            ResetArtifactOfferLayout();
            var wasVisible = _surfaceKind != CollectionPresentationSurfaceKind.Hidden;
            if (_artifactOfferRoot != null) _artifactOfferRoot.SetActive(false);
            if (_rewardRoot != null) _rewardRoot.SetActive(false);
            if (_pageSealRoot != null) _pageSealRoot.SetActive(false);
            if (_browseRoot != null) _browseRoot.SetActive(false);
            if (_compendiumRoot != null) _compendiumRoot.SetActive(false);
            if (_detailRoot != null) _detailRoot.SetActive(false);
            if (_worldRoot != null) _worldRoot.SetActive(false);
            SetCanvas(_artifactCanvasGroup, false);
            SetCanvas(_rewardCanvasGroup, false);
            SetCanvas(_browseCanvasGroup, false);
            _surfaceKind = CollectionPresentationSurfaceKind.Hidden;
            if (_worldRoot != null) _worldRoot.transform.localScale = _worldBaseScale;
            if (wasVisible) SurfaceChanged?.Invoke(_surfaceKind);
            if (wasVisible) PublishArtifactTutorialStage(CollectionArtifactTutorialStage.Hidden);
        }

        void BindButtons()
        {
            _deferArtifactButton.onClick.AddListener(DeferArtifact);
            _continueRewardButton.onClick.AddListener(ContinueReward);
            _closeBrowseButton.onClick.AddListener(CloseBrowseMode);
            _toggleCompendiumButton.onClick.AddListener(ToggleCompendium);
            _closeDetailButton.onClick.AddListener(CloseDetail);
            _entryActions = new UnityAction[_entryViews.Length];
            for (var index = 0; index < _entryViews.Length; index++)
            {
                var captured = index;
                _entryActions[index] = () => ShowEntry(captured);
                _entryViews[index].Button.onClick.AddListener(_entryActions[index]);
            }
        }

        void UnbindButtons()
        {
            if (_deferArtifactButton != null) _deferArtifactButton.onClick.RemoveListener(DeferArtifact);
            if (_continueRewardButton != null) _continueRewardButton.onClick.RemoveListener(ContinueReward);
            if (_closeBrowseButton != null) _closeBrowseButton.onClick.RemoveListener(CloseBrowseMode);
            if (_toggleCompendiumButton != null) _toggleCompendiumButton.onClick.RemoveListener(ToggleCompendium);
            if (_closeDetailButton != null) _closeDetailButton.onClick.RemoveListener(CloseDetail);
            for (var index = 0; index < _entryActions.Length && index < _entryViews.Length; index++)
                if (_entryActions[index] != null && _entryViews[index]?.Button != null)
                    _entryViews[index].Button.onClick.RemoveListener(_entryActions[index]);
            _entryActions = Array.Empty<UnityAction>();
        }

        void CloseDetail()
        {
            _detailRoot.SetActive(false);
            if (_surfaceKind == CollectionPresentationSurfaceKind.Browse)
                _compendiumRoot.SetActive(true);
        }

        bool TryResolveArtifact(
            string artifactId,
            out CollectionArtifactState artifact,
            out CollectionArtifactRecord config)
        {
            artifact = FindArtifact(artifactId);
            config = null;
            return artifact != null && _catalogAsset.TryGetArtifactPresentation(artifactId, out config);
        }

        CollectionArtifactState FindArtifact(string artifactId)
        {
            if (_state == null || string.IsNullOrWhiteSpace(artifactId)) return null;
            for (var index = 0; index < _state.Artifacts.Count; index++)
                if (string.Equals(_state.Artifacts[index].Definition.ArtifactId, artifactId, StringComparison.Ordinal))
                    return _state.Artifacts[index];
            return null;
        }

        CollectionArtifactState FindFirst(CollectionArtifactStatus status)
        {
            if (_state == null) return null;
            for (var index = 0; index < _state.Artifacts.Count; index++)
                if (_state.Artifacts[index].State == status) return _state.Artifacts[index];
            return null;
        }

        void StopMotion()
        {
            if (_motion == null) return;
            StopCoroutine(_motion);
            _motion = null;
        }

        void StopSurfaceTransition()
        {
            if (_surfaceTransition == null) return;
            StopCoroutine(_surfaceTransition);
            _surfaceTransition = null;
        }

        void DestroyArtifact()
        {
            if (_foldout != null)
            {
                _foldout.SetActiveInput(false);
                _foldout.Closed -= HandleFoldoutClosed;
                _foldout.Changed -= HandleFoldoutChanged;
                _foldout.Touched -= HandleFoldoutTouched;
                _foldout = null;
            }
            if (_activeArtifactObject != null)
            {
                if (Application.isPlaying) Destroy(_activeArtifactObject);
                else DestroyImmediate(_activeArtifactObject);
            }
            _activeArtifactObject = null;
            _activeArtifact = null;
            _activeArtifactConfig = null;
        }

        void PlayAudio(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip);
        }

        void ValidateBindings()
        {
            if (_worldRoot == null || _artifactOfferRoot == null || _rewardRoot == null ||
                _pageSealRoot == null || _browseRoot == null || _compendiumRoot == null || _detailRoot == null ||
                _artifactSpawn == null || _artifactCatch == null || _artifactReturnSlot == null ||
                _artifactCanvas == null || _artifactCanvasGroup == null || _deferArtifactButton == null ||
                _artifactTitle == null || _artifactHint == null || _rewardCanvas == null ||
                _rewardCanvasGroup == null || _continueRewardButton == null || _rewardTitle == null ||
                _rewardCount == null || _browseCanvas == null || _browseCanvasGroup == null ||
                _closeBrowseButton == null || _toggleCompendiumButton == null || _closeDetailButton == null ||
                _browseCount == null || _detailTitle == null || _detailSummary == null || _detailCategory == null ||
                _bookResponseRoot == null || _recordDisplayRoot == null || _audioSource == null)
                throw new InvalidOperationException("CollectionWorld prefab bindings are incomplete.");
            if (_entryViews == null || _entryViews.Length == 0)
                throw new InvalidOperationException("CollectionWorld prefab requires authored compendium entry slots.");
            for (var index = 0; index < _entryViews.Length; index++)
                (_entryViews[index] ?? throw new InvalidOperationException($"Collection entry slot {index} is missing.")).Validate(index);
            if (_artifactCanvas.renderMode != RenderMode.WorldSpace ||
                _rewardCanvas.renderMode != RenderMode.WorldSpace ||
                _browseCanvas.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException("CollectionWorld canvases must be world-space.");
        }

        void RequireConfigured()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CollectionWorldFrontend));
            if (!_configured) throw new InvalidOperationException("Collection world presenter is not configured.");
        }

        static Vector3 Quadratic(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            var oneMinus = 1f - t;
            return (oneMinus * oneMinus * start) + (2f * oneMinus * t * control) + (t * t * end);
        }

        internal static float Smooth01(float value)
            => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));

        static void PrepareCanvas(Canvas canvas, Camera camera, int sortingOrder)
        {
            canvas.worldCamera = camera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        static void SetCanvas(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
