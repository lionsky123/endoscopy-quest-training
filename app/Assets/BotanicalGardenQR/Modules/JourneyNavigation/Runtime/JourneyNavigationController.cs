using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;

namespace BotanicalGardenQR.JourneyNavigation.Runtime
{
    public sealed class JourneyNavigationController : IJourneyNavigation, IDisposable
    {
        readonly StateChannel<JourneyViewState> _states;
        readonly Dictionary<SceneId, JourneyPointResult> _results = new Dictionary<SceneId, JourneyPointResult>();

        JourneySessionId _session;
        SceneId _currentScene;
        readonly HashSet<SceneId> _completed = new HashSet<SceneId>();
        long _completionRevision;
        SessionToken _activeContentSession;
        JourneyContentState _contentState;
        JourneyProgressState _progressState;
        JourneyViewState _state;
        bool _disposed;
        long _version;

        public JourneyNavigationController()
        {
            _contentState = JourneyContentState.NotOpen;
            _progressState = JourneyProgressState.Unavailable;
            _state = BuildState("尚未开始导览。");
            _states = StateChannel<JourneyViewState>.ForCurrentThread(_state, state => state.Version);
        }

        public JourneyViewState CurrentState
        {
            get
            {
                RequireAvailable();
                return _state;
            }
        }

        public void BeginJourney(JourneySessionId session)
        {
            RequireAvailable();
            if (!session.IsValid) throw new ArgumentException("A valid journey session is required.", nameof(session));
            if (_session == session) return;

            _session = session;
            _results.Clear();
            _completed.Clear();
            _completionRevision = 0;
            _contentState = JourneyContentState.NotOpen;
            _progressState = JourneyProgressState.AwaitingQr;
            _currentScene = default;
            _activeContentSession = default;
            Publish("到站后开启发现，记录本次见闻。", true);
        }

        public bool AcceptContentOpened(ContentOpenedFact fact)
        {
            RequireAvailable();
            if (fact == null || !_session.IsValid || fact.JourneySession != _session ||
                (fact.EntryKind != RecognitionSourceKinds.Qr && fact.EntryKind != RecognitionSourceKinds.Fieldbook) || !fact.SceneId.IsValid)
                return false;
            // Activation has already resolved and committed the enabled QR route.
            if (fact.IsRecall && (_currentScene != fact.SceneId || _contentState != JourneyContentState.Paused))
                return false;
            _currentScene = fact.SceneId;
            if (GetResult(_currentScene) == JourneyPointResult.Unvisited)
                _results[_currentScene] = JourneyPointResult.Visited;
            _activeContentSession = fact.ContentSession;
            _contentState = JourneyContentState.Viewing;
            _progressState = JourneyProgressState.AtSelectedPoint;
            Publish("已打开本次发现。", true);
            return true;
        }

        public bool AcceptContentClosed(ContentClosedFact fact)
        {
            RequireAvailable();
            if (fact == null || !_session.IsValid || fact.JourneySession != _session ||
                !_currentScene.IsValid || fact.SceneId != _currentScene ||
                !_activeContentSession.IsValid || fact.ContentSession != _activeContentSession)
                return false;
            _activeContentSession = default;
            _contentState = JourneyContentState.Paused;
            Publish("内容已暂停，可继续浏览或前往下一站。", true);
            return true;
        }

        public JourneyCommandResult Dispatch(JourneyIntent intent)
        {
            RequireAvailable();
            if (!_session.IsValid) return JourneyCommandResult.Reject(JourneyCommandFailure.NoActiveJourney, _state);
            switch (intent.Kind)
            {
                case JourneyIntentKind.RecallCurrentPoint:
                    if (!_currentScene.IsValid || _contentState != JourneyContentState.Paused)
                        return JourneyCommandResult.Reject(JourneyCommandFailure.InvalidState, _state);
                    return JourneyCommandResult.Success(_state, requiresActivationRecall: true);
                case JourneyIntentKind.ContinueToNext:
                    if (!_state.CanContinue)
                        return JourneyCommandResult.Reject(
                            !_currentScene.IsValid ? JourneyCommandFailure.CurrentPointUnavailable : JourneyCommandFailure.InvalidState,
                            _state);
                    return Advance();
                default:
                    return JourneyCommandResult.Reject(JourneyCommandFailure.InvalidState, _state);
            }
        }

        public IDisposable Observe(IJourneyNavigationStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.OnJourneyStateChanged);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _states.Dispose();
            _session = default;
            _currentScene = default;
            _activeContentSession = default;
            _results.Clear();
            _completed.Clear();
            _completionRevision = 0;
        }

        JourneyCommandResult Advance()
        {
            _completed.Add(_currentScene);
            _results[_currentScene] = JourneyPointResult.Explored;
            _completionRevision++;
            _activeContentSession = default;
            _contentState = JourneyContentState.NotOpen;
            _progressState = JourneyProgressState.AwaitingQr;
            Publish("本次观察已完成，可以前往下一站。", true);
            return JourneyCommandResult.Success(_state);
        }

        JourneyPointResult GetResult(SceneId scene)
            => _results.TryGetValue(scene, out var result) ? result : JourneyPointResult.Unvisited;

        JourneyViewState BuildState(string message, bool hasSession = false)
        {
            var result = GetResult(_currentScene);
            var canContinue = _currentScene.IsValid && _contentState == JourneyContentState.Paused &&
                              _progressState == JourneyProgressState.AtSelectedPoint;
            return new JourneyViewState(_version, hasSession ? _session : default, _currentScene,
                result, _contentState, _progressState, canContinue, message, _completed.Count, _completionRevision);
        }

        void Publish(string message, bool increment)
        {
            if (increment) _version++;
            _state = BuildState(message, _session.IsValid);
            _states.Publish(_state);
        }

        void RequireAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(JourneyNavigationController));
        }
    }
}
