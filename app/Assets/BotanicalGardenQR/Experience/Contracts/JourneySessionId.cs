using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public readonly struct JourneySessionId : IEquatable<JourneySessionId>
    {
        readonly Guid _value;

        public JourneySessionId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("JourneySessionId must not be empty.", nameof(value));
            _value = value;
        }

        public Guid Value => _value;
        public bool IsValid => _value != Guid.Empty;
        public static JourneySessionId CreateNew() => new JourneySessionId(Guid.NewGuid());

        public bool Equals(JourneySessionId other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is JourneySessionId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => _value.ToString("N");
        public static bool operator ==(JourneySessionId left, JourneySessionId right) => left.Equals(right);
        public static bool operator !=(JourneySessionId left, JourneySessionId right) => !left.Equals(right);
    }
}
