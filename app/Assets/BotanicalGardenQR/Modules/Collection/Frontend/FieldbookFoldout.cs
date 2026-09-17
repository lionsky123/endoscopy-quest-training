using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.Collection.Frontend
{
    /// <summary>A single SDK transformer drives hinged paper, never a free-moving reward object.</summary>
    public sealed class FieldbookFoldout : MonoBehaviour, ITransformer
    {
        [SerializeField] Transform _leftPage, _rightPage, _leftGrip, _rightGrip, _detail;
        [SerializeField] Transform _labels;
        [SerializeField] Renderer[] _pictures = Array.Empty<Renderer>();
        [SerializeField] Renderer _detailPicture;
        [SerializeField] HandGrabInteractable _handGrab;
        [SerializeField, Min(.05f)] float _halfWidth = .14f;
        [SerializeField, Range(30, 85)] float _foldedAngle = 78f;
        [SerializeField, Range(.5f, 1)] float _openThreshold = .85f;
        [SerializeField, Range(0, .4f)] float _closeThreshold = .15f;
        [SerializeField, Min(.05f)] float _stableSeconds = .2f;
        IGrabbable _grabbable;
        FoldoutGesture _gesture;
        float _left, _anchor, _pulse, _lastProgress = -1;
        int _side = 1;
        bool _active, _notified;
        IHand[] _hands = Array.Empty<IHand>();
        bool _touchInside;
        public event Action Closed;
        public event Action Changed;
        public event Action Touched;
        public float Progress => _gesture?.Progress ?? 0;
        public bool Revealed => _gesture?.Revealed == true;
        public bool IsHeld => _gesture?.IsHeld == true;
        public Vector3 PartnerPosition => (_side > 0 ? _leftGrip : _rightGrip).position;
        public Vector3 DetailPosition => _detail.position;
        float ClosedSpan => 2 * _halfWidth * Mathf.Cos(_foldedAngle * Mathf.Deg2Rad);
        float Travel => 2 * _halfWidth - ClosedSpan;

        public void Configure(Texture2D picture, Texture2D detailPicture = null)
        {
            _gesture = new FoldoutGesture(Travel, _openThreshold, _closeThreshold, _stableSeconds);
            _left = -ClosedSpan / 2; _side = 1; _notified = false; _lastProgress = -1; _touchInside = false;
            var block = new MaterialPropertyBlock();
            if (picture != null) { block.SetTexture("_BaseMap", picture); block.SetTexture("_MainTex", picture); }
            foreach (var renderer in _pictures) renderer.SetPropertyBlock(block);
            if (detailPicture != null) { block.SetTexture("_BaseMap", detailPicture); block.SetTexture("_MainTex", detailPicture); }
            if (_detailPicture != null) _detailPicture.SetPropertyBlock(block);
            Present(0);
        }
        public void SetActiveInput(bool active) { _active = active; if (!active) _gesture?.End(); }
        public void RetryAfterRejectedClose()
        { _gesture?.RetryAfterRejectedClose(); _notified = false; _active = true; Changed?.Invoke(); }
        public void BindHands(IHand[] hands) { _hands = hands ?? Array.Empty<IHand>(); }
        public void Initialize(IGrabbable grabbable) { _grabbable = grabbable; }
        public void BeginTransform()
        {
            if (!_active || _gesture == null || _grabbable.GrabPoints.Count != 1) return;
            var point = transform.InverseTransformPoint(_grabbable.GrabPoints[0].position);
            var span = ClosedSpan + Travel * Progress;
            _side = point.x < _left + span / 2 ? -1 : 1;
            _anchor = _side > 0 ? _left : _left + span;
            _gesture.Begin(Mathf.Clamp(point.x, -.4f, .4f), _side);
            Changed?.Invoke();
        }
        public void UpdateTransform()
        {
            if (!_active || _gesture == null || !_gesture.IsHeld || _grabbable.GrabPoints.Count != 1) return;
            bool tracked = false;
            foreach (var interactor in _handGrab.SelectingInteractors)
                tracked |= interactor.Hand != null && interactor.Hand.IsTrackedDataValid && interactor.Hand.IsHighConfidence;
            var point = transform.InverseTransformPoint(_grabbable.GrabPoints[0].position);
            var wasRevealed = Revealed;
            tracked &= Mathf.Abs(point.y) <= .3f && Mathf.Abs(point.z) <= .35f;
            var complete = _gesture.Move(Mathf.Clamp(point.x, -.4f, .4f), Time.unscaledDeltaTime, tracked);
            var span = ClosedSpan + Travel * Progress;
            _left = _side > 0 ? _anchor : _anchor - span;
            Present(Progress);
            if (wasRevealed != Revealed) Changed?.Invoke();
            if (complete && !_notified) { _notified = true; Closed?.Invoke(); }
        }
        public void EndTransform() { _gesture?.End(); Changed?.Invoke(); }
        void OnDisable() { _active = false; _gesture?.End(); }
        void Update()
        {
            if (_pulse > 0) { _pulse = Mathf.Max(0, _pulse - Time.unscaledDeltaTime); Present(Progress); }
            if (!_active || !Revealed || !_detail.gameObject.activeInHierarchy) return;
            bool inside = false;
            foreach (var hand in _hands)
                if (hand != null && hand.IsTrackedDataValid && hand.IsHighConfidence &&
                    hand.GetJointPose(HandJointId.HandIndexTip, out var tip))
                    inside |= Vector3.Distance(tip.position, _detail.position) < (_touchInside ? .095f : .07f);
            if (inside && !_touchInside) Touch();
            _touchInside = inside;
        }
        public void Touch()
        {
            if (!_active || !Revealed || _pulse > .3f) return;
            _pulse = .8f; Touched?.Invoke();
        }
        // Used by the existing Editor presentation capture, without fabricating a grab/domain event.
        public void Preview(float progress, bool touched = false)
        { _left = -ClosedSpan / 2; _pulse = touched ? .6f : 0; Present(Mathf.Clamp01(progress)); }
        void Present(float progress)
        {
            var span = ClosedSpan + Travel * progress;
            var angle = Mathf.Acos(Mathf.Clamp01(span / (2 * _halfWidth))) * Mathf.Rad2Deg;
            var depth = Mathf.Sin(angle * Mathf.Deg2Rad) * _halfWidth;
            _leftPage.localPosition = new Vector3(_left + span / 4, 0, -depth / 2);
            _leftPage.localRotation = Quaternion.Euler(0, angle, 0);
            _rightPage.localPosition = new Vector3(_left + 3 * span / 4, 0, -depth / 2);
            _rightPage.localRotation = Quaternion.Euler(0, -angle, 0);
            _leftGrip.localPosition = new Vector3(_left, 0, 0);
            _rightGrip.localPosition = new Vector3(_left + span, 0, 0);
            if (_labels != null) _labels.localPosition = new Vector3(_left + span / 2, 0, 0);
            var reveal = Mathf.Clamp01((progress - .6f) / .3f);
            _detail.gameObject.SetActive(reveal > 0);
            _detail.localScale = Vector3.one * reveal;
            _detail.localPosition = new Vector3(_left + span / 2, .025f, -.025f - .07f * reveal);
            _detail.localRotation = Quaternion.Euler(Mathf.Sin(_pulse * 30) * _pulse * 18, Mathf.Sin(_pulse * 22) * _pulse * 25, 0);
            if (!Mathf.Approximately(_lastProgress, progress)) { _lastProgress = progress; Changed?.Invoke(); }
        }
    }
}
