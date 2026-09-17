using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Narration.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Narration.Backend
{
    internal sealed class NarrationController : MonoBehaviour, INarrationController
    {
        readonly StateChannel<NarrationState> _states = StateChannel<NarrationState>.ForCurrentThread(
            new NarrationState(default, 0, NarrationPhase.Closed, 0d, 0d, false, false, false, false),
            state => state.Version);

        AudioSource _player;
        GameObject _playerObject;
        NarrationSurfaceLease _surface;
        SessionToken _session;
        NarrationPhase _phase = NarrationPhase.Closed;
        long _version;
        uint _generation;
        float _nextSnapshotAt;
        bool _playbackObserved;
        bool _isMuted;
        NarrationStartPolicy _startPolicy = NarrationStartPolicy.OnVisitorCommand;

        public NarrationResult Open(SessionToken session, NarrationDefinition definition, NarrationSurfaceLease surface)
        {
            if (!session.IsValid)
                return NarrationResult.Failure(NarrationFailureCode.StaleSession, "narration.session.invalid");
            if (definition == null || definition.Clip == null)
                return NarrationResult.Failure(NarrationFailureCode.InvalidDefinition, "narration.definition.invalid");
            if (surface == null || surface.ContentRoot == null)
                return NarrationResult.Failure(NarrationFailureCode.SurfaceUnavailable, "narration.surface.unavailable");
            if (_player != null)
                return NarrationResult.Failure(NarrationFailureCode.PlaybackFailed, "narration.already_open");

            _session = session;
            _surface = surface;
            _isMuted = false;
            _startPolicy = definition.StartPolicy;
            ++_generation;

            try
            {
                Publish(NarrationPhase.Loading);
                _playerObject = new GameObject("NarrationRuntime");
                _playerObject.transform.SetParent(surface.ContentRoot, false);
                _player = _playerObject.AddComponent<AudioSource>();
                _player.playOnAwake = false;
                _player.loop = false;
                _player.spatialBlend = 0f;
                _player.clip = definition.Clip;
                _player.volume = 1f;
                _player.mute = false;
                Publish(NarrationPhase.Ready);
                if (_startPolicy == NarrationStartPolicy.OnPageEnter)
                    StartPlayback();
                return NarrationResult.Success();
            }
            catch (Exception)
            {
                Publish(NarrationPhase.Failed, new UserFault("讲解音频加载失败"));
                ReleasePlayerAndSurface();
                return NarrationResult.Failure(NarrationFailureCode.LoadFailed, "narration.open.failed");
            }
        }

        public NarrationResult Dispatch(SessionToken session, NarrationIntent intent)
        {
            if (_player == null)
                return NarrationResult.Failure(NarrationFailureCode.NotOpen, "narration.not_open");
            if (session != _session)
                return NarrationResult.Failure(NarrationFailureCode.StaleSession, "narration.stale_session");

            try
            {
                switch (intent.Kind)
                {
                    case NarrationIntentKind.TogglePlayback:
                        if (_phase == NarrationPhase.Playing)
                        {
                            _player.Pause();
                            Publish(NarrationPhase.Paused);
                        }
                        else if (_phase == NarrationPhase.Ready || _phase == NarrationPhase.Paused)
                        {
                            StartPlayback();
                        }
                        else if (_phase == NarrationPhase.Completed)
                        {
                            RestartPlayback();
                        }
                        else
                        {
                            return NarrationResult.Failure(NarrationFailureCode.InvalidIntent, "narration.play.unavailable");
                        }
                        return NarrationResult.Success();

                    case NarrationIntentKind.Replay:
                        if (_phase == NarrationPhase.Loading || _phase == NarrationPhase.Failed)
                            return NarrationResult.Failure(NarrationFailureCode.InvalidIntent, "narration.replay.unavailable");
                        RestartPlayback();
                        return NarrationResult.Success();

                    case NarrationIntentKind.ToggleMute:
                        if (_phase == NarrationPhase.Loading || _phase == NarrationPhase.Failed)
                            return NarrationResult.Failure(NarrationFailureCode.InvalidIntent, "narration.mute.unavailable");
                        _isMuted = !_isMuted;
                        _player.mute = _isMuted;
                        Publish(_phase);
                        return NarrationResult.Success();

                    default:
                        return NarrationResult.Failure(NarrationFailureCode.InvalidIntent, "narration.intent.unknown");
                }
            }
            catch (Exception)
            {
                Publish(NarrationPhase.Failed, new UserFault("讲解音频播放失败"));
                return NarrationResult.Failure(NarrationFailureCode.PlaybackFailed, "narration.playback.failed");
            }
        }

        public NarrationResult Close(SessionToken session)
        {
            if (_player == null)
                return NarrationResult.Success();
            if (session != _session)
                return NarrationResult.Failure(NarrationFailureCode.StaleSession, "narration.stale_session");

            ++_generation;
            _player.Stop();
            ReleasePlayerAndSurface();
            Publish(NarrationPhase.Closed);
            return NarrationResult.Success();
        }

        public IDisposable Observe(INarrationStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.Publish);
        }

        void Update()
        {
            if (_player == null || _phase != NarrationPhase.Playing)
                return;
            if (_player.isPlaying)
            {
                _playbackObserved = true;
                if (Time.unscaledTime >= _nextSnapshotAt)
                {
                    _nextSnapshotAt = Time.unscaledTime + 0.1f;
                    Publish(NarrationPhase.Playing);
                }
                return;
            }

            if (_playbackObserved)
                Publish(NarrationPhase.Completed);
        }

        void OnDestroy()
        {
            ++_generation;
            if (_player != null) _player.Stop();
            ReleasePlayerAndSurface();
            _states.Dispose();
        }

        void StartPlayback()
        {
            _playbackObserved = false;
            _player.mute = _isMuted;
            _player.Play();
            _nextSnapshotAt = Time.unscaledTime;
            Publish(NarrationPhase.Playing);
        }

        void RestartPlayback()
        {
            _player.Stop();
            _player.timeSamples = 0;
            _playbackObserved = false;
            StartPlayback();
        }

        void ReleasePlayerAndSurface()
        {
            if (_playerObject != null)
                Destroy(_playerObject);
            _player = null;
            _playerObject = null;
            _startPolicy = NarrationStartPolicy.OnVisitorCommand;
            _surface?.Dispose();
            _surface = null;
        }

        void Publish(NarrationPhase phase, UserFault fault = default)
        {
            _phase = phase;
            var position = _player == null ? 0d : _player.time;
            var duration = _player == null || _player.clip == null ? 0d : _player.clip.length;
            if (phase == NarrationPhase.Completed && duration > 0d)
                position = duration;
            var state = new NarrationState(
                _session,
                ++_version,
                phase,
                position,
                duration,
                _isMuted,
                phase == NarrationPhase.Ready || phase == NarrationPhase.Playing || phase == NarrationPhase.Paused,
                phase == NarrationPhase.Ready || phase == NarrationPhase.Playing || phase == NarrationPhase.Paused || phase == NarrationPhase.Completed,
                phase != NarrationPhase.Loading && phase != NarrationPhase.Failed && phase != NarrationPhase.Closed,
                fault);
            _states.Publish(state);
        }
    }
}
