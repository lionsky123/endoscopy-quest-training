using System;
using BotanicalGardenQR.Fairy.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    internal sealed class FairyOrbitDriver : MonoBehaviour
    {
        const float FollowDistance = 1.25f;
        const float AirborneHeight = 0.35f;
        const float OrbitDegreesPerSecond = 32f;
        const float PositionResponsiveness = 6f;
        const float HoverAmplitude = 0.035f;
        const float HoverAngularFrequency = 2.4f;
        const float WalkingDistanceThreshold = 0.045f;
        const float TurnDegreesPerSecond = 240f;
        const float DepartureAngle = 10f;

        Transform _viewer;
        Transform _groundReference;
        FairyBehavior _behavior;
        Action<bool> _movementChanged;
        Action<float> _speedChanged;
        FairyCompanionCue _cue = FairyCompanionCue.Idle;
        Vector3 _baseScale;
        float _orbitAngle;
        float _cueEndsAt;
        bool _walking;
        bool _enlarged;
        long _motionRequest;
        Vector3 _motionPosition, _motionForward;
        bool _motionMoving;
        bool _motionAcquiring;
        float _motionSpeed;
        bool _motionSuspended;
        bool _motionTurning;
        long _lastMotionRequest;
        long _releasedMotionRequest;
        float _recallRemaining;
        Vector3 _recallPosition;
        internal bool TryRecall(Vector3 position)
        {
            if (!isActiveAndEnabled || !Finite(position) || _cue.Kind != FairyCompanionCueKind.Idle || _recallRemaining > 0) return false;
            _recallPosition = position; _recallRemaining = .36f;
            _motionMoving = false; _motionAcquiring = false; _motionTurning = false;
            SetWalking(false);
            return true;
        }
        internal bool ApplyMotion(long requestId, Vector3 position, Vector3 forward, bool moving, float entryRadius, float speed)
        {
            if (requestId <= 0 || requestId < _lastMotionRequest || requestId <= _releasedMotionRequest ||
                !isActiveAndEnabled || _recallRemaining > 0 || _cue.Kind != FairyCompanionCueKind.Idle ||
                !Finite(position) || !Finite(forward) || !(speed > 0) || !(entryRadius > 0)) return false;
            _lastMotionRequest = requestId;
            _motionSuspended = false;
            if (_motionRequest != requestId)
            {
                // A new leg waits for the previous navigation pose to be restored after a cue.
                if (_motionRequest > 0 && _motionAcquiring) return false;
                if (Vector3.ProjectOnPlane(transform.position - position, Vector3.up).magnitude > entryRadius) return false;
                _motionRequest = requestId;
                _lastMotionRequest = requestId;
                _motionPosition = position;
                _motionSpeed = speed;
                _motionAcquiring = Vector3.Distance(transform.position, position) > 0.001f;
            }
            if (_motionAcquiring) return false;
            var distance = Vector3.Distance(transform.position, position);
            if (!moving && distance > 0.001f)
            {
                // The initial stationary request may acquire the nearby authored route start.
                // The route's entry radius is horizontal; a temporary dialogue height must
                // not permanently prevent the same Guide from returning to the floor.
                if (Vector3.ProjectOnPlane(transform.position - position, Vector3.up).magnitude > entryRadius) return false;
                _motionPosition = position;
                _motionSpeed = speed;
                _motionAcquiring = true;
                return false;
            }
            if (distance > Mathf.Max(0.05f, speed * 0.15f)) return false;
            _motionForward = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (_motionForward.sqrMagnitude < 0.000001f) _motionForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (moving && !FacingMovement(_motionForward))
            {
                // Do not accept a route sample until the body can walk towards it.
                // The caller retains its route progress while LateUpdate turns in place.
                _motionTurning = true;
                _motionMoving = false;
                SetWalking(false);
                return false;
            }
            _motionTurning = false;
            _motionPosition = position;
            _motionMoving = moving;
            transform.position = position;
            SetWalking(moving);
            return true;
        }

        bool FacingMovement(Vector3 direction)
            => direction.sqrMagnitude < 0.000001f ||
               Vector3.Angle(transform.forward, direction) <= DepartureAngle;

        void FaceMovement(Vector3 direction, float deltaSeconds)
        {
            if (direction.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), TurnDegreesPerSecond * deltaSeconds);
        }
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        internal void HoldMotion(long requestId)
        {
            // A pending successor can pause restoration before it acquires the driver.
            if (requestId >= _lastMotionRequest && requestId > _releasedMotionRequest)
            {
                _lastMotionRequest = requestId;
                _motionMoving = false;
                _motionSuspended = true;
                _motionTurning = false;
                SetWalking(false);
            }
        }
        internal void ReleaseMotion(long requestId)
        {
            if (requestId < _lastMotionRequest) return;
            _lastMotionRequest = requestId;
            _releasedMotionRequest = Math.Max(_releasedMotionRequest, requestId);
            _motionRequest = 0;
            _motionAcquiring = false;
            _motionTurning = false;
            SetWalking(false);
        }


        internal void Initialize(
            Transform viewer,
            Transform groundReference,
            FairyBehavior behavior,
            Action<bool> movementChanged = null, Action<float> speedChanged = null)
        {
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _groundReference = groundReference != null ? groundReference : viewer;
            _behavior = behavior;
            _movementChanged = movementChanged;
            _speedChanged = speedChanged;
            _baseScale = transform.localScale;
            SnapToTarget();
        }

        internal void SetEnlarged(bool enlarged)
        {
            _enlarged = enlarged;
            transform.localScale = TargetScale();
        }

        internal bool IsAtCueTarget => !_walking &&
            Vector3.ProjectOnPlane(TargetPosition() - transform.position, Vector3.up).sqrMagnitude <=
            WalkingDistanceThreshold * WalkingDistanceThreshold;

        internal void PresentCue(FairyCompanionCue cue)
        {
            if (_motionRequest > 0 && cue.Kind != FairyCompanionCueKind.Idle)
            {
                _motionMoving = false;
                _motionAcquiring = true;
            }
            // Only a fresh motion sample may resume restoration; cue expiry cannot override a Hold.
            _cue = cue;
            _cueEndsAt = cue.Kind == FairyCompanionCueKind.Idle || cue.DurationSeconds <= 0f
                ? float.PositiveInfinity
                : Time.unscaledTime + cue.DurationSeconds;
        }

        void LateUpdate() { Tick(Time.unscaledDeltaTime); }

        internal void Tick(float deltaSeconds)
        {
            if (_viewer == null) return;
            deltaSeconds = Mathf.Clamp(deltaSeconds, 0f, 0.1f);
            if (_recallRemaining > 0)
            {
                var previous = _recallRemaining;
                _recallRemaining = Mathf.Max(0, _recallRemaining - deltaSeconds);
                if (previous > .18f && _recallRemaining <= .18f)
                {
                    transform.position = _recallPosition;
                    _motionPosition = _recallPosition;
                    _motionRequest = 0;
                }
                transform.localScale = TargetScale() * Mathf.Clamp01(Mathf.Abs(_recallRemaining - .18f) / .18f);
                return;
            }
            if (_cue.Kind != FairyCompanionCueKind.Idle && Time.unscaledTime >= _cueEndsAt)
                PresentCue(FairyCompanionCue.Idle);
            if (_motionRequest > 0 && _cue.Kind == FairyCompanionCueKind.Idle)
            {
                if (_motionAcquiring && !_motionSuspended)
                {
                    var direction = Vector3.ProjectOnPlane(_motionPosition - transform.position, Vector3.up);
                    FaceMovement(direction, deltaSeconds);
                    var next = FacingMovement(direction)
                        ? Vector3.MoveTowards(transform.position, _motionPosition, _motionSpeed * deltaSeconds)
                        : transform.position;
                    SetWalking(Vector3.Distance(next, transform.position) > 0.00001f);
                    transform.position = next;
                    _motionAcquiring = Vector3.Distance(next, _motionPosition) > 0.001f;
                    if (!_motionAcquiring) SetWalking(false);
                }
                else
                {
                    if (!_motionSuspended) FaceMovement(_motionForward, deltaSeconds);
                    SetWalking(_motionMoving && !_motionTurning);
                }
                transform.localScale = Vector3.Lerp(transform.localScale, TargetScale(), 1f - Mathf.Exp(-PositionResponsiveness * deltaSeconds));
                return;
            }
            var orbitSpeed = _cue.Kind == FairyCompanionCueKind.Celebrate
                ? OrbitDegreesPerSecond * 4.2f
                : OrbitDegreesPerSecond;
            if (_behavior == FairyBehavior.Orbit || _cue.Kind == FairyCompanionCueKind.Celebrate)
                _orbitAngle += orbitSpeed * deltaSeconds;

            var target = TargetPosition();
            var targetDelta = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            var needsWalk = targetDelta.sqrMagnitude > WalkingDistanceThreshold * WalkingDistanceThreshold;
            if (_behavior == FairyBehavior.Guide && needsWalk) FaceMovement(targetDelta, deltaSeconds);
            SetWalking(_behavior == FairyBehavior.Guide &&
                       needsWalk && FacingMovement(targetDelta));
            transform.position = _behavior == FairyBehavior.Guide
                ? Vector3.MoveTowards(transform.position, target, ((_walking || !needsWalk) ? (_motionSpeed > 0 ? _motionSpeed : 0.7f) : 0f) * deltaSeconds)
                : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-PositionResponsiveness * deltaSeconds));
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                TargetScale(),
                1f - Mathf.Exp(-PositionResponsiveness * deltaSeconds));

            var lookDirection = _walking
                ? targetDelta
                : Vector3.ProjectOnPlane(_viewer.position - transform.position, Vector3.up);
            if (lookDirection.sqrMagnitude <= 0.001f) return;
            if (needsWalk && _behavior == FairyBehavior.Guide) return;
            var targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-PositionResponsiveness * deltaSeconds));
        }

        internal void SnapToTarget()
        {
            transform.position = TargetPosition();
            transform.localScale = TargetScale();
            var lookDirection = Vector3.ProjectOnPlane(_viewer.position - transform.position, Vector3.up);
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            SetWalking(false);
        }

        Vector3 TargetPosition()
        {
            if (_behavior == FairyBehavior.Guide)
                return GroundTargetPosition();

            // Airborne model sources retain the source-independent orbit motion.
            var hover = Mathf.Sin(Time.unscaledTime * HoverAngularFrequency) * HoverAmplitude;
            if (_cue.Kind != FairyCompanionCueKind.Idle)
            {
                var viewerRight = Vector3.ProjectOnPlane(_viewer.right, Vector3.up).normalized;
                if (viewerRight.sqrMagnitude < 0.001f) viewerRight = Vector3.right;
                switch (_cue.Kind)
                {
                    case FairyCompanionCueKind.ArtifactWait:
                        return _cue.WorldPosition + Vector3.up * (0.18f + hover) + viewerRight * 0.16f;
                    case FairyCompanionCueKind.ArtifactReturn:
                        return _cue.WorldPosition + Vector3.up * (0.24f + hover) - viewerRight * 0.12f;
                    case FairyCompanionCueKind.Celebrate:
                        var orbit = Quaternion.AngleAxis(_orbitAngle, Vector3.up) * Vector3.forward * 0.25f;
                        var bob = Mathf.Sin(Time.unscaledTime * 8f) * 0.045f;
                        return _cue.WorldPosition + orbit + Vector3.up * (0.3f + bob);
                    case FairyCompanionCueKind.CoachAttention:
                        break;
                    case FairyCompanionCueKind.DialogueFocus:
                        return _cue.WorldPosition;
                }
            }

            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            var offset = Quaternion.AngleAxis(_orbitAngle, Vector3.up) * forward * FollowDistance;
            return _viewer.position + offset + Vector3.up * (AirborneHeight + hover);
        }

        Vector3 GroundTargetPosition()
        {
            var viewerRight = Vector3.ProjectOnPlane(_viewer.right, Vector3.up).normalized;
            if (viewerRight.sqrMagnitude < 0.001f) viewerRight = Vector3.right;

            Vector3 target;
            switch (_cue.Kind)
            {
                case FairyCompanionCueKind.DialogueFocus:
                    target = _cue.WorldPosition;
                    break;
                case FairyCompanionCueKind.ArtifactWait:
                    target = _cue.WorldPosition + viewerRight * 0.42f;
                    break;
                case FairyCompanionCueKind.ArtifactReturn:
                    target = _cue.WorldPosition - viewerRight * 0.34f;
                    break;
                case FairyCompanionCueKind.Celebrate:
                    target = _cue.WorldPosition + viewerRight * 0.48f;
                    break;
                case FairyCompanionCueKind.CoachAttention:
                default:
                    var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                    target = _viewer.position + forward * FollowDistance;
                    break;
            }

            // Visitor uses Floor-level tracking. The explicitly injected XR rig root is
            // therefore the stable floor-height authority even when the head moves.
            target.y = _groundReference.position.y;
            return target;
        }

        Vector3 TargetScale()
            => _baseScale * (_enlarged ? 1.5f : 1f);

        void SetWalking(bool walking)
        {
            _speedChanged?.Invoke(walking ? (_motionSpeed > 0 ? _motionSpeed : 0.7f) : 0f);
            if (_walking == walking) return;
            _walking = walking;
            _movementChanged?.Invoke(walking);
        }

        void OnDisable() { SetWalking(false); }

        void OnDestroy()
        {
            SetWalking(false);
            _movementChanged = null;
            _speedChanged = null;
        }
    }
}
