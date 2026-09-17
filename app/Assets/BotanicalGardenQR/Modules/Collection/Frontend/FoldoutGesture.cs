using System;

namespace BotanicalGardenQR.Collection.Frontend
{
    /// <summary>Presentation gesture only. Collection remains the owner of durable completion.</summary>
    public sealed class FoldoutGesture
    {
        readonly float _travel, _open, _close, _stableSeconds;
        float _startAxis, _startProgress, _lastAxis, _stable;
        int _side;
        public float Progress { get; private set; }
        public bool IsHeld { get; private set; }
        public bool Revealed { get; private set; }
        public bool Completed { get; private set; }
        public FoldoutGesture(float travel, float open = .85f, float close = .15f, float stableSeconds = .2f)
        {
            if (!Finite(travel) || travel <= 0 || !Finite(open) || !Finite(close) || close < 0 || open > 1 || close >= open ||
                !Finite(stableSeconds) || stableSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(travel));
            _travel = travel; _open = open; _close = close; _stableSeconds = stableSeconds;
        }
        public void Begin(float axis, int side)
        {
            if (!Finite(axis) || Completed || (side != -1 && side != 1)) return;
            _startAxis = _lastAxis = axis; _startProgress = Progress; _side = side; _stable = 0; IsHeld = true;
        }
        public bool Move(float axis, float dt, bool tracked)
        {
            if (!IsHeld || Completed) return false;
            if (!tracked || !Finite(axis) || !Finite(dt) || dt < 0 || Math.Abs(axis - _lastAxis) > .12f)
            { End(); return false; }
            _lastAxis = axis;
            Progress = Math.Max(0, Math.Min(1, _startProgress + (axis - _startAxis) * _side / _travel));
            bool atBoundary = Revealed ? Progress <= _close : Progress >= _open;
            _stable = atBoundary ? _stable + Math.Min(dt, .05f) : 0;
            if (_stable < _stableSeconds) return false;
            _stable = 0;
            if (!Revealed) { Revealed = true; return false; }
            Completed = true; IsHeld = false; return true;
        }
        public void End() { IsHeld = false; _stable = 0; }
        public void RetryAfterRejectedClose()
        { End(); Completed = false; Revealed = false; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
