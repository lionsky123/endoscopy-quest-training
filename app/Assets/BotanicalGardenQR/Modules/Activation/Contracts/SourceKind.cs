using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    public readonly struct SourceKind : IEquatable<SourceKind>
    {
        readonly string _value;

        public SourceKind(string value)
        {
            if (!IsCanonical(value))
                throw new ArgumentException("SourceKind must be lowercase snake_case and start with a letter.", nameof(value));
            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsValid => _value != null;

        public bool Equals(SourceKind other)
            => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SourceKind other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;
        public static bool operator ==(SourceKind left, SourceKind right) => left.Equals(right);
        public static bool operator !=(SourceKind left, SourceKind right) => !left.Equals(right);

        static bool IsCanonical(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
                return false;

            var previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                var valid = character >= 'a' && character <= 'z' ||
                            character >= '0' && character <= '9' ||
                            character == '_';
                if (!valid || character == '_' && previousUnderscore)
                    return false;
                previousUnderscore = character == '_';
            }

            return !previousUnderscore;
        }
    }

    public static class RecognitionSourceKinds
    {
        public static readonly SourceKind Qr = new SourceKind("qr");
        public static readonly SourceKind Fieldbook = new SourceKind("fieldbook");
    }
}
