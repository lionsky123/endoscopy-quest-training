using System;
using BotanicalGardenQR.MapNavigation.Contracts;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Backend
{
    /// <summary>
    /// Owns one fixed map pose, one asynchronous GLB lease and all bounded
    /// summon/book timing. Entry choices may snapshot a fresh viewer-relative
    /// pose, while foreground gates never move or unload the fixed map.
    /// </summary>
    internal sealed class VisitorAtlasHubController : IVisitorAtlasHubController
    {
        readonly IVisitorAtlasHubPresentation _presentation;
        readonly IVisitorAtlasHubMapLoader _loader;
        readonly Action<DiagnosticEvent> _diagnostics;
        VisitorAtlasHubLifecycle _lifecycle;
        VisitorAtlasHubBookTransaction _book;
        PalmSummonLatch _palmLatch;
        readonly IMapNavigation _mapNavigation;
        float _frameWait;
        CancellationTokenSource _initializationCancellation;
        Task _initializationTask;
        IVisitorAtlasHubMapLease _mapLease;
        VisitorAtlasHubConfiguration _configuration;
        VisitorAtlasHubPalmStage _lastPalmStage = VisitorAtlasHubPalmStage.Inactive;
        uint _generation;
        bool _poseCommitted;
        bool _primePalmOnNextTick;
        bool _presentationConfigured;
        bool _configured;
        bool _disposed;
        bool _entryLayerSuppressed;

        public VisitorAtlasHubController(
            IVisitorAtlasHubPresentation presentation,
            IVisitorAtlasHubMapLoader loader,
            Action<DiagnosticEvent> diagnostics, IMapNavigation mapNavigation)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _diagnostics = diagnostics;
            _mapNavigation = mapNavigation ?? throw new ArgumentNullException(nameof(mapNavigation));
        }

        public VisitorAtlasHubPhase Phase => _lifecycle?.Phase ??
                                             (_disposed ? VisitorAtlasHubPhase.Disposed : VisitorAtlasHubPhase.Uninitialized);
        public VisitorAtlasHubBookState BookState => _book?.State ?? VisitorAtlasHubBookState.ClosedInteractive;
        public bool InteractionGateOpen => _lifecycle != null && _lifecycle.InteractionGateOpen;
        public bool IsVisible => _lifecycle != null && _lifecycle.IsVisible;

        public event Action<VisitorAtlasHubPhase> PhaseChanged;
        public event Action<VisitorAtlasHubPalmStage> PalmStageChanged;
        public event Action Summoned;
        public event Action BookSelected;
        public event Action<int> OpenCollectionRequested;
        public event Action Hidden;
        public event Action<VisitorAtlasHubFailure> Failed;

        public void Configure(Transform viewer, Transform interactionRigRoot)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorAtlasHubController));
            if (_configured) throw new InvalidOperationException("Visitor Atlas Hub is already configured.");
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));

            _presentation.Configure(viewer, interactionRigRoot);
            _presentationConfigured = true;
            if (_presentation.MapContentRoot == null)
                throw new InvalidOperationException("Visitor Atlas Hub presentation returned no map content root.");
            _configuration = _presentation.Configuration;
            StreamingAssetsUriResolver.ValidateRelativePath(_presentation.StreamingAssetsPath);
            _lifecycle = new VisitorAtlasHubLifecycle();
            _book = new VisitorAtlasHubBookTransaction(_configuration.BrowseConfirmationTimeoutSeconds);
            _palmLatch = new PalmSummonLatch(
                _configuration.PalmHoldSeconds,
                _configuration.TrackingGraceSeconds,
                _configuration.GestureReleaseSeconds);

            _presentation.BookSelected += HandleBookSelected;
            _presentation.MapSelected += HandleMapSelected;
            _presentation.HideRequested += HandleHideRequested;
            _presentation.BookOpeningCompleted += HandleBookOpeningCompleted;
            _presentation.SetEntryLayerVisible(true);
            _presentation.SetInteractionEnabled(false, false, false);
            _presentation.SetVisible(false);
            _presentation.SetMapDisplay(false, false);
            _lifecycle.BeginInitialization();
            _configured = true;
            PublishPhase();

            var cancellation = new CancellationTokenSource();
            _initializationCancellation = cancellation;
            var generation = ++_generation;
            // This task is intentionally started before any pose Tick. Both
            // prerequisites therefore progress independently from startup.
            _initializationTask = LoadMapWithBoundedRetryAsync(generation, cancellation.Token);
        }

        public void SetInteractionGate(bool open)
        {
            if (_disposed || !_configured || _lifecycle.InteractionGateOpen == open) return;
            _lifecycle.SetInteractionGate(open);
            if (open)
            {
                _primePalmOnNextTick = true;
            }
            else
            {
                _primePalmOnNextTick = false;
                PublishPalmStage(VisitorAtlasHubPalmStage.Inactive);
                if (_book.State == VisitorAtlasHubBookState.Opening)
                    CancelBookOpening(_book.Generation);
            }
            ApplyInteractionState();
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed || !_configured) return;
            ObserveInitializationTask();
            var delta = Mathf.Max(0f, unscaledDeltaSeconds);
            CaptureInitialPose(delta);
            AdvancePalm(delta);
            if (_book.Advance(delta))
            {
                _presentation.ResetBook();
                Diagnose(
                    "ATLAS_BOOK_CONFIRM_TIMEOUT",
                    "Collection Browse surface did not confirm within the bounded book transaction window.",
                    "Book");
                ApplyInteractionState();
            }
        }

        public bool TryBeginBookOpening(out int generation)
        {
            generation = 0;
            if (_disposed || !_configured || !CanInteractHub() ||
                !_book.TryBegin(out generation))
                return false;
            _presentation.BeginBookOpening(generation);
            ApplyInteractionState();
            return true;
        }

        public void ConfirmCollectionOpened(int generation)
        {
            if (_disposed || !_configured || !_book.ConfirmBrowse(generation)) return;
            _presentation.SetBookOpenLocked();
            _entryLayerSuppressed = true;
            _presentation.SetEntryLayerVisible(false);
            ApplyInteractionState();
        }

        public void CancelBookOpening(int generation = 0)
        {
            if (_disposed || !_configured || !_book.Cancel(generation)) return;
            _presentation.ResetBook();
            ApplyInteractionState();
        }

        public void NotifyCollectionClosed()
        {
            if (_disposed || !_configured) return;
            var resetBook = _book.Reset();
            if (!resetBook && !_entryLayerSuppressed) return;
            if (resetBook) _presentation.ResetBook();
            if (_entryLayerSuppressed)
            {
                _entryLayerSuppressed = false;
                if (IsVisible)
                    _presentation.RefreshEntryChoicePose();
                _presentation.SetEntryLayerVisible(true);
            }
            ApplyInteractionState();
        }

        public void RejectBookSelection()
        {
            if (_disposed || !_configured || !IsVisible) return;
            _presentation.RejectBookSelection();
        }

        public void Hide()
        {
            if (_disposed || !_configured) return;
            var before = _lifecycle.Phase;
            if (!_lifecycle.Hide()) return;
            if (_book.Reset()) _presentation.ResetBook();
            _entryLayerSuppressed = false;
            _presentation.SetEntryLayerVisible(true);
            _presentation.SetInteractionEnabled(false, false, false);
            _presentation.SetVisible(false);
            PublishPhaseIfChanged(before);
            PublishPalmStage(VisitorAtlasHubPalmStage.Inactive);
            Hidden?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            var cancellation = _initializationCancellation;
            _initializationCancellation = null;
            if (cancellation != null)
            {
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
                cancellation.Dispose();
            }
            var initializationTask = _initializationTask;
            _initializationTask = null;
            if (initializationTask != null && initializationTask.IsFaulted)
                _ = initializationTask.Exception;
            _presentation.BookSelected -= HandleBookSelected;
            _presentation.MapSelected -= HandleMapSelected;
            _presentation.HideRequested -= HandleHideRequested;
            _presentation.BookOpeningCompleted -= HandleBookOpeningCompleted;
            if (_presentationConfigured)
            {
                _presentation.SetInteractionEnabled(false, false, false);
                _presentation.SetVisible(false);
                _presentation.SetMapDisplay(false, false);
            }
            _mapLease?.Dispose();
            _mapLease = null;
            _palmLatch?.Reset();
            _lifecycle?.Dispose();
            if (_presentationConfigured) _presentation.Dispose();
            _presentationConfigured = false;
            _configured = false;
            PhaseChanged = null;
            PalmStageChanged = null;
            Summoned = null;
            BookSelected = null;
            OpenCollectionRequested = null;
            Hidden = null;
            Failed = null;
        }

        async Task LoadMapWithBoundedRetryAsync(uint generation, CancellationToken token)
        {
            Exception lastFailure = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                IVisitorAtlasHubMapLease candidate = null;
                try
                {
                    var uri = StreamingAssetsUriResolver.Resolve(
                        Application.streamingAssetsPath,
                        _presentation.StreamingAssetsPath);
                    candidate = await _loader.LoadAsync(uri, _presentation.MapContentRoot, token);
                    token.ThrowIfCancellationRequested();
                    if (_disposed || generation != _generation)
                    {
                        candidate?.Dispose();
                        return;
                    }
                    _mapLease = candidate ?? throw new InvalidOperationException("Map loader returned no lease.");
                    candidate = null;
                    DisposeCompletedInitializationCancellation();
                    var before = _lifecycle.Phase;
                    _lifecycle.MarkMapReady();
                    HandleLifecycleTransition(before);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    candidate?.Dispose();
                    return;
                }
                catch (Exception exception)
                {
                    candidate?.Dispose();
                    lastFailure = exception;
                    if (attempt == 0)
                    {
                        Diagnose(
                            "ATLAS_MAP_RETRY",
                            $"Map preparation failed once and will retry: {exception.GetType().Name}.",
                            "MapLoad");
                        try
                        {
                            if (_configuration.RetryDelaySeconds > 0f)
                                await Task.Delay(
                                    TimeSpan.FromSeconds(_configuration.RetryDelaySeconds),
                                    token);
                            else
                                await Task.Yield();
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }

            if (!_disposed && generation == _generation)
                Fail(
                    VisitorAtlasHubFailureStage.MapLoad,
                    $"Map preparation failed after one retry: {lastFailure?.GetType().Name}: {lastFailure?.Message}");
        }

        void CaptureInitialPose(float delta)
        {
            if(_poseCommitted || _lifecycle.Phase!=VisitorAtlasHubPhase.InitializingHidden)return;
            if(_mapNavigation.HasFrame)
            {
                var frame=_mapNavigation.Frame;
                _presentation.MapContentRoot.localScale=Vector3.one*frame.Scale;
                _presentation.CommitSessionRoot(new Pose(new Vector3(frame.Origin.x,frame.Origin.y+_configuration.SessionRootWorldHeight,frame.Origin.z),Quaternion.Euler(0,frame.YawDegrees,0)));
                _poseCommitted=true;var before=_lifecycle.Phase;_lifecycle.MarkPoseReady();HandleLifecycleTransition(before);return;
            }
            _frameWait+=delta;
            if(_mapNavigation.State.Phase==MapNavigationPhase.Unavailable || _frameWait>=_configuration.PoseAttemptTimeoutSeconds*2)
                Fail(VisitorAtlasHubFailureStage.InitialPose,"The shared map session frame is unavailable.");
        }

        void AdvancePalm(float delta)
        {
            if (!InteractionGateOpen || _lifecycle.Phase == VisitorAtlasHubPhase.Failed ||
                _lifecycle.Phase == VisitorAtlasHubPhase.Disposed)
                return;
            var sample = _presentation.SamplePalmCandidate();
            PublishPalmStage(IsVisible ? VisitorAtlasHubPalmStage.Summoned : sample.Stage);
            if (_primePalmOnNextTick)
            {
                _primePalmOnNextTick = false;
                _palmLatch.Prime(sample.IsCandidate);
                return;
            }
            if (!_palmLatch.Advance(sample, delta)) return;
            var before = _lifecycle.Phase;
            if (!_lifecycle.RequestSummon()) return;
            HandleLifecycleTransition(before);
        }

        void HandleLifecycleTransition(VisitorAtlasHubPhase before)
        {
            PublishPhaseIfChanged(before);
            if (_lifecycle.Phase != VisitorAtlasHubPhase.Revealing) return;
            _entryLayerSuppressed = false;
            _presentation.SetEntryLayerVisible(true);
            _presentation.RefreshEntryChoicePose();
            _presentation.SetVisible(true);
            _presentation.PlayReveal();
            var revealing = _lifecycle.Phase;
            _lifecycle.CompleteReveal();
            PublishPhaseIfChanged(revealing);
            ApplyInteractionState();
            PublishPalmStage(VisitorAtlasHubPalmStage.Summoned);
            Summoned?.Invoke();
        }

        void HandleBookSelected()
        {
            if (!CanInteractHub() ||
                _book.State != VisitorAtlasHubBookState.ClosedInteractive) return;
            BookSelected?.Invoke();
        }

        void HandleMapSelected()
        {
            if (!CanInteractHub() || _entryLayerSuppressed || _book.State != VisitorAtlasHubBookState.ClosedInteractive) return;
            MapRequested = !MapRequested;
            ApplyMapDisplay();
            Hide();
        }

        void HandleBookOpeningCompleted(int generation)
        {
            if (_disposed || !_configured || !_book.CompleteAnimation(generation)) return;
            ApplyInteractionState();
            OpenCollectionRequested?.Invoke(generation);
        }

        void HandleHideRequested()
        {
            if (CanInteractHub()) Hide();
        }

        bool CanInteractHub() => InteractionGateOpen && _lifecycle.Phase == VisitorAtlasHubPhase.Visible;

        void ApplyInteractionState()
        {
            if (_disposed || !_configured) return;
            var canInteract = CanInteractHub() && !_entryLayerSuppressed;
            var choices = canInteract && _book.State == VisitorAtlasHubBookState.ClosedInteractive;
            _presentation.SetInteractionEnabled(choices, choices, canInteract);
        }

        public bool MapRequested { get; private set; }
        public bool IsMapVisible => MapRequested && !_mapSuppressed && _poseCommitted &&
            Phase != VisitorAtlasHubPhase.Failed && Phase != VisitorAtlasHubPhase.Disposed;
        bool _mapSuppressed;
        public void SetMapSuppressed(bool suppressed)
        {
            if (_disposed || _mapSuppressed == suppressed) return;
            _mapSuppressed = suppressed;
            ApplyMapDisplay();
        }
        void ApplyMapDisplay()
        {
            if (_configured) _presentation.SetMapDisplay(MapRequested, IsMapVisible);
        }

        void Fail(VisitorAtlasHubFailureStage stage, string diagnostic)
        {
            if (_disposed || !_configured || _lifecycle.Phase == VisitorAtlasHubPhase.Failed) return;
            ++_generation;
            var cancellation = _initializationCancellation;
            _initializationCancellation = null;
            if (cancellation != null)
            {
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
                cancellation.Dispose();
            }
            var before = _lifecycle.Phase;
            _lifecycle.Fail();
            ApplyMapDisplay();
            _entryLayerSuppressed = false;
            _presentation.SetEntryLayerVisible(true);
            _presentation.SetInteractionEnabled(false, false, false);
            _presentation.SetVisible(false);
            _mapLease?.Dispose();
            _mapLease = null;
            PublishPhaseIfChanged(before);
            var failure = new VisitorAtlasHubFailure(stage, diagnostic);
            Failed?.Invoke(failure);
            Diagnose("ATLAS_INITIALIZATION_FAILED", diagnostic, stage.ToString());
        }

        void PublishPhaseIfChanged(VisitorAtlasHubPhase previous)
        {
            if (previous != _lifecycle.Phase) PublishPhase();
        }

        void PublishPhase() => PhaseChanged?.Invoke(_lifecycle.Phase);

        void PublishPalmStage(VisitorAtlasHubPalmStage stage)
        {
            if (_lastPalmStage == stage) return;
            _lastPalmStage = stage;
            PalmStageChanged?.Invoke(stage);
        }

        void Diagnose(string code, string message, string stage)
        {
            _diagnostics?.Invoke(new DiagnosticEvent(
                code,
                message,
                "VisitorAtlasHub",
                stage,
                DateTimeOffset.UtcNow,
                assetPath: _presentation.StreamingAssetsPath));
            if (code == "ATLAS_INITIALIZATION_FAILED")
                Debug.LogWarning($"[VisitorAtlasHub] {message}");
        }

        void DisposeCompletedInitializationCancellation()
        {
            var cancellation = _initializationCancellation;
            _initializationCancellation = null;
            cancellation?.Dispose();
        }

        void ObserveInitializationTask()
        {
            var task = _initializationTask;
            if (task == null || !task.IsCompleted) return;
            _initializationTask = null;
            if (task.IsCanceled || task.Exception == null) return;
            var exception = task.Exception.GetBaseException();
            Fail(
                VisitorAtlasHubFailureStage.MapLoad,
                $"Map initialization task faulted: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
