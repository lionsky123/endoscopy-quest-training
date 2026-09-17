using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public readonly struct SessionToken : IEquatable<SessionToken>
    {
        readonly Guid _value;

        public SessionToken(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("SessionToken must not be empty.", nameof(value));
            _value = value;
        }

        public Guid Value => _value;
        public bool IsValid => _value != Guid.Empty;
        public static SessionToken CreateNew() => new SessionToken(Guid.NewGuid());

        public bool Equals(SessionToken other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is SessionToken other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString("N");
        public static bool operator ==(SessionToken left, SessionToken right) => left.Equals(right);
        public static bool operator !=(SessionToken left, SessionToken right) => !left.Equals(right);
    }
}
