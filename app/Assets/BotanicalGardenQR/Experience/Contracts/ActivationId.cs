using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public readonly struct ActivationId : IEquatable<ActivationId>
    {
        readonly Guid _value;

        public ActivationId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("ActivationId must not be empty.", nameof(value));
            _value = value;
        }

        public Guid Value => _value;
        public bool IsValid => _value != Guid.Empty;
        public static ActivationId CreateNew() => new ActivationId(Guid.NewGuid());

        public bool Equals(ActivationId other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is ActivationId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString("N");
        public static bool operator ==(ActivationId left, ActivationId right) => left.Equals(right);
        public static bool operator !=(ActivationId left, ActivationId right) => !left.Equals(right);
    }
}
