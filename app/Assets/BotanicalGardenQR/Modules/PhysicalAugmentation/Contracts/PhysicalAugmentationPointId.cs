using System;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public readonly struct PhysicalAugmentationPointId : IEquatable<PhysicalAugmentationPointId>, IComparable<PhysicalAugmentationPointId>
    {
        readonly string _value;

        public PhysicalAugmentationPointId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A physical augmentation point ID is required.", nameof(value));
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A physical augmentation point ID must be canonical.", nameof(value));
            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(_value);

        public int CompareTo(PhysicalAugmentationPointId other)
            => string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(PhysicalAugmentationPointId other)
            => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is PhysicalAugmentationPointId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static bool operator ==(PhysicalAugmentationPointId left, PhysicalAugmentationPointId right) => left.Equals(right);
        public static bool operator !=(PhysicalAugmentationPointId left, PhysicalAugmentationPointId right) => !left.Equals(right);
    }
}
