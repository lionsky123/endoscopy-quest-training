using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public readonly struct SceneId : IEquatable<SceneId>
    {
        readonly string _value;

        public SceneId(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("SceneId must not contain leading or trailing whitespace.", nameof(value));
            if (!IsCanonical(value))
                throw new ArgumentException("SceneId must be lowercase snake_case and start with a letter.", nameof(value));

            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsValid => _value != null;

        public bool Equals(SceneId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SceneId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public override string ToString() => Value;

        public static bool operator ==(SceneId left, SceneId right) => left.Equals(right);
        public static bool operator !=(SceneId left, SceneId right) => !left.Equals(right);

        static bool IsCanonical(string value)
        {
            if (value.Length == 0 || value[0] < 'a' || value[0] > 'z')
                return false;

            var previousWasUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                var isLowercaseLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isUnderscore = character == '_';
                if (!isLowercaseLetter && !isDigit && !isUnderscore)
                    return false;
                if (isUnderscore && previousWasUnderscore)
                    return false;

                previousWasUnderscore = isUnderscore;
            }

            return !previousWasUnderscore;
        }
    }
}
