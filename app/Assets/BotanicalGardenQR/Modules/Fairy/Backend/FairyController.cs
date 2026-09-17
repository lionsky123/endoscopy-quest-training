using System;
using System.Collections;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Fairy.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    internal sealed class FairyController : MonoBehaviour, IFairyController, IFairyMotion, IFairyRecovery
    {
        const float ArrivalPreludeSeconds = .8f;
        const float ArrivalWorldEstablishSeconds = 2.5f;
        const float ArrivalSettleSeconds = 3.8f;
        const float ArrivalCrossingSeconds = 5f;
        const float ArrivalWalkDistance = 1.5f;
        const float ArrivalDoorOffset = .75f;
        const float OfficialAutomaticWindowOpenSeconds = 3.8f;
        const float FirstIdleLocomotionFeedbackDelaySeconds = 1.25f;
        const float SpeechPreparationGraceSeconds = 2f;
        const float SpeechStartGraceSeconds = 0.5f;
        const float SpeechCompletionGraceSeconds = 1f;
        const float SpeechGainBoostDb = 8f;
        const int SpatialGainParameter = (int)MetaXRAudioSource.NativeParameterIndex.P_GAIN;

        StateChannel<FairyState> _states;
        Transform _viewer;
        Func<Vector3, Vector3?> _arrivalHandPosition;
        FairyArrivalResonance _arrivalResonance;
        FairyArrivalSoundEvents _arrivalSounds;
        float _arrivalRealElapsed;
        internal event Action<float, string, AudioClip, Vector3, float> ArrivalSoundPlayed;
        AudioListener _listener;
        Transform _groundReference;
        GameObject _instance;
        FairyOrbitDriver _orbit;
        FairyInteractionController _interaction;
        FairyGazeInteractionDriver _gazeInteraction;
        FairyAnimationDriver _animation;
        bool _celebrationPending;
        FairyArrivalVisualState _arrivalVisualState;
        FairyArrivalPassage _arrivalPassage;
        Vector3? _invitationOrigin;
        AudioSource _arrivalAudioSource;
        GameObject _companionCueEffectInstance;
        ParticleSystem _companionCueParticles;
        AudioClip _coachAttentionClip;
        AudioClip _artifactWaitClip;
        AudioClip _artifactReturnClip;
        AudioClip _celebrateClip;
        IReadOnlyList<AudioClip> _idleLocomotionClips;
        Coroutine _companionFeedbackRoutine;
        Coroutine _idleLocomotionRoutine;
        Coroutine _speechRoutine;
        Coroutine _arrivalRoutine;
        float _arrivalElapsed;
        Vector3 _arrivalLandingPosition, _arrivalStepDirection;
        bool _arrivalInProgress, _arrivalWindowShown, _arrivalCharacterShown, _arrivalChargeStarted, _arrivalRevealed, _arrivalLanded;
        bool _arrivalResponseStarted;
        bool _arrivalGreetingStarted;
        GameObject _arrivalEffectInstance;
        GameObject _arrivalEffectPrefab;
        GameObject _arrivalMaskInstance;
        GameObject _arrivalMaskPrefab;
        FairyArrivalFlashlightAperture _arrivalFlashlightAperture;
        FairyArrivalOtherWorldWindow _arrivalOtherWorldWindow;
        Action<DiagnosticEvent> _diagnostics;
        SessionToken _session;
        FairyBehavior _behavior;
        FairyPhase _phase = FairyPhase.Closed;
        float _arrivalEffectDelaySeconds;
        float _arrivalRevealDelaySeconds;
        float _coachAttentionVolume;
        float _artifactWaitVolume;
        float _artifactReturnVolume;
        float _celebrateVolume;
        float _idleLocomotionVolume;
        float _coachAttentionEffectSeconds;
        float _artifactWaitEffectSeconds;
        float _artifactReturnEffectSeconds;
        float _celebrateEffectSeconds;
        float _idleLocomotionMinIntervalSeconds;
        float _idleLocomotionMaxIntervalSeconds;
        float _idleLocomotionEffectSeconds;
        int _coachAttentionParticleBurst;
        int _artifactWaitParticleBurst;
        int _artifactReturnParticleBurst;
        int _celebrateParticleBurst;
        int _idleLocomotionParticleBurst;
        int _companionFeedbackGeneration;
        int _speechGeneration;
        FairyCompanionCueKind _lastCompanionCueKind = FairyCompanionCueKind.Idle;
        FairyDialogueReaction _dialogueReaction;
        bool _dialogueReactionPending;
        float _companionCueEndsAt;
        bool _companionAudioPlaying;
        bool _speechPlaying;
        AudioClip _speechClip;
        Guid _speechRequestId;
        float _speechOutputPeak;
        int _speechPlayedSamples;
        Action<FairyResult> _speechCompletion;
        bool _speechBlocked;
        bool _ambientAudioSuppressed;
        bool _walking;
        bool _arrivalCompleted;
        long _version;

        internal GameObject CompanionCueEffectInstance => _companionCueEffectInstance;
        internal int CompanionFeedbackPlayCount { get; private set; }
        internal AudioClip LastCompanionFeedbackClip { get; private set; }
        internal int IdleLocomotionFeedbackPlayCount { get; private set; }
        internal AudioClip LastIdleLocomotionFeedbackClip { get; private set; }
        internal int SpeechPlayCount { get; private set; }
        internal AudioClip LastSpeechClip { get; private set; }

        internal void Initialize(
            Transform viewer,
            Transform groundReference,
            OVRPassthroughLayer arrivalPassthroughLayer,
            Light arrivalEnvironmentLight,
            Action<DiagnosticEvent> diagnostics = null,
            Func<Vector3, Vector3?> arrivalHandPosition = null)
        {
            if (_viewer != null) throw new InvalidOperationException("Fairy controller is already initialized.");
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _arrivalHandPosition = arrivalHandPosition;
            _listener = viewer.GetComponent<AudioListener>();
            _groundReference = groundReference != null
                ? groundReference
                : throw new ArgumentNullException(nameof(groundReference));
            _arrivalVisualState = new FairyArrivalVisualState(
                arrivalPassthroughLayer,
                arrivalEnvironmentLight);
            _diagnostics = diagnostics;
            _states = StateChannel<FairyState>.ForCurrentThread(
                new FairyState(default, 0, FairyPhase.Closed),
                state => state.Version);
        }

        public FairyResult Open(SessionToken session, FairyDefinition definition)
        {
            if (_viewer == null || _arrivalVisualState == null)
                return FairyResult.Failure(FairyFailureCode.LoadFailed, "fairy.not_initialized");
            if (!session.IsValid)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.session.invalid");
            if (definition == null || definition.Prefab == null)
                return FairyResult.Failure(FairyFailureCode.InvalidDefinition, "fairy.definition.invalid");
            if (_instance != null)
                return FairyResult.Failure(FairyFailureCode.InvalidIntent, "fairy.already_open");

            _session = session;
            _behavior = definition.Behavior;
            _arrivalEffectPrefab = definition.ArrivalEffectPrefab;
            _arrivalMaskPrefab = definition.ArrivalMaskPrefab;
            var companionFeedback = definition.CompanionFeedback;
            _coachAttentionClip = companionFeedback.CoachAttentionClip;
            _artifactWaitClip = companionFeedback.ArtifactWaitClip;
            _artifactReturnClip = companionFeedback.ArtifactReturnClip;
            _celebrateClip = companionFeedback.CelebrateClip;
            _idleLocomotionClips = companionFeedback.IdleLocomotionClips;
            _coachAttentionVolume = companionFeedback.CoachAttentionVolume;
            _artifactWaitVolume = companionFeedback.ArtifactWaitVolume;
            _artifactReturnVolume = companionFeedback.ArtifactReturnVolume;
            _celebrateVolume = companionFeedback.CelebrateVolume;
            _idleLocomotionVolume = companionFeedback.IdleLocomotionVolume;
            _coachAttentionEffectSeconds = companionFeedback.CoachAttentionEffectSeconds;
            _artifactWaitEffectSeconds = companionFeedback.ArtifactWaitEffectSeconds;
            _artifactReturnEffectSeconds = companionFeedback.ArtifactReturnEffectSeconds;
            _celebrateEffectSeconds = companionFeedback.CelebrateEffectSeconds;
            _idleLocomotionMinIntervalSeconds = companionFeedback.IdleLocomotionMinIntervalSeconds;
            _idleLocomotionMaxIntervalSeconds = companionFeedback.IdleLocomotionMaxIntervalSeconds;
            _idleLocomotionEffectSeconds = companionFeedback.IdleLocomotionEffectSeconds;
            _coachAttentionParticleBurst = companionFeedback.CoachAttentionParticleBurst;
            _artifactWaitParticleBurst = companionFeedback.ArtifactWaitParticleBurst;
            _artifactReturnParticleBurst = companionFeedback.ArtifactReturnParticleBurst;
            _celebrateParticleBurst = companionFeedback.CelebrateParticleBurst;
            _idleLocomotionParticleBurst = companionFeedback.IdleLocomotionParticleBurst;
            _arrivalEffectDelaySeconds = definition.ArrivalEffectDelaySeconds;
            _arrivalRevealDelaySeconds = definition.ArrivalRevealDelaySeconds;
            _lastCompanionCueKind = FairyCompanionCueKind.Idle;
            _companionCueEndsAt = 0f;
            _companionAudioPlaying = false;
            _speechRoutine = null;
            _speechPlaying = false;
            _speechClip = null;
            _speechRequestId = Guid.Empty;
            _speechCompletion = null;
            _speechBlocked = false;
            _ambientAudioSuppressed = false;
            _walking = false;
            CompanionFeedbackPlayCount = 0;
            LastCompanionFeedbackClip = null;
            IdleLocomotionFeedbackPlayCount = 0;
            LastIdleLocomotionFeedbackClip = null;
            SpeechPlayCount = 0;
            LastSpeechClip = null;
            _arrivalCompleted = false;
            Publish(FairyPhase.Loading);
            try
            {
                _instance = Instantiate(definition.Prefab, transform, false);
                _instance.name = "FairyRuntimeInstance";
                _instance.transform.localScale = definition.Prefab.transform.localScale * definition.Scale;

                _animation = _instance.AddComponent<FairyAnimationDriver>();
                _animation.Initialize();
                _orbit = _instance.AddComponent<FairyOrbitDriver>();
                _orbit.Initialize(_viewer, _groundReference, definition.Behavior, HandleMovementChanged, speed => _animation?.SetTravelSpeed(speed));
                _interaction = _instance.AddComponent<FairyInteractionController>();
                _interaction.Initialize(HandleInteraction);
                _gazeInteraction = _instance.AddComponent<FairyGazeInteractionDriver>();
                _gazeInteraction.Initialize(_viewer, _interaction);
                _arrivalAudioSource = _instance.AddComponent<AudioSource>();
                _arrivalAudioSource.playOnAwake = false;
                _arrivalAudioSource.loop = false;
                _arrivalAudioSource.volume = 1f;
                _arrivalAudioSource.pitch = 1f;
                _arrivalAudioSource.spatialBlend = 1f;
                _arrivalAudioSource.spatialize = true;
                _arrivalAudioSource.dopplerLevel = 0f;
                _arrivalAudioSource.rolloffMode = AudioRolloffMode.Linear;
                _arrivalAudioSource.minDistance = 0.5f;
                _arrivalAudioSource.maxDistance = 8f;
                CreateCompanionFeedback(companionFeedback);
                if (!_animation.HasPlayableAnimation)
                    Diagnose(
                        "FAIRY_ANIMATION_UNAVAILABLE",
                        "Fairy prefab has no playable Animation or Animator controller.",
                        "open");
                Publish(FairyPhase.Active);
                return FairyResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("FAIRY_OPEN_FAILED", exception.Message, "open");
                try
                {
                    ReleaseInstance();
                }
                catch (Exception releaseException)
                {
                    Debug.LogException(releaseException, this);
                    Diagnose("FAIRY_RELEASE_FAILED", releaseException.Message, "release_after_open_failure");
                }
                Publish(FairyPhase.Failed, new UserFault("精灵向导加载失败"));
                return FairyResult.Failure(FairyFailureCode.LoadFailed, "fairy.open.failed");
            }
        }

        public bool TryRecallMotion(Vector3 position)
            => _instance != null && _instance.activeInHierarchy && _phase == FairyPhase.Active && _orbit != null && _orbit.TryRecall(position);
        public bool TryGetMotionPosition(out Vector3 position)
        {position=_instance!=null?_instance.transform.position:default;return _instance!=null && _phase==FairyPhase.Active;}
        public bool ApplyMotion(long requestId,Vector3 position,Vector3 forward,bool moving,float entryRadius,float speed)
            => _instance!=null && _instance.activeInHierarchy && _phase==FairyPhase.Active && _orbit!=null && _orbit.ApplyMotion(requestId,position,forward,moving,entryRadius,speed);
        public void HoldMotion(long requestId){if(_orbit!=null)_orbit.HoldMotion(requestId);}
        public void ReleaseMotion(long requestId){if(_orbit!=null)_orbit.ReleaseMotion(requestId);}

        public FairyResult Dispatch(SessionToken session, FairyIntent intent, UnityEngine.Vector3? arrivalOrigin = null)
        {
            if (_instance == null)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.not_open");
            if (session != _session)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.stale_session");

            switch (intent)
            {
                case FairyIntent.Show:
                    if (arrivalOrigin.HasValue)
                    {
                        var p = arrivalOrigin.Value;
                        if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                            return FairyResult.Failure(FairyFailureCode.InvalidIntent, "fairy.arrival.invalid_origin");
                    }
                    _invitationOrigin = arrivalOrigin;
                    return ShowWithFirstArrival();
                case FairyIntent.Hide:
                    CancelArrival();
                    _animation?.Stop();
                    _instance.SetActive(false);
                    Publish(FairyPhase.Hidden);
                    return FairyResult.Success();
                case FairyIntent.Interact:
                    _interaction.Interact();
                    return FairyResult.Success();
                default:
                    return FairyResult.Failure(FairyFailureCode.InvalidIntent, "fairy.intent.unknown");
            }
        }

        public FairyResult PresentCompanionCue(SessionToken session, FairyCompanionCue cue)
        {
            if (_instance == null || _orbit == null)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.not_open");
            if (session != _session)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.stale_session");

            _orbit.PresentCue(cue);
            _companionCueEndsAt = cue.Kind == FairyCompanionCueKind.Idle
                ? 0f
                : cue.DurationSeconds > 0f
                    ? Time.unscaledTime + cue.DurationSeconds
                    : float.PositiveInfinity;
            if (_phase != FairyPhase.Active || !_instance.activeInHierarchy)
                return FairyResult.Success();
            var reactionChanged = cue.Kind == FairyCompanionCueKind.DialogueFocus &&
                (cue.Kind != _lastCompanionCueKind || cue.DialogueReaction != _dialogueReaction);
            if (reactionChanged)
            {
                _animation?.EndWonder();
                _dialogueReaction = cue.DialogueReaction;
                _dialogueReactionPending = true;
            }
            if (cue.Kind == _lastCompanionCueKind)
            {
                PlayPendingDialogueReaction();
                return FairyResult.Success();
            }

            _lastCompanionCueKind = cue.Kind;
            if (cue.Kind != FairyCompanionCueKind.Celebrate) _celebrationPending = false;
            if (cue.Kind != FairyCompanionCueKind.DialogueFocus)
            {
                _dialogueReactionPending = false;
                _animation?.EndWonder();
            }
            StopCompanionFeedback(true);
            PlayPendingDialogueReaction();

            if (cue.Kind == FairyCompanionCueKind.Celebrate)
            {
                _celebrationPending = true;
                PlayPendingCelebration();
                PlayCompanionFeedback(
                    _celebrateClip,
                    _celebrateVolume,
                    _celebrateEffectSeconds,
                    _celebrateParticleBurst);
            }
            else if (cue.Kind == FairyCompanionCueKind.CoachAttention)
            {
                _animation?.PlayCoachAttention();
                PlayCompanionFeedback(
                    _coachAttentionClip,
                    _coachAttentionVolume,
                    _coachAttentionEffectSeconds,
                    _coachAttentionParticleBurst);
            }
            else if (cue.Kind == FairyCompanionCueKind.ArtifactWait)
            {
                PlayCompanionFeedback(
                    _artifactWaitClip,
                    _artifactWaitVolume,
                    _artifactWaitEffectSeconds,
                    _artifactWaitParticleBurst);
            }
            else if (cue.Kind == FairyCompanionCueKind.ArtifactReturn)
            {
                PlayCompanionFeedback(
                    _artifactReturnClip,
                    _artifactReturnVolume,
                    _artifactReturnEffectSeconds,
                    _artifactReturnParticleBurst);
            }
            return FairyResult.Success();
        }

        public FairyResult Speak(SessionToken session, FairySpeech speech, Action<FairyResult> completion = null)
        {
            if (_instance == null || _arrivalAudioSource == null)
                return RejectSpeech(speech, FairyFailureCode.NotOpen, "not_open");
            if (session != _session)
                return RejectSpeech(speech, FairyFailureCode.StaleSession, "stale_session");
            if (speech.Clip == null || speech.Clip.length <= 0f)
                return RejectSpeech(speech, FairyFailureCode.InvalidSpeech, "invalid_clip");
            if (_speechBlocked || !_instance.activeInHierarchy)
                return RejectSpeech(speech, FairyFailureCode.NotVisible, "hidden");
            if (!Application.isPlaying || !isActiveAndEnabled)
                return RejectSpeech(speech, FairyFailureCode.AudioUnavailable, "runtime_inactive");

            var generation = _speechGeneration + 1;
            StopSpeech("replaced_by_speech");
            // A completion callback may have synchronously started a newer request.
            if (generation != _speechGeneration)
                return RejectSpeech(speech, FairyFailureCode.SpeechCancelled, "superseded_during_completion");
            StopCompanionFeedback(false);
            _speechClip = speech.Clip;
            _speechRequestId = speech.RequestId;
            _speechOutputPeak = 0f;
            _speechPlayedSamples = 0;
            _speechCompletion = completion;
            DiagnoseSpeech("FAIRY_SPEECH_REQUESTED", speech.Clip, "requested", generation);
            _speechRoutine = StartCoroutine(GuardSpeechPlayback(speech, generation));
            return FairyResult.Success();
        }

        public FairyResult CancelSpeech(SessionToken session, Guid requestId)
        {
            if (_instance == null) return FairyResult.Success();
            if (session != _session)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.stale_session");
            if (requestId != Guid.Empty && requestId == _speechRequestId)
                StopSpeech("cancelled_by_owner");
            return FairyResult.Success();
        }

        public FairyResult SetAmbientAudioSuppressed(SessionToken session, bool suppressed)
        {
            if (_instance == null)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.not_open");
            if (session != _session)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.stale_session");

            _ambientAudioSuppressed = suppressed;
            if (suppressed)
                StopCompanionFeedback(true);
            else
                EnsureIdleLocomotionRoutine();
            return FairyResult.Success();
        }

        public FairyResult Close(SessionToken session)
        {
            if (_instance == null) return FairyResult.Success();
            if (session != _session)
                return FairyResult.Failure(FairyFailureCode.StaleSession, "fairy.stale_session");

            try
            {
                ReleaseInstance();
                Publish(FairyPhase.Closed);
                Diagnose("FAIRY_CLOSED", "Fairy closed normally.", "close");
                return FairyResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("FAIRY_RELEASE_FAILED", exception.Message, "close");
                Publish(FairyPhase.Failed, new UserFault("精灵向导关闭失败"));
                return FairyResult.Failure(FairyFailureCode.LoadFailed, "fairy.close.failed");
            }
        }

        public IDisposable Observe(IFairyStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_states == null) throw new InvalidOperationException("Fairy controller is not initialized.");
            return _states.Observe(sink.Publish);
        }

        void HandleInteraction(bool enlarged)
        {
            if (_orbit != null) _orbit.SetEnlarged(enlarged);
            _animation?.PlayInteraction();
            Publish(_phase);
        }

        void HandleMovementChanged(bool walking)
        {
            _walking = walking;
            _animation?.SetMoving(walking);
            if (!walking) { PlayPendingDialogueReaction(); PlayPendingCelebration(); }
        }

        void PlayPendingDialogueReaction()
        {
            if (!_dialogueReactionPending || _walking || _orbit == null || !_orbit.IsAtCueTarget || _phase != FairyPhase.Active ||
                _lastCompanionCueKind != FairyCompanionCueKind.DialogueFocus) return;
            _dialogueReactionPending = false;
            if (_dialogueReaction == FairyDialogueReaction.Welcome) _animation?.PlayCoachAttention();
            else if (_dialogueReaction == FairyDialogueReaction.Wonder) _animation?.PlayWonder();
            if (Application.isPlaying && !_ambientAudioSuppressed && _speechClip == null &&
                _dialogueReaction != FairyDialogueReaction.Listening && _idleLocomotionClips != null && _idleLocomotionClips.Count > 0)
            {
                var index = _dialogueReaction == FairyDialogueReaction.Wonder ? Mathf.Min(1, _idleLocomotionClips.Count - 1) : 0;
                PlayFeedbackAudioAndParticles(_idleLocomotionClips[index], _idleLocomotionVolume * .6f, .25f, 0);
            }
        }

        void PlayPendingCelebration()
        {
            if (!_celebrationPending || _walking || _orbit == null || !_orbit.IsAtCueTarget ||
                _phase != FairyPhase.Active || _lastCompanionCueKind != FairyCompanionCueKind.Celebrate) return;
            _celebrationPending = false;
            if (Time.unscaledTime < _companionCueEndsAt) _animation?.PlayCelebrate();
        }

        FairyResult ShowWithFirstArrival()
        {
            _speechBlocked = false;
            if (_arrivalRoutine != null || _arrivalInProgress)
                return FairyResult.Success();
            if (_arrivalCompleted)
            {
                RevealInstance();
                return FairyResult.Success();
            }

            try
            {
                _orbit.SnapToTarget();
                _arrivalLandingPosition = _instance.transform.position;
                _arrivalStepDirection = _arrivalLandingPosition - _viewer.position;
                _arrivalStepDirection.y = 0;
                _arrivalStepDirection = _arrivalStepDirection.sqrMagnitude > .001f ? _arrivalStepDirection.normalized : Vector3.forward;
                _instance.transform.position = _arrivalLandingPosition + _arrivalStepDirection * ArrivalWalkDistance;
                _orbit.enabled = false;
                _gazeInteraction.enabled = false;
                _instance.SetActive(true);
                // Animator.Rebind restores renderer defaults. Do it before the window captures and hides the guide.
                _animation?.Play();
                _animation?.SetMoving(false);
                _arrivalVisualState.Capture();
                CreateArrivalMask();
                _arrivalPassage = new FairyArrivalPassage(_viewer);
                _arrivalElapsed = _arrivalRealElapsed = 0f;
                _arrivalInProgress = true;
                _arrivalWindowShown = _arrivalCharacterShown = _arrivalChargeStarted = _arrivalRevealed = _arrivalLanded = _arrivalResponseStarted = false;
                _arrivalGreetingStarted = false;
                Publish(FairyPhase.Arriving);

                if (_arrivalEffectDelaySeconds <= 0f && _arrivalRevealDelaySeconds <= 0f)
                    CompleteImmediateArrival();
                else
                    _arrivalRoutine = StartCoroutine(PlayFirstArrivalSequence());
                return _phase == FairyPhase.Failed
                    ? FairyResult.Failure(FairyFailureCode.LoadFailed, "fairy.arrival.failed")
                    : FairyResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("FAIRY_ARRIVAL_EFFECT_FAILED", exception.Message, "arrival");
                return FailArrival();
            }
        }

        IEnumerator PlayFirstArrivalSequence()
        {
            // Do not count the loading/input frame that happened before the invitation was accepted.
            yield return null;
            while (_arrivalInProgress)
            {
                AdvanceFirstArrival(Time.unscaledDeltaTime);
                yield return null;
            }
        }

        // The runtime coroutine and deterministic presentation tests use the same sequence.
        internal void AdvanceFirstArrival(float deltaSeconds)
        {
            if (!_arrivalInProgress || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
                return;
            _arrivalRealElapsed += deltaSeconds;
            var previousElapsed = _arrivalElapsed;
            _arrivalElapsed += deltaSeconds;
            var interactive = _arrivalEffectDelaySeconds > 0 || _arrivalRevealDelaySeconds > 0;
            if (interactive && _arrivalResonance != null && !_arrivalResonance.Completed)
                _arrivalElapsed = Mathf.Min(_arrivalElapsed, 3.35f);
            _arrivalResonance?.Tick(deltaSeconds, interactive && _arrivalElapsed >= 2.7f);
            _arrivalSounds?.Tick(deltaSeconds);
            try
            {
                var crossingAt = ArrivalPreludeSeconds + OfficialAutomaticWindowOpenSeconds +
                    ArrivalWorldEstablishSeconds + _arrivalEffectDelaySeconds;
                var landedAt = crossingAt + ArrivalCrossingSeconds;
                var doorCenter = _arrivalLandingPosition + _arrivalStepDirection * ArrivalDoorOffset + Vector3.up * .8f;
                var origin = _invitationOrigin ?? (_viewer.position + _viewer.forward * .5f - Vector3.up * .15f);
                var opening = EvaluateArrivalOpening(_arrivalElapsed);
                var collision = EvaluateArrivalCollision(_arrivalElapsed);
                if (_arrivalElapsed == 3.35f && _arrivalResonance != null && !_arrivalResonance.Completed)
                    collision = .05f + .04f * Mathf.Sin(_arrivalRealElapsed * 3f);
                if (previousElapsed < .35f && _arrivalElapsed >= .35f) _arrivalSounds?.Play(FairyArrivalSound.Wake, "wake");
                if (previousElapsed < .8f && _arrivalElapsed >= .8f) _arrivalSounds?.Play(FairyArrivalSound.Crack, "crack-first", volume: .45f);
                if (previousElapsed < 1.25f && _arrivalElapsed >= 1.25f) _arrivalSounds?.Play(FairyArrivalSound.Recoil, "recoil-first", volume: .38f);
                if (previousElapsed < 2.15f && _arrivalElapsed >= 2.15f) _arrivalSounds?.Play(FairyArrivalSound.Crack, "crack-second", volume: .65f);
                if (previousElapsed < 2.7f && _arrivalElapsed >= 2.7f) _arrivalSounds?.Play(FairyArrivalSound.Recoil, "recoil-second", volume: .45f);
                if (previousElapsed <= 3.35f && _arrivalElapsed > 3.35f)
                {
                    _arrivalSounds?.Play(FairyArrivalSound.Connect, "portal-connected", volume: .8f);
                    if (interactive) PlayArrivalShockwave();
                }
                _arrivalFlashlightAperture?.SetInvitationWindow(origin, doorCenter, 1);
                _arrivalFlashlightAperture?.SetOpeningProgress(opening);
                _arrivalFlashlightAperture?.SetFlickerTime(Mathf.Clamp01((_arrivalElapsed - .65f) / .15f));
                if (_arrivalOtherWorldWindow != null)
                {
                    _arrivalOtherWorldWindow.SetRitualTime(_arrivalElapsed);
                    _arrivalOtherWorldWindow.SetStrength(_arrivalFlashlightAperture.CurrentStrength);
                    _arrivalOtherWorldWindow.SetAudioDuck(_arrivalSounds != null && _arrivalSounds.DuckRemaining > 0);
                    _arrivalOtherWorldWindow.SetAnticipation(opening);
                }
                var worldShift = (.22f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((_arrivalElapsed - .8f) / .6f)) +
                    .78f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((_arrivalElapsed - 3.35f) / .5f))) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((_arrivalElapsed - landedAt) / 3f)));
                worldShift = Mathf.Max(worldShift, _arrivalResonance != null && _arrivalElapsed < 4f ? _arrivalResonance.WorldShift : 0);
                _arrivalPassage?.Tick(_arrivalElapsed, landedAt, collision, worldShift, _arrivalResonance != null ? _arrivalResonance.Flash : 0);
                _arrivalVisualState.SetWorldShift(worldShift);
                _arrivalVisualState.SetPressure(Mathf.Clamp01(opening * .45f + collision * .55f) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((_arrivalElapsed - landedAt) / 2f))));
                if (!_arrivalChargeStarted && _arrivalElapsed >= .35f)
                {
                    _arrivalChargeStarted = true;
                    // Individual crack/recoil events are emitted at their actual transitions.
                }
                if (opening < 1) return;
                // The connection accent ends with the opening, before the character's own entrance cue.
                if (_arrivalEffectInstance != null) ReleaseArrivalEffect();
                _arrivalWindowShown = true;
                // Let the changed room and the empty other world register before revealing its inhabitant.
                if (_arrivalElapsed < ArrivalPreludeSeconds + OfficialAutomaticWindowOpenSeconds + .9f) return;
                if (!_arrivalCharacterShown)
                {
                    _arrivalCharacterShown = true;
                    _arrivalSounds?.Play(FairyArrivalSound.Appear, "character-appeared", _instance.transform.position, .6f);
                    _arrivalOtherWorldWindow.ShowCharacterInWindow();
                    _animation?.PlayWonder();
                    if (_idleLocomotionClips != null && _idleLocomotionClips.Count > 1)
                        PlayArrivalClip(_idleLocomotionClips[1], .5f);
                }
                var approachElapsed = _arrivalElapsed - crossingAt;
                if (!_arrivalGreetingStarted && _arrivalElapsed >= ArrivalPreludeSeconds + OfficialAutomaticWindowOpenSeconds + ArrivalWorldEstablishSeconds)
                {
                    _arrivalGreetingStarted = true;
                    _animation?.EndWonder();
                    _animation?.PlayCoachAttention();
                }
                if (approachElapsed < 0) return;
                if (!_arrivalRevealed)
                {
                    CompleteFirstArrivalReveal();
                    _arrivalRevealed = true;
                }
                var crossing = EvaluateArrivalTravel(approachElapsed);
                _instance.transform.position = _arrivalLandingPosition + _arrivalStepDirection * (ArrivalWalkDistance * (1 - crossing));
                _instance.transform.rotation = Quaternion.LookRotation(-_arrivalStepDirection);
                _animation?.SetMoving(crossing < 1);
                _animation?.SetTravelSpeed(ArrivalWalkDistance / ArrivalCrossingSeconds);
                if (approachElapsed >= 2.5f) _arrivalSounds?.Play(FairyArrivalSound.Cross, "body-crossed", _instance.transform.position, .6f);
                if (approachElapsed >= 4.45f) _arrivalSounds?.Play(FairyArrivalSound.Step, "foot-left", _instance.transform.position, .48f);
                if (approachElapsed >= 4.9f) _arrivalSounds?.Play(FairyArrivalSound.Step, "foot-right", _instance.transform.position, .6f);
                if (crossing < 1) return;
                if (!_arrivalLanded)
                {
                    if (!_arrivalOtherWorldWindow.HasBodyClearedBoundary())
                    {
                        if (_arrivalElapsed > landedAt + 3f)
                            throw new InvalidOperationException("Arrival body did not clear its fixed boundary.");
                        return;
                    }
                    _arrivalLanded = true;
                    _arrivalOtherWorldWindow.RevealCharacter();
                    _animation?.PlayWonder();
                }
                var afterLanding = _arrivalElapsed - landedAt;
                // A grounded inspection, then a return of attention to the viewer, with time for a full reaction.
                var turn = Mathf.Sin(Mathf.Clamp01(afterLanding / 2.2f) * Mathf.PI) * 32f;
                _instance.transform.rotation = Quaternion.LookRotation(-_arrivalStepDirection) * Quaternion.Euler(0, turn, 0);
                if (!_arrivalResponseStarted && afterLanding >= 2.2f)
                {
                    _arrivalResponseStarted = true;
                    _animation?.EndWonder();
                    _animation?.PlayInteraction();
                    if (_idleLocomotionClips != null && _idleLocomotionClips.Count > 0)
                        PlayArrivalClip(_idleLocomotionClips[0], .65f);
                }
                if (afterLanding >= 1.2f) _arrivalSounds?.Play(FairyArrivalSound.Close, "portal-closed", volume: .5f);
                var closing = Mathf.Clamp01((afterLanding - 1.2f) / Mathf.Max(2f, _arrivalRevealDelaySeconds));
                _arrivalFlashlightAperture?.SetClosingProgress(closing);
                if (_arrivalOtherWorldWindow != null)
                    _arrivalOtherWorldWindow.SetStrength(_arrivalFlashlightAperture.CurrentStrength);
                if (closing >= 1) ReleaseArrivalMask();
                if (afterLanding < Mathf.Max(ArrivalSettleSeconds, 1.5f + _arrivalRevealDelaySeconds)) return;
                FinishFirstArrival();
            }
            catch (Exception exception)
            {
                HandleArrivalSequenceFailure(exception, "arrival_local_sequence");
            }
        }

        // Two failed openings build pressure before the third holds. Never contract during body crossing.
        internal static float EvaluateArrivalOpening(float seconds)
        {
            if (seconds < .8f) return 0;
            if (seconds < 1.25f) return Mathf.Lerp(0, .4f, Mathf.SmoothStep(0, 1, (seconds - .8f) / .45f));
            if (seconds < 1.85f) return Mathf.Lerp(.4f, .06f, Mathf.SmoothStep(0, 1, (seconds - 1.25f) / .6f));
            if (seconds < 2.15f) return .06f;
            if (seconds < 2.7f) return Mathf.Lerp(.06f, .68f, Mathf.SmoothStep(0, 1, (seconds - 2.15f) / .55f));
            if (seconds < 3.35f) return Mathf.Lerp(.68f, .18f, Mathf.SmoothStep(0, 1, (seconds - 2.7f) / .65f));
            return Mathf.Lerp(.18f, 1, Mathf.SmoothStep(0, 1, (seconds - 3.35f) / 1.25f));
        }

        internal static float ArrivalPulse(float seconds, float start, float peak, float end)
            => seconds < peak ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, peak, seconds)) :
                1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(peak, end, seconds));

        internal static float EvaluateArrivalCollision(float seconds)
            => .55f * ArrivalPulse(seconds, .35f, .48f, 1.05f) +
               .78f * ArrivalPulse(seconds, 1.85f, 1.98f, 2.5f) +
               ArrivalPulse(seconds, 3.15f, 3.32f, 4.05f);

        internal static float EvaluateArrivalTravel(float seconds)
        {
            // Authored walk cadence is independent of the preceding discovery beats.
            return Mathf.Clamp01(seconds / ArrivalCrossingSeconds);
        }

        void CompleteImmediateArrival()
            => AdvanceFirstArrival(ArrivalPreludeSeconds + OfficialAutomaticWindowOpenSeconds + ArrivalWorldEstablishSeconds +
                _arrivalEffectDelaySeconds + ArrivalCrossingSeconds + _arrivalRevealDelaySeconds + ArrivalSettleSeconds + 0.01f);

        void FinishFirstArrival()
        {
            _arrivalPassage?.Dispose(); _arrivalPassage = null;
            ReleaseArrivalEffect();
            _animation?.EndWonder();
            _arrivalVisualState.Restore();
            _orbit.enabled = true;
            _gazeInteraction.enabled = true;
            _arrivalCompleted = true;
            _arrivalInProgress = false;
            _arrivalRoutine = null;
            _arrivalAudioSource.Stop();
            _arrivalAudioSource.spatialBlend = 1f;
            _arrivalAudioSource.volume = 1f;
            Publish(FairyPhase.Active);
            EnsureIdleLocomotionRoutine();
        }

        void RevealInstance()
        {
            _instance.SetActive(true);
            _orbit.enabled = true;
            _gazeInteraction.enabled = true;
            _animation?.Play();
            Publish(FairyPhase.Active);
            EnsureIdleLocomotionRoutine();
        }

        void CompleteFirstArrivalReveal()
        {
            _arrivalOtherWorldWindow?.BeginCrossing();
            // The boundary crossing and foot contacts have their own events below.
            _animation?.EndWonder();
            _animation?.SetMoving(true);
            _animation?.SetTravelSpeed(ArrivalWalkDistance / ArrivalCrossingSeconds);
        }

        void CreateArrivalMask()
        {
            if (_arrivalMaskPrefab == null)
                throw new InvalidOperationException("Fairy arrival requires the official discovery-mask prefab.");

            _arrivalMaskInstance = Instantiate(_arrivalMaskPrefab, _instance.transform, false);
            _arrivalMaskInstance.name = "FairyArrivalDiscoveryMask";
            _arrivalFlashlightAperture = _arrivalMaskInstance
                .GetComponentInChildren<FairyArrivalFlashlightAperture>(true);
            if (_arrivalFlashlightAperture == null)
                throw new InvalidOperationException(
                    "Fairy arrival mask requires the official flashlight aperture.");
            _arrivalFlashlightAperture.Initialize(_viewer, _instance.transform);
            _arrivalFlashlightAperture.SetOpeningProgress(0f);
            _arrivalOtherWorldWindow = _arrivalMaskInstance
                .GetComponent<FairyArrivalOtherWorldWindow>();
            if (_arrivalOtherWorldWindow == null)
                throw new InvalidOperationException(
                    "Fairy arrival mask requires the automatic other-world window.");
            _arrivalOtherWorldWindow.Initialize(_instance.transform, _arrivalFlashlightAperture);
            var door = _arrivalLandingPosition + _arrivalStepDirection * ArrivalDoorOffset + Vector3.up * .8f;
            _arrivalSounds = _arrivalMaskInstance.GetComponent<FairyArrivalSoundEvents>();
            _arrivalResonance = _arrivalMaskInstance.GetComponent<FairyArrivalResonance>();
            if (_arrivalSounds == null || _arrivalResonance == null)
                throw new InvalidOperationException("Arrival mask requires resonance and event sound configuration.");
            _arrivalSounds.Initialize(door);
            _arrivalSounds.Played += (key, clip, position, volume) => ArrivalSoundPlayed?.Invoke(_arrivalRealElapsed, key, clip, position, volume);
            _arrivalResonance.Initialize(_viewer, door, _arrivalHandPosition);
            _arrivalResonance.Changed += key =>
            {
                Diagnose("FAIRY_ARRIVAL_INTERACTION", key, "arrival");
                if (key == "artifact-escaped") _arrivalSounds?.Play(FairyArrivalSound.Recoil, key, _arrivalResonance.Position, .55f);
                if (key == "artifact-touched") _arrivalSounds?.Play(FairyArrivalSound.Stabilize, key, _arrivalResonance.Position, .8f);
                if (key == "artifact-conduit") _arrivalSounds?.Play(FairyArrivalSound.Crack, key, _arrivalResonance.Position, .7f);
                if (key == "space-rupture") _arrivalSounds?.Play(FairyArrivalSound.Connect, key, volume: .85f);
            };
        }

        void PlayArrivalShockwave()
        {
            if (_arrivalEffectPrefab == null)
                throw new InvalidOperationException("Fairy arrival requires the official composite VFX prefab.");
            _arrivalEffectInstance = Instantiate(_arrivalEffectPrefab);
            _arrivalEffectInstance.name = "FairyArrivalWorldShockwave";
            _arrivalEffectInstance.transform.position = _arrivalLandingPosition + _arrivalStepDirection * ArrivalDoorOffset + Vector3.up * .8f;
            var particles = _arrivalEffectInstance.GetComponent<ParticleSystem>();
            if (particles == null)
                throw new InvalidOperationException("Fairy arrival effect requires a root ParticleSystem.");
            particles.Play();
        }

        void CreateCompanionFeedback(FairyCompanionFeedbackDefinition feedback)
        {
            _companionCueEffectInstance = Instantiate(feedback.EffectPrefab, _instance.transform, false);
            _companionCueEffectInstance.name = "FairyCompanionCueEffect";
            var effectTransform = _companionCueEffectInstance.transform;
            effectTransform.localPosition = feedback.EffectPrefab.transform.localPosition +
                                            feedback.EffectLocalOffset;
            effectTransform.localRotation = feedback.EffectPrefab.transform.localRotation;
            effectTransform.localScale = feedback.EffectPrefab.transform.localScale *
                                         feedback.EffectLocalScale;
            _companionCueParticles = _companionCueEffectInstance.GetComponent<ParticleSystem>();
            if (_companionCueParticles == null)
                throw new InvalidOperationException(
                    "Fairy companion feedback requires a root ParticleSystem.");
            _companionCueParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void PlayCompanionFeedback(
            AudioClip clip,
            float volume,
            float effectSeconds,
            int particleBurst)
        {
            if (_arrivalAudioSource == null || _companionCueParticles == null || clip == null)
                throw new InvalidOperationException(
                    "Fairy companion feedback requires its single AudioSource, pooled particles, and configured clip.");

            if (_ambientAudioSuppressed || _speechClip != null) return;
            CompanionFeedbackPlayCount++;
            LastCompanionFeedbackClip = clip;
            if (!Application.isPlaying) return;

            PlayFeedbackAudioAndParticles(clip, volume, effectSeconds, particleBurst);
        }

        void EnsureIdleLocomotionRoutine()
        {
            if (!Application.isPlaying || _idleLocomotionRoutine != null || _instance == null)
                return;
            _idleLocomotionRoutine = StartCoroutine(PlayIdleLocomotionFeedbackLoop());
        }

        IEnumerator PlayIdleLocomotionFeedbackLoop()
        {
            var firstFeedback = true;
            while (_instance != null)
            {
                var delay = firstFeedback
                    ? Mathf.Min(
                        FirstIdleLocomotionFeedbackDelaySeconds,
                        _idleLocomotionMinIntervalSeconds)
                    : UnityEngine.Random.Range(
                        _idleLocomotionMinIntervalSeconds,
                        _idleLocomotionMaxIntervalSeconds);
                firstFeedback = false;
                yield return new WaitForSecondsRealtime(delay);
                if (CanPlayIdleLocomotionFeedback())
                    PlayIdleLocomotionFeedback();
            }

            _idleLocomotionRoutine = null;
        }

        bool CanPlayIdleLocomotionFeedback()
            => _behavior == FairyBehavior.Guide &&
               _phase == FairyPhase.Active &&
               _arrivalRoutine == null &&
               _arrivalCompleted &&
               _instance != null &&
               _instance.activeInHierarchy &&
               !_ambientAudioSuppressed &&
               !_walking &&
               _speechClip == null &&
               !_companionAudioPlaying &&
               _arrivalAudioSource != null &&
               !_arrivalAudioSource.isPlaying &&
               Time.unscaledTime >= _companionCueEndsAt;

        void PlayIdleLocomotionFeedback()
        {
            if (_idleLocomotionClips == null || _idleLocomotionClips.Count == 0 ||
                _arrivalAudioSource == null || _companionCueParticles == null ||
                _ambientAudioSuppressed || _speechClip != null || _companionAudioPlaying || _arrivalAudioSource.isPlaying)
                return;

            var clip = _idleLocomotionClips[UnityEngine.Random.Range(0, _idleLocomotionClips.Count)];
            if (clip == null) return;

            IdleLocomotionFeedbackPlayCount++;
            LastIdleLocomotionFeedbackClip = clip;
            if (!Application.isPlaying) return;

            _animation?.PlayIdleReaction();
            PlayFeedbackAudioAndParticles(
                clip,
                _idleLocomotionVolume,
                _idleLocomotionEffectSeconds,
                _idleLocomotionParticleBurst);
        }

        void PlayFeedbackAudioAndParticles(
            AudioClip clip,
            float volume,
            float effectSeconds,
            int particleBurst)
        {
            if (_companionAudioPlaying) _arrivalAudioSource.Stop();
            _arrivalAudioSource.PlayOneShot(clip, volume);
            _companionAudioPlaying = true;
            if (Application.isPlaying) StartCoroutine(FairyAudioPlaybackProbe.Observe(_arrivalAudioSource, clip, _viewer, "companion"));
            _companionCueParticles.Clear(true);
            _companionCueParticles.Play(true);
            _companionCueParticles.Emit(particleBurst);
            var generation = ++_companionFeedbackGeneration;
            _companionFeedbackRoutine = StartCoroutine(
                StopCompanionFeedbackAfter(effectSeconds, clip.length, generation));
        }

        IEnumerator StopCompanionFeedbackAfter(
            float effectSeconds,
            float audioSeconds,
            int generation)
        {
            yield return new WaitForSecondsRealtime(effectSeconds);
            if (generation != _companionFeedbackGeneration) yield break;
            _companionCueParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            var remainingAudioSeconds = audioSeconds - effectSeconds;
            if (remainingAudioSeconds > 0f)
                yield return new WaitForSecondsRealtime(remainingAudioSeconds);
            if (generation != _companionFeedbackGeneration) yield break;

            _companionAudioPlaying = false;
            _companionFeedbackRoutine = null;
        }

        IEnumerator GuardSpeechPlayback(FairySpeech speech, int generation)
        {
            var sequence = PlaySpeechWhenReady(speech, generation);
            while (generation == _speechGeneration)
            {
                var hasNext = false;
                Exception failure = null;
                try { hasNext = sequence.MoveNext(); }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    FinishSpeech(generation, false, "exception_" + failure.GetType().Name);
                    yield break;
                }
                if (!hasNext) yield break;
                yield return sequence.Current;
            }
        }

        IEnumerator PlaySpeechWhenReady(FairySpeech speech, int generation)
        {
            // Yield first so a synchronous failure cannot leave a stale Coroutine handle.
            yield return null;
            var arrivalDeadline = Time.realtimeSinceStartup + ArrivalPreludeSeconds + ArrivalWorldEstablishSeconds + OfficialAutomaticWindowOpenSeconds +
                                  _arrivalEffectDelaySeconds +
                                  (_arrivalRevealDelaySeconds + ArrivalSettleSeconds) +
                                  SpeechPreparationGraceSeconds;
            if (_arrivalRoutine != null || !_arrivalCompleted)
                DiagnoseSpeech("FAIRY_SPEECH_WAITING", speech.Clip, "arrival", generation);
            while (_arrivalRoutine != null || !_arrivalCompleted)
            {
                if (generation != _speechGeneration) yield break;
                if (!_arrivalInProgress && Time.realtimeSinceStartup >= arrivalDeadline)
                {
                    FinishSpeech(generation, false, "arrival_timeout");
                    yield break;
                }
                yield return null;
            }
            if (generation != _speechGeneration) yield break;

            var loadDeadline = Time.realtimeSinceStartup + SpeechPreparationGraceSeconds;
            if (!speech.Clip.LoadAudioData())
            {
                FinishSpeech(generation, false, "load_rejected");
                yield break;
            }
            while (speech.Clip.loadState == AudioDataLoadState.Loading)
            {
                if (generation != _speechGeneration) yield break;
                if (Time.realtimeSinceStartup >= loadDeadline) break;
                yield return null;
            }
            if (generation != _speechGeneration) yield break;
            if (speech.Clip.loadState != AudioDataLoadState.Loaded || _instance == null ||
                !_instance.activeInHierarchy || _speechBlocked || !_arrivalAudioSource.enabled)
            {
                FinishSpeech(generation, false, "source_not_ready");
                yield break;
            }

            StopCompanionFeedback(false);
            _arrivalAudioSource.Stop();
            _arrivalAudioSource.clip = speech.Clip;
            _arrivalAudioSource.volume = speech.Volume;
            _arrivalAudioSource.Play();
            _speechPlaying = true;
            _animation?.PlayCoachAttention();

            var startDeadline = Time.realtimeSinceStartup + SpeechStartGraceSeconds;
            yield return null;
            while (generation == _speechGeneration && !_arrivalAudioSource.isPlaying &&
                   Time.realtimeSinceStartup < startDeadline)
                yield return null;
            if (generation != _speechGeneration) yield break;
            if (!_arrivalAudioSource.isPlaying)
            {
                FinishSpeech(generation, false, "source_did_not_start");
                yield break;
            }

            // The spatializer instance is available once this source has started.
            // Boost speech only; keep the authored feedback mix and 3D positioning.
            if (!_arrivalAudioSource.SetSpatializerFloat(SpatialGainParameter, SpeechGainBoostDb))
                DiagnoseSpeech("FAIRY_SPEECH_GAIN_UNAVAILABLE", speech.Clip, "gain_not_applied", generation);
            SpeechPlayCount++;
            LastSpeechClip = speech.Clip;
            DiagnoseSpeech("FAIRY_SPEECH_STARTED", speech.Clip, "playing", generation);
            var deadline = Time.realtimeSinceStartup + speech.Clip.length + SpeechCompletionGraceSeconds;
            var samples = new float[256];
            var sampleInterval = new WaitForSecondsRealtime(0.05f);
            while (generation == _speechGeneration && _arrivalAudioSource.isPlaying &&
                   Time.realtimeSinceStartup < deadline)
            {
                _speechPlayedSamples = Mathf.Max(_speechPlayedSamples, _arrivalAudioSource.timeSamples);
                _arrivalAudioSource.GetOutputData(samples, 0);
                foreach (var value in samples) _speechOutputPeak = Mathf.Max(_speechOutputPeak, Mathf.Abs(value));
                yield return sampleInterval;
            }
            if (generation != _speechGeneration) yield break;
            var reachedEnd = !_arrivalAudioSource.isPlaying &&
                             _speechPlayedSamples >= speech.Clip.samples - speech.Clip.frequency * 0.15f;
            FinishSpeech(generation, reachedEnd,
                reachedEnd ? "completed" : "stopped_before_end");
        }

        FairyResult RejectSpeech(FairySpeech speech, FairyFailureCode code, string reason)
        {
            DiagnoseSpeech("FAIRY_SPEECH_REJECTED", speech.Clip, reason, _speechGeneration);
            return FairyResult.Failure(code, "fairy.speech." + reason);
        }

        void StopSpeech(string reason)
        {
            var generation = _speechGeneration++;
            var clip = _speechClip;
            var completion = _speechCompletion;
            _speechClip = null;
            _speechRequestId = Guid.Empty;
            _speechCompletion = null;
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }
            if (clip != null)
                DiagnoseSpeech("FAIRY_SPEECH_CANCELLED", clip, reason, generation, _speechOutputPeak, _speechPlayedSamples);
            if (_speechPlaying) ResetSpeechSource();
            _speechPlaying = false;
            if (clip == null) return;
            NotifySpeechCompletion(completion,
                FairyResult.Failure(FairyFailureCode.SpeechCancelled, "fairy.speech." + reason));
        }

        void FinishSpeech(int generation, bool succeeded, string reason)
        {
            if (generation != _speechGeneration) return;
            var clip = _speechClip;
            var completion = _speechCompletion;
            _speechClip = null;
            _speechRequestId = Guid.Empty;
            _speechCompletion = null;
            _speechRoutine = null;
            _speechPlaying = false;
            DiagnoseSpeech(succeeded ? "FAIRY_SPEECH_COMPLETED" : "FAIRY_SPEECH_FAILED",
                clip, reason, generation, _speechOutputPeak, _speechPlayedSamples);
            if (succeeded && _speechOutputPeak <= 0.00001f)
                DiagnoseSpeech("FAIRY_SPEECH_OUTPUT_SILENT", clip, "no_measured_output", generation, _speechOutputPeak, _speechPlayedSamples);
            ResetSpeechSource();
            NotifySpeechCompletion(completion, succeeded
                ? FairyResult.Success()
                : FairyResult.Failure(FairyFailureCode.AudioUnavailable, "fairy.speech." + reason));
        }

        void ResetSpeechSource()
        {
            if (_arrivalAudioSource == null) return;
            // Reset before Stop, while the native spatializer is still available.
            // This source is shared with arrival/companion feedback, not another voice channel.
            _arrivalAudioSource.SetSpatializerFloat(SpatialGainParameter, 0f);
            _arrivalAudioSource.Stop();
            _arrivalAudioSource.clip = null;
            _arrivalAudioSource.volume = 1f;
        }

        void NotifySpeechCompletion(Action<FairyResult> completion, FairyResult result)
        {
            if (completion == null) return;
            try { completion(result); }
            catch (Exception exception)
            {
                Diagnose("FAIRY_SPEECH_CALLBACK_FAILED", exception.GetType().Name, "speech_completion");
            }
        }

        void DiagnoseSpeech(string code, AudioClip clip, string reason, int generation, float peak = 0f, int playedSamples = 0)
        {
            var source = _arrivalAudioSource;
            var spatialGainDb = 0f;
            var hasSpatialGain = source != null && source.GetSpatializerFloat(SpatialGainParameter, out spatialGainDb);
            var distance = source != null && _viewer != null
                ? Vector3.Distance(source.transform.position, _viewer.position) : -1f;
            Diagnose(code, FormattableString.Invariant(
                $"clip={(clip != null ? clip.name : "none")}; request={generation}; reason={reason}; loaded={(clip != null ? clip.loadState.ToString() : "none")}; playing={(source != null && source.isPlaying)}; active={(source != null && source.gameObject.activeInHierarchy)}; enabled={(source != null && source.enabled)}; virtualized={(source != null && source.isVirtual)}; volume={(source != null ? source.volume : 0f):F3}; mute={(source != null && source.mute)}; distance={distance:F3}; spatialized={(source != null && source.spatialize)}; spatial_gain_available={hasSpatialGain}; spatial_gain_db={spatialGainDb:F1}; viewer_listener={(_listener != null ? _listener.isActiveAndEnabled.ToString() : "unknown")}; listener_paused={AudioListener.pause}; listener_volume={AudioListener.volume:F3}; dsp={AudioSettings.dspTime:F3}; samples={playedSamples}; output_peak={peak:F6}"), "speech");
        }

        void StopCompanionFeedback(bool clearParticles)
        {
            ++_companionFeedbackGeneration;
            if (_companionFeedbackRoutine != null)
            {
                StopCoroutine(_companionFeedbackRoutine);
                _companionFeedbackRoutine = null;
            }
            if (_companionAudioPlaying) _arrivalAudioSource?.Stop();
            _companionAudioPlaying = false;
            _companionCueParticles?.Stop(
                true,
                clearParticles
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
        }

        void PlayArrivalClip(AudioClip clip, float volume = 1f)
        {
            if (_arrivalAudioSource == null || clip == null)
                throw new InvalidOperationException("Fairy arrival requires its configured official AudioSource and clip.");
            ArrivalSoundPlayed?.Invoke(_arrivalRealElapsed, "character-voice", clip, _instance.transform.position, volume * .8f);
            _arrivalAudioSource.spatialBlend = .65f;
            _arrivalAudioSource.volume = .8f;
            _arrivalAudioSource.PlayOneShot(clip, volume);
            if (Application.isPlaying) StartCoroutine(FairyAudioPlaybackProbe.Observe(_arrivalAudioSource, clip, _viewer, "arrival"));
        }

        void HandleArrivalSequenceFailure(Exception exception, string stage)
        {
            Debug.LogException(exception, this);
            Diagnose("FAIRY_ARRIVAL_EFFECT_FAILED", exception.Message, stage);
            _arrivalRoutine = null;
            FailArrival();
        }

        FairyResult FailArrival()
        {
            _arrivalPassage?.Dispose(); _arrivalPassage = null;
            _arrivalInProgress = false;
            _arrivalRoutine = null;
            StopCompanionFeedback(true);
            _arrivalAudioSource?.Stop();
            ReleaseArrivalEffect();
            ReleaseArrivalMask();
            _animation?.EndWonder();
            _arrivalVisualState?.Restore();
            if (_orbit != null) _orbit.enabled = true;
            _arrivalCompleted = false;
            _animation?.Stop();
            if (_instance != null) _instance.SetActive(false);
            Publish(FairyPhase.Failed, new UserFault("联系暂时中断，请重新回应邀请。"));
            return FairyResult.Failure(FairyFailureCode.LoadFailed, "fairy.arrival.failed");
        }

        void CancelArrival()
        {
            _arrivalPassage?.Dispose(); _arrivalPassage = null;
            _celebrationPending = _dialogueReactionPending = false;
            _arrivalInProgress = false;
            _speechBlocked = true;
            StopIdleLocomotionRoutine();
            if (_arrivalRoutine != null)
            {
                StopCoroutine(_arrivalRoutine);
                _arrivalRoutine = null;
            }
            StopSpeech("hidden_or_released");
            StopCompanionFeedback(true);
            _arrivalAudioSource?.Stop();
            ReleaseArrivalEffect();
            ReleaseArrivalMask();
            _animation?.EndWonder();
            _arrivalVisualState?.Restore();
            if (_orbit != null) _orbit.enabled = true;
        }

        void StopIdleLocomotionRoutine()
        {
            if (_idleLocomotionRoutine == null) return;
            StopCoroutine(_idleLocomotionRoutine);
            _idleLocomotionRoutine = null;
        }

        void ReleaseArrivalEffect()
        {
            var effect = _arrivalEffectInstance;
            _arrivalEffectInstance = null;
            if (effect != null) effect.SetActive(false);
            DestroyOwnedObject(effect);
        }

        void ReleaseArrivalMask()
        {
            var mask = _arrivalMaskInstance;
            _arrivalMaskInstance = null;
            _arrivalFlashlightAperture = null;
            _arrivalOtherWorldWindow = null;
            _arrivalResonance = null; _arrivalSounds = null;
            if (mask != null) mask.SetActive(false);
            DestroyOwnedObject(mask);
        }

        void ReleaseInstance()
        {
            CancelArrival();
            var interaction = _interaction;
            var animation = _animation;
            var instance = _instance;
            _interaction = null;
            _orbit = null;
            _gazeInteraction = null;
            _animation = null;
            _arrivalAudioSource = null;
            _companionCueEffectInstance = null;
            _companionCueParticles = null;
            _coachAttentionClip = null;
            _artifactWaitClip = null;
            _artifactReturnClip = null;
            _celebrateClip = null;
            _idleLocomotionClips = null;
            _instance = null;
            _behavior = default;
            _arrivalEffectPrefab = null;
            _arrivalMaskPrefab = null;
            _arrivalEffectDelaySeconds = 0f;
            _arrivalRevealDelaySeconds = 0f;
            _coachAttentionVolume = 0f;
            _artifactWaitVolume = 0f;
            _artifactReturnVolume = 0f;
            _celebrateVolume = 0f;
            _idleLocomotionVolume = 0f;
            _coachAttentionEffectSeconds = 0f;
            _artifactWaitEffectSeconds = 0f;
            _artifactReturnEffectSeconds = 0f;
            _celebrateEffectSeconds = 0f;
            _idleLocomotionMinIntervalSeconds = 0f;
            _idleLocomotionMaxIntervalSeconds = 0f;
            _idleLocomotionEffectSeconds = 0f;
            _coachAttentionParticleBurst = 0;
            _artifactWaitParticleBurst = 0;
            _artifactReturnParticleBurst = 0;
            _celebrateParticleBurst = 0;
            _idleLocomotionParticleBurst = 0;
            _lastCompanionCueKind = FairyCompanionCueKind.Idle;
            _companionCueEndsAt = 0f;
            _companionAudioPlaying = false;
            _speechPlaying = false;
            _speechClip = null;
            _speechCompletion = null;
            _speechRoutine = null;
            _ambientAudioSuppressed = false;
            _walking = false;
            CompanionFeedbackPlayCount = 0;
            LastCompanionFeedbackClip = null;
            IdleLocomotionFeedbackPlayCount = 0;
            LastIdleLocomotionFeedbackClip = null;
            SpeechPlayCount = 0;
            LastSpeechClip = null;
            _arrivalCompleted = false;

            Exception releaseFailure = null;
            try
            {
                animation?.Stop();
            }
            catch (Exception exception)
            {
                releaseFailure = exception;
            }

            try
            {
                interaction?.Dispose();
            }
            catch (Exception exception)
            {
                if (releaseFailure == null) releaseFailure = exception;
            }

            DestroyOwnedObject(instance);
            if (releaseFailure != null) throw releaseFailure;
        }

        static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        void OnDestroy()
        {
            try
            {
                ReleaseInstance();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("FAIRY_RELEASE_FAILED", exception.Message, "destroy");
            }
            finally
            {
                _states?.Dispose();
            }
        }

        void Diagnose(string code, string message, string stage)
        {
            try
            {
                _diagnostics?.Invoke(new DiagnosticEvent(
                    code, message, "Fairy", stage, DateTimeOffset.UtcNow, sessionToken: _session));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Fairy diagnostic sink failed: {exception.GetType().Name}.");
            }
        }

        void Publish(FairyPhase phase, UserFault fault = null)
        {
            _phase = phase;
            var state = new FairyState(_session, ++_version, phase, fault);
            _states.Publish(state);
        }

    }
}
