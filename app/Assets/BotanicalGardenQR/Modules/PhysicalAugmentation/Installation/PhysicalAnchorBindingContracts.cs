using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Installation
{
    public static class PhysicalAnchorPoseResolver
    {
        public static Pose Resolve(Pose anchorWorldPose, Pose augmentationPoseInAnchorSpace)
            => PhysicalAugmentationPose.Resolve(anchorWorldPose, augmentationPoseInAnchorSpace);
    }

    public enum PhysicalAnchorRecordStatus
    {
        Unassigned,
        Assigned,
        PendingErase
    }

    public static class PhysicalAnchorDisplayIdentity
    {
        public const int MaximumCustomNameLength = 24;

        public static string NormalizeCustomName(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (normalized.Length > MaximumCustomNameLength)
                throw new ArgumentException(
                    $"An anchor custom name cannot exceed {MaximumCustomNameLength} characters.",
                    nameof(value));
            for (var index = 0; index < normalized.Length; index++)
                if (char.IsControl(normalized[index]))
                    throw new ArgumentException("An anchor custom name cannot contain control characters.", nameof(value));
            return normalized;
        }

        public static string Format(int displayNumber, string customName)
        {
            if (displayNumber <= 0) throw new ArgumentOutOfRangeException(nameof(displayNumber));
            var prefix = $"锚点 {displayNumber:D2}";
            var normalized = NormalizeCustomName(customName);
            return normalized.Length == 0 ? prefix : $"{prefix} · {normalized}";
        }
    }

    public readonly struct PhysicalAnchorRecord
    {
        public PhysicalAnchorRecord(
            Guid uuid,
            PhysicalAnchorRecordStatus status,
            DateTimeOffset maintainedAt,
            int displayNumber,
            string customName)
        {
            if (uuid == Guid.Empty) throw new ArgumentException("An anchor UUID is required.", nameof(uuid));
            if (!Enum.IsDefined(typeof(PhysicalAnchorRecordStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (displayNumber <= 0) throw new ArgumentOutOfRangeException(nameof(displayNumber));
            Uuid = uuid;
            Status = status;
            MaintainedAt = maintainedAt.ToUniversalTime();
            DisplayNumber = displayNumber;
            CustomName = PhysicalAnchorDisplayIdentity.NormalizeCustomName(customName);
        }

        public Guid Uuid { get; }
        public PhysicalAnchorRecordStatus Status { get; }
        public DateTimeOffset MaintainedAt { get; }
        public int DisplayNumber { get; }
        public string CustomName { get; }
        public string DisplayLabel => PhysicalAnchorDisplayIdentity.Format(DisplayNumber, CustomName);
    }

    public readonly struct PhysicalAnchorBinding
    {
        public PhysicalAnchorBinding(
            PhysicalAugmentationPointId pointId,
            Guid anchorUuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long calibrationRevision,
            DateTimeOffset confirmedAt)
        {
            if (!pointId.IsValid) throw new ArgumentException("A valid point ID is required.", nameof(pointId));
            if (anchorUuid == Guid.Empty) throw new ArgumentException("An anchor UUID is required.", nameof(anchorUuid));
            if (!IsFinite(augmentationPoseInAnchorSpace.position) || !IsFinite(augmentationPoseInAnchorSpace.rotation) ||
                QuaternionMagnitudeSquared(augmentationPoseInAnchorSpace.rotation) < 0.0001f)
                throw new ArgumentException("The augmentation pose must be finite and have a valid rotation.", nameof(augmentationPoseInAnchorSpace));
            if (!(uniformScale > 0f) || float.IsNaN(uniformScale) || float.IsInfinity(uniformScale))
                throw new ArgumentOutOfRangeException(nameof(uniformScale));
            if (calibrationRevision <= 0) throw new ArgumentOutOfRangeException(nameof(calibrationRevision));
            PointId = pointId;
            AnchorUuid = anchorUuid;
            AugmentationPoseInAnchorSpace = new Pose(
                augmentationPoseInAnchorSpace.position,
                Normalize(augmentationPoseInAnchorSpace.rotation));
            UniformScale = uniformScale;
            CalibrationRevision = calibrationRevision;
            ConfirmedAt = confirmedAt.ToUniversalTime();
        }

        public PhysicalAugmentationPointId PointId { get; }
        public Guid AnchorUuid { get; }
        public Pose AugmentationPoseInAnchorSpace { get; }
        public float UniformScale { get; }
        public long CalibrationRevision { get; }

        public Pose ResolveWorldPose(Pose anchorWorldPose)
            => PhysicalAnchorPoseResolver.Resolve(anchorWorldPose, AugmentationPoseInAnchorSpace);
        public DateTimeOffset ConfirmedAt { get; }

        static Quaternion Normalize(Quaternion value)
        {
            var magnitude = Mathf.Sqrt(QuaternionMagnitudeSquared(value));
            return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
        }

        static float QuaternionMagnitudeSquared(Quaternion value)
            => value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;

        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct PhysicalAnchorConfiguredBinding
    {
        public PhysicalAnchorConfiguredBinding(
            PhysicalAugmentationPointId pointId,
            int anchorDisplayNumber)
        {
            if (!pointId.IsValid) throw new ArgumentException("A valid point ID is required.", nameof(pointId));
            if (anchorDisplayNumber <= 0) throw new ArgumentOutOfRangeException(nameof(anchorDisplayNumber));
            PointId = pointId;
            AnchorDisplayNumber = anchorDisplayNumber;
        }

        public PhysicalAugmentationPointId PointId { get; }
        public int AnchorDisplayNumber { get; }
    }

    public sealed class PhysicalAnchorBindingSnapshot
    {
        readonly ReadOnlyCollection<PhysicalAnchorRecord> _anchors;
        readonly ReadOnlyCollection<PhysicalAnchorBinding> _bindings;
        readonly Dictionary<Guid, PhysicalAnchorRecord> _anchorsByUuid;
        readonly Dictionary<PhysicalAugmentationPointId, PhysicalAnchorBinding> _bindingsByPoint;

        public PhysicalAnchorBindingSnapshot(
            long storeVersion,
            IReadOnlyList<PhysicalAnchorRecord> anchors,
            IReadOnlyList<PhysicalAnchorBinding> bindings)
        {
            if (storeVersion < 0) throw new ArgumentOutOfRangeException(nameof(storeVersion));
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            StoreVersion = storeVersion;

            var anchorCopy = new PhysicalAnchorRecord[anchors.Count];
            _anchorsByUuid = new Dictionary<Guid, PhysicalAnchorRecord>();
            var displayNumbers = new HashSet<int>();
            for (var index = 0; index < anchors.Count; index++)
            {
                var anchor = anchors[index];
                if (anchor.Uuid == Guid.Empty)
                    throw new ArgumentException("Anchor UUIDs cannot be empty.", nameof(anchors));
                if (anchor.DisplayNumber <= 0)
                    throw new ArgumentException("Anchor display numbers must be positive.", nameof(anchors));
                if (!_anchorsByUuid.TryAdd(anchor.Uuid, anchor))
                    throw new ArgumentException("Anchor UUIDs must be unique.", nameof(anchors));
                if (!displayNumbers.Add(anchor.DisplayNumber))
                    throw new ArgumentException("Anchor display numbers must be unique.", nameof(anchors));
                anchorCopy[index] = anchor;
            }

            var bindingCopy = new PhysicalAnchorBinding[bindings.Count];
            _bindingsByPoint = new Dictionary<PhysicalAugmentationPointId, PhysicalAnchorBinding>();
            var boundUuids = new HashSet<Guid>();
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (!binding.PointId.IsValid || binding.AnchorUuid == Guid.Empty)
                    throw new ArgumentException("Bindings require valid Point IDs and anchor UUIDs.", nameof(bindings));
                if (!_bindingsByPoint.TryAdd(binding.PointId, binding))
                    throw new ArgumentException("Point IDs must have at most one active binding.", nameof(bindings));
                if (!boundUuids.Add(binding.AnchorUuid))
                    throw new ArgumentException("An anchor UUID must bind to at most one point.", nameof(bindings));
                if (!_anchorsByUuid.TryGetValue(binding.AnchorUuid, out var anchor) || anchor.Status != PhysicalAnchorRecordStatus.Assigned)
                    throw new ArgumentException("Every binding must reference an Assigned anchor record.", nameof(bindings));
                bindingCopy[index] = binding;
            }

            foreach (var anchor in anchorCopy)
                if (anchor.Status == PhysicalAnchorRecordStatus.Assigned && !boundUuids.Contains(anchor.Uuid))
                    throw new ArgumentException("Every Assigned anchor record must have one binding.", nameof(anchors));

            Array.Sort(anchorCopy, (left, right) => left.Uuid.CompareTo(right.Uuid));
            Array.Sort(bindingCopy, (left, right) => left.PointId.CompareTo(right.PointId));
            _anchors = Array.AsReadOnly(anchorCopy);
            _bindings = Array.AsReadOnly(bindingCopy);
        }

        public long StoreVersion { get; }
        public IReadOnlyList<PhysicalAnchorRecord> Anchors => _anchors;
        public IReadOnlyList<PhysicalAnchorBinding> Bindings => _bindings;

        public bool TryGetBinding(PhysicalAugmentationPointId pointId, out PhysicalAnchorBinding binding)
            => _bindingsByPoint.TryGetValue(pointId, out binding);

        public bool TryGetAnchor(Guid uuid, out PhysicalAnchorRecord anchor)
            => _anchorsByUuid.TryGetValue(uuid, out anchor);

        public static PhysicalAnchorBindingSnapshot Empty()
            => new PhysicalAnchorBindingSnapshot(0, Array.Empty<PhysicalAnchorRecord>(), Array.Empty<PhysicalAnchorBinding>());
    }

    public enum PhysicalAnchorBindingFailureCode
    {
        None,
        CorruptStore,
        StorageUnavailable,
        StaleVersion,
        DuplicateAnchor,
        DuplicatePoint,
        AnchorNotFound,
        AnchorNotUnassigned,
        PointNotBound,
        AnchorStillAssigned,
        InvalidMutation
    }

    public readonly struct PhysicalAnchorBindingReadResult
    {
        PhysicalAnchorBindingReadResult(bool succeeded, PhysicalAnchorBindingSnapshot snapshot, PhysicalAnchorBindingFailureCode code, string tag)
        {
            Succeeded = succeeded;
            Snapshot = snapshot;
            FailureCode = code;
            DiagnosticTag = tag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public PhysicalAnchorBindingSnapshot Snapshot { get; }
        public PhysicalAnchorBindingFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }

        public static PhysicalAnchorBindingReadResult Success(PhysicalAnchorBindingSnapshot snapshot)
            => new PhysicalAnchorBindingReadResult(true, snapshot ?? throw new ArgumentNullException(nameof(snapshot)), PhysicalAnchorBindingFailureCode.None, string.Empty);

        public static PhysicalAnchorBindingReadResult Failure(PhysicalAnchorBindingFailureCode code, string tag)
            => new PhysicalAnchorBindingReadResult(false, null, code, tag);
    }

    public readonly struct PhysicalAnchorBindingMutationResult
    {
        PhysicalAnchorBindingMutationResult(bool succeeded, long storeVersion, PhysicalAnchorBindingFailureCode code, string tag)
        {
            Succeeded = succeeded;
            StoreVersion = storeVersion;
            FailureCode = code;
            DiagnosticTag = tag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public long StoreVersion { get; }
        public PhysicalAnchorBindingFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }

        public static PhysicalAnchorBindingMutationResult Success(long storeVersion)
            => new PhysicalAnchorBindingMutationResult(true, storeVersion, PhysicalAnchorBindingFailureCode.None, string.Empty);

        public static PhysicalAnchorBindingMutationResult Failure(PhysicalAnchorBindingFailureCode code, string tag, long storeVersion)
            => new PhysicalAnchorBindingMutationResult(false, storeVersion, code, tag);
    }

    public interface IPhysicalAnchorBindingReader
    {
        PhysicalAnchorBindingReadResult Read();
    }

    public interface IPhysicalAnchorBindingWriter
    {
        PhysicalAnchorBindingMutationResult RegisterUnassigned(Guid uuid, long expectedStoreVersion, DateTimeOffset maintainedAt);
        PhysicalAnchorBindingMutationResult RegisterAndAssign(
            PhysicalAugmentationPointId pointId,
            Guid uuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt);
        PhysicalAnchorBindingMutationResult Assign(
            PhysicalAugmentationPointId pointId,
            Guid uuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt);
        PhysicalAnchorBindingMutationResult SwapBinding(
            PhysicalAugmentationPointId pointId,
            Guid newUuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt);
        PhysicalAnchorBindingMutationResult Recalibrate(
            PhysicalAugmentationPointId pointId,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt);
        PhysicalAnchorBindingMutationResult RenameAnchor(
            Guid uuid,
            string customName,
            long expectedStoreVersion,
            DateTimeOffset maintainedAt);
        PhysicalAnchorBindingMutationResult ReplaceUnassignedIdentity(
            Guid oldUuid,
            Guid newUuid,
            int stableDisplayNumber,
            string stableCustomName,
            long expectedStoreVersion,
            DateTimeOffset maintainedAt);
        PhysicalAnchorBindingMutationResult ReconcileConfiguredBindings(
            IReadOnlyList<PhysicalAnchorConfiguredBinding> configuredBindings,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt);
        PhysicalAnchorBindingMutationResult Unassign(Guid uuid, long expectedStoreVersion, DateTimeOffset maintainedAt);
        PhysicalAnchorBindingMutationResult RemoveUnassigned(Guid uuid, long expectedStoreVersion);
    }
}
