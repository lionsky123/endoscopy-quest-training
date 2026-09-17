using System;
using System.IO;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;
using UnityEngine.Video;

namespace BotanicalGardenQR.Video.Backend
{
    internal sealed class VideoController : MonoBehaviour, IVideoController
    {
        const int DefaultRenderTextureWidth = 1280;
        const int DefaultRenderTextureHeight = 720;
        const int MaxPreparedRenderTextureEdge = 1920;

        readonly StateChannel<VideoState> _states = StateChannel<VideoState>.ForCurrentThread(
            new VideoState(default, 0, VideoPhase.Closed, 0d, 0d, false, false, false, false),
            state => state.Version);

        VideoRuntimeOptions _options;
        VideoPlayer _player;
        AudioSource _audioSource;
        GameObject _instance;
        RenderTexture _renderTexture;
        VideoSurfaceLease _surface;
        VideoDefinition _definition;
        SessionToken _session;
        VideoPhase _phase = VideoPhase.Closed;
        long _version;
        uint _generation;
        float _nextSnapshotAt;
        float _prepareDeadline;
        bool _isMuted;

        internal void Initialize(VideoRuntimeOptions options)
        {
            if (_options != null) throw new InvalidOperationException("Video controller is already initialized.");
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public VideoResult Open(SessionToken session, VideoDefinition definition, VideoSurfaceLease surface)
        {
            if (_options == null || !session.IsValid || definition == null)
                return VideoResult.Failure(VideoFailureCode.InvalidDefinition, "video.definition.invalid");
            if (surface == null || surface.ContentRoot == null)
                return VideoResult.Failure(VideoFailureCode.SurfaceUnavailable, "video.surface.unavailable");
            if (_player != null || _surface != null)
                return VideoResult.Failure(VideoFailureCode.PlaybackFailed, "video.already_open");

            _session = session;
            _definition = definition;
            _surface = surface;
            _isMuted = false;
            var generation = ++_generation;

            try
            {
                CreateSurface(surface.ContentRoot);
                _player = _instance.AddComponent<VideoPlayer>();
                _audioSource = _instance.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
                _audioSource.spatialBlend = 0f;

                _player.playOnAwake = false;
                _player.waitForFirstFrame = true;
                _player.skipOnDrop = true;
                _player.isLooping = definition.Loop;
                _player.renderMode = VideoRenderMode.RenderTexture;
                _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                _player.controlledAudioTrackCount = 1;
                _player.SetTargetAudioSource(0, _audioSource);
                _player.prepareCompleted += player => OnPrepared(player, session, generation);
                _player.loopPointReached += player => OnLoopPointReached(player, session, generation);
                _player.errorReceived += (player, message) => OnError(player, session, generation, message);
                if (definition.Source.Kind == VideoSourceKind.VideoClip)
                {
                    _player.source = UnityEngine.Video.VideoSource.VideoClip;
                    _player.clip = definition.Source.Clip;
                }
                else
                {
                    _player.source = UnityEngine.Video.VideoSource.Url;
                    _player.url = Path.Combine(Application.streamingAssetsPath, definition.Source.StreamingAssetsPath);
                }

                EnsureRenderTexture(DefaultRenderTextureWidth, DefaultRenderTextureHeight);
                _player.targetTexture = _renderTexture;
                _surface.Target.SetTexture(_renderTexture, 16f / 9f);
                _surface.Target.SetVisible(false);

                Publish(VideoPhase.Loading);
                _prepareDeadline = Time.unscaledTime + _options.PrepareTimeoutSeconds;
                _player.Prepare();
                _player.Play();
                return VideoResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseRuntime();
                Publish(VideoPhase.Failed, new UserFault("视频播放失败"));
                return VideoResult.Failure(VideoFailureCode.PlaybackFailed, "video.surface.create_failed");
            }
        }

        public VideoResult Dispatch(SessionToken session, VideoIntent intent)
        {
            if (_player == null)
                return VideoResult.Failure(VideoFailureCode.NotOpen, "video.not_open");
            if (session != _session)
                return VideoResult.Failure(VideoFailureCode.StaleSession, "video.stale_session");

            try
            {
                switch (intent.Kind)
                {
                    case VideoIntentKind.TogglePlayback:
                        if (_phase == VideoPhase.Playing)
                        {
                            _player.Pause();
                            Publish(VideoPhase.Paused);
                            return VideoResult.Success();
                        }
                        if (_phase == VideoPhase.Paused)
                        {
                            _player.Play();
                            Publish(VideoPhase.Playing);
                            return VideoResult.Success();
                        }
                        return VideoResult.Failure(VideoFailureCode.InvalidIntent, "video.playback.unavailable");

                    case VideoIntentKind.Replay:
                        if (!CanReplay())
                            return VideoResult.Failure(VideoFailureCode.InvalidIntent, "video.replay.unavailable");
                        _player.time = 0d;
                        _player.Play();
                        Publish(VideoPhase.Playing);
                        return VideoResult.Success();

                    case VideoIntentKind.ToggleMute:
                        if (!CanMute())
                            return VideoResult.Failure(VideoFailureCode.InvalidIntent, "video.mute.unavailable");
                        _isMuted = !_isMuted;
                        _audioSource.mute = _isMuted;
                        Publish(_phase);
                        return VideoResult.Success();

                    default:
                        return VideoResult.Failure(VideoFailureCode.InvalidIntent, "video.intent.unknown");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Fail(new UserFault("视频播放失败"));
                return VideoResult.Failure(VideoFailureCode.PlaybackFailed, "video.dispatch.failed");
            }
        }

        public VideoResult Close(SessionToken session)
        {
            if (_player == null && _surface == null)
                return VideoResult.Success();
            if (session != _session)
                return VideoResult.Failure(VideoFailureCode.StaleSession, "video.stale_session");

            ++_generation;
            ReleaseRuntime();
            Publish(VideoPhase.Closed);
            return VideoResult.Success();
        }

        public IDisposable Observe(IVideoStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.Publish);
        }

        void Update()
        {
            if (_player == null)
                return;
            if (HasPrepareTimedOut(_phase, Time.unscaledTime, _prepareDeadline))
            {
                FailPreparation(new UserFault("视频加载超时，请返回后重试。"));
                return;
            }
            if (_phase != VideoPhase.Playing && _phase != VideoPhase.Paused)
                return;
            if (_phase != VideoPhase.Playing || Time.unscaledTime < _nextSnapshotAt)
                return;
            _nextSnapshotAt = Time.unscaledTime + 0.1f;
            Publish(VideoPhase.Playing);
        }

        void OnDestroy()
        {
            ++_generation;
            ReleaseRuntime();
            _states.Dispose();
        }

        void OnPrepared(VideoPlayer player, SessionToken session, uint generation)
        {
            if (!IsCurrent(player, session, generation)) return;
            var width = (int)player.width;
            var height = (int)player.height;
            if ((width <= 0 || height <= 0) && player.texture != null)
            {
                width = player.texture.width;
                height = player.texture.height;
            }
            var preparedSize = GetPreparedRenderTextureSize(width, height);
            EnsureRenderTexture(preparedSize.x, preparedSize.y);
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _renderTexture;
            _surface.Target.SetTexture(_renderTexture, (float)preparedSize.x / preparedSize.y);
            _surface.Target.SetVisible(true);
            _audioSource.mute = _isMuted;
            _player.Play();
            _prepareDeadline = 0f;
            _nextSnapshotAt = Time.unscaledTime;
            Publish(VideoPhase.Playing);
        }

        static Vector2Int GetPreparedRenderTextureSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return new Vector2Int(DefaultRenderTextureWidth, DefaultRenderTextureHeight);

            var longestEdge = Mathf.Max(width, height);
            if (longestEdge > MaxPreparedRenderTextureEdge)
            {
                var scale = MaxPreparedRenderTextureEdge / (float)longestEdge;
                width = Mathf.Max(2, Mathf.RoundToInt(width * scale));
                height = Mathf.Max(2, Mathf.RoundToInt(height * scale));
            }

            return new Vector2Int(width, height);
        }

        void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height && _renderTexture.IsCreated())
                return;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "VideoRuntimeTexture",
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _renderTexture.Create();
        }

        void OnLoopPointReached(VideoPlayer player, SessionToken session, uint generation)
        {
            if (!IsCurrent(player, session, generation)) return;
            if (_definition != null && _definition.Loop)
            {
                _player.time = 0d;
                _player.Play();
                Publish(VideoPhase.Playing);
                return;
            }

            Publish(VideoPhase.Completed);
        }

        void OnError(VideoPlayer player, SessionToken session, uint generation, string message)
        {
            if (!IsCurrent(player, session, generation)) return;
            if (_phase == VideoPhase.Loading)
            {
                FailPreparation(new UserFault("视频加载失败"));
                return;
            }
            Fail(new UserFault("视频播放失败"));
        }

        void FailPreparation(UserFault fault)
        {
            ++_generation;
            ReleaseRuntime();
            Publish(VideoPhase.Failed, fault);
        }

        void Fail(UserFault fault)
        {
            HideSurface();
            Publish(VideoPhase.Failed, fault);
        }

        bool IsCurrent(VideoPlayer player, SessionToken session, uint generation)
            => _player != null && player == _player && session == _session && generation == _generation;

        bool CanReplay() => _phase == VideoPhase.Playing || _phase == VideoPhase.Paused || _phase == VideoPhase.Completed;
        bool CanMute() => _audioSource != null && _phase != VideoPhase.Loading && _phase != VideoPhase.Failed;

        void CreateSurface(Transform parent)
        {
            _instance = new GameObject("VideoRuntime");
            _instance.layer = parent.gameObject.layer;
            _instance.transform.SetParent(parent, false);
        }

        void HideSurface()
        {
            if (_surface == null || _surface.Target == null) return;
            _surface.Target.SetVisible(false);
            _surface.Target.Clear();
        }

        void ReleaseRuntime()
        {
            if (_player != null)
            {
                _player.targetTexture = null;
                _player.targetMaterialRenderer = null;
                _player.Stop();
            }
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
            if (_instance != null) Destroy(_instance);

            _player = null;
            _audioSource = null;
            _renderTexture = null;
            _instance = null;
            _definition = null;
            _prepareDeadline = 0f;
            _surface?.Dispose();
            _surface = null;
            _phase = VideoPhase.Closed;
        }

        void Publish(VideoPhase phase, UserFault fault = default)
        {
            _phase = phase;
            var position = _player == null ? 0d : Math.Max(0d, _player.time);
            var duration = _player == null ? 0d : Math.Max(0d, _player.length);
            if (phase == VideoPhase.Completed && duration > 0d)
                position = duration;
            var state = new VideoState(
                _session,
                ++_version,
                phase,
                position,
                duration,
                _isMuted,
                phase == VideoPhase.Playing || phase == VideoPhase.Paused,
                CanReplay(),
                CanMute(),
                fault);
            _states.Publish(state);
        }

        internal static bool HasPrepareTimedOut(VideoPhase phase, float now, float deadline)
            => phase == VideoPhase.Loading &&
               !float.IsNaN(now) &&
               !float.IsInfinity(now) &&
               !float.IsNaN(deadline) &&
               !float.IsInfinity(deadline) &&
               now >= deadline;
    }
}
