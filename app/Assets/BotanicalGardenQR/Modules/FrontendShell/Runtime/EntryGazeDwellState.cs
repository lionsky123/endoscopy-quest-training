using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // Local project copy of the XREAL ImageTracking dwell state machine.
    public readonly struct EntryGazeDwellResult
    {
        public EntryGazeDwellResult(float progress, bool activated)
        {
            Progress = progress;
            Activated = activated;
        }

        public float Progress { get; }
        public bool Activated { get; }
    }

    public sealed class EntryGazeDwellState
    {
        object _currentTarget;
        float _elapsed;
        float _lostElapsed;
        bool _activationLocked;
        bool _requiresGazeExit;

        public EntryGazeDwellResult UpdateTarget(
            object target,
            bool interactable,
            float deltaTime,
            float dwellDuration,
            float lostTargetGraceSeconds)
        {
            var safeDelta = Mathf.Max(0f, deltaTime);
            var safeDuration = Mathf.Max(0.01f, dwellDuration);
            if (target == null)
            {
                if (_requiresGazeExit)
                {
                    _lostElapsed += safeDelta;
                    if (_lostElapsed > Mathf.Max(0f, lostTargetGraceSeconds))
                        Reset();
                    return new EntryGazeDwellResult(0f, false);
                }

                if (_currentTarget != null && lostTargetGraceSeconds > 0f)
                {
                    _lostElapsed += safeDelta;
                    if (_lostElapsed <= lostTargetGraceSeconds)
                        return new EntryGazeDwellResult(Mathf.Clamp01(_elapsed / safeDuration), false);
                }

                ResetFocus();
                return new EntryGazeDwellResult(0f, false);
            }

            if (!interactable)
            {
                ClearTarget();
                return new EntryGazeDwellResult(0f, false);
            }

            if (_requiresGazeExit)
            {
                _lostElapsed = 0f;
                return new EntryGazeDwellResult(0f, false);
            }

            if (!ReferenceEquals(target, _currentTarget))
            {
                _currentTarget = target;
                _elapsed = 0f;
                _lostElapsed = 0f;
                _activationLocked = false;
            }
            else
            {
                _lostElapsed = 0f;
            }

            if (_activationLocked)
                return new EntryGazeDwellResult(0f, false);

            _elapsed += safeDelta;
            var progress = Mathf.Clamp01(_elapsed / safeDuration);
            var activated = progress >= 1f;
            if (activated) _activationLocked = true;
            return new EntryGazeDwellResult(progress, activated);
        }

        public void ClearTarget(bool requireGazeExit = false)
        {
            // Surface invalidation cancels progress but must not rearm a retained gaze.
            _requiresGazeExit |= requireGazeExit || _currentTarget != null;
            ResetFocus();
        }

        public void Reset()
        {
            _requiresGazeExit = false;
            ResetFocus();
        }

        void ResetFocus()
        {
            _currentTarget = null;
            _elapsed = 0f;
            _lostElapsed = 0f;
            _activationLocked = false;
        }
    }
}
