using System;
using System.Collections.Generic;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Installation;
using UnityEngine;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Persistence
{
    public readonly struct PhysicalAnchorAdminConfiguredBinding
    {
        public PhysicalAnchorAdminConfiguredBinding(string pointId, int anchorDisplayNumber)
        {
            if (string.IsNullOrWhiteSpace(pointId))
                throw new ArgumentException("A point ID is required.", nameof(pointId));
            if (anchorDisplayNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(anchorDisplayNumber));
            PointId = pointId.Trim();
            AnchorDisplayNumber = anchorDisplayNumber;
        }

        public string PointId { get; }
        public int AnchorDisplayNumber { get; }
    }

    public enum PhysicalAnchorAdminRecordStatus
    {
        Unassigned,
        Assigned,
        PendingErase,
        PendingIndex
    }

    public readonly struct PhysicalAnchorAdminRecordSnapshot
    {
        public PhysicalAnchorAdminRecordSnapshot(
            Guid uuid,
            PhysicalAnchorAdminRecordStatus status,
            string pointId,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long calibrationRevision,
            int displayNumber,
            string customName)
        {
            Uuid = uuid;
            Status = status;
            PointId = pointId ?? string.Empty;
            AugmentationPoseInAnchorSpace = augmentationPoseInAnchorSpace;
            UniformScale = uniformScale;
            CalibrationRevision = calibrationRevision;
            DisplayNumber = displayNumber;
            CustomName = customName ?? string.Empty;
        }

        public Guid Uuid { get; }
        public PhysicalAnchorAdminRecordStatus Status { get; }
        public string PointId { get; }
        public Pose AugmentationPoseInAnchorSpace { get; }
        public float UniformScale { get; }
        public long CalibrationRevision { get; }
        public int DisplayNumber { get; }
        public string CustomName { get; }
        public string DisplayLabel => DisplayNumber > 0
            ? PhysicalAnchorDisplayIdentity.Format(DisplayNumber, CustomName)
            : "锚点（待写入编号）";

        public Pose ResolveAugmentationWorldPose(Pose anchorWorldPose)
            => PhysicalAnchorPoseResolver.Resolve(anchorWorldPose, AugmentationPoseInAnchorSpace);
    }

    public sealed class PhysicalAnchorAdminSnapshot
    {
        public PhysicalAnchorAdminSnapshot(long storeVersion, PhysicalAnchorAdminRecordSnapshot[] records)
        {
            StoreVersion = storeVersion;
            Records = Array.AsReadOnly(records ?? Array.Empty<PhysicalAnchorAdminRecordSnapshot>());
        }

        public long StoreVersion { get; }
        public IReadOnlyList<PhysicalAnchorAdminRecordSnapshot> Records { get; }
    }

    public readonly struct PhysicalAnchorAdminOperationResult
    {
        PhysicalAnchorAdminOperationResult(bool succeeded, bool pendingIndex, long storeVersion, string diagnosticTag)
        {
            Succeeded = succeeded;
            PendingIndex = pendingIndex;
            StoreVersion = storeVersion;
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool PendingIndex { get; }
        public long StoreVersion { get; }
        public string DiagnosticTag { get; }

        public static PhysicalAnchorAdminOperationResult Success(long storeVersion)
            => new PhysicalAnchorAdminOperationResult(true, false, storeVersion, string.Empty);

        public static PhysicalAnchorAdminOperationResult Failure(string tag, long storeVersion, bool pendingIndex = false)
            => new PhysicalAnchorAdminOperationResult(false, pendingIndex, storeVersion, tag);
    }

    /// <summary>
    /// Admin-only transaction facade. It hides the PointId and Store schema assemblies from the imported Meta sample.
    /// </summary>
    public sealed class PhysicalAnchorAdminBindingSession
    {
        readonly IPhysicalAnchorBindingReader _reader;
        readonly IPhysicalAnchorBindingWriter _writer;
        readonly Func<DateTimeOffset> _clock;
        readonly HashSet<Guid> _pendingIndex = new HashSet<Guid>();

        public PhysicalAnchorAdminBindingSession(
            IPhysicalAnchorBindingReader reader,
            IPhysicalAnchorBindingWriter writer,
            Func<DateTimeOffset> clock = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public bool TryRead(out PhysicalAnchorAdminSnapshot snapshot, out string diagnosticTag)
        {
            snapshot = null;
            diagnosticTag = string.Empty;
            var read = _reader.Read();
            if (!read.Succeeded)
            {
                diagnosticTag = read.DiagnosticTag;
                return false;
            }

            var bindingsByUuid = new Dictionary<Guid, PhysicalAnchorBinding>();
            for (var index = 0; index < read.Snapshot.Bindings.Count; index++)
            {
                var binding = read.Snapshot.Bindings[index];
                bindingsByUuid.Add(binding.AnchorUuid, binding);
            }

            var records = new List<PhysicalAnchorAdminRecordSnapshot>(
                read.Snapshot.Anchors.Count + _pendingIndex.Count);
            for (var index = 0; index < read.Snapshot.Anchors.Count; index++)
            {
                var anchor = read.Snapshot.Anchors[index];
                if (bindingsByUuid.TryGetValue(anchor.Uuid, out var binding))
                {
                    records.Add(new PhysicalAnchorAdminRecordSnapshot(
                        anchor.Uuid,
                        PhysicalAnchorAdminRecordStatus.Assigned,
                        binding.PointId.Value,
                        binding.AugmentationPoseInAnchorSpace,
                        binding.UniformScale,
                        binding.CalibrationRevision,
                        anchor.DisplayNumber,
                        anchor.CustomName));
                }
                else
                {
                    var adminStatus = anchor.Status == PhysicalAnchorRecordStatus.PendingErase
                        ? PhysicalAnchorAdminRecordStatus.PendingErase
                        : PhysicalAnchorAdminRecordStatus.Unassigned;
                    records.Add(new PhysicalAnchorAdminRecordSnapshot(
                        anchor.Uuid,
                        adminStatus,
                        string.Empty,
                        Pose.identity,
                        1f,
                        0,
                        anchor.DisplayNumber,
                        anchor.CustomName));
                }
            }

            foreach (var uuid in _pendingIndex)
                if (!ContainsUuid(records, uuid))
                    records.Add(new PhysicalAnchorAdminRecordSnapshot(
                        uuid,
                        PhysicalAnchorAdminRecordStatus.PendingIndex,
                        string.Empty,
                        Pose.identity,
                        1f,
                        0,
                        0,
                        string.Empty));
            records.Sort((left, right) => left.Uuid.CompareTo(right.Uuid));
            snapshot = new PhysicalAnchorAdminSnapshot(read.Snapshot.StoreVersion, records.ToArray());
            return true;
        }

        public bool TryGetIndexedUuids(out Guid[] uuids, out string diagnosticTag)
        {
            uuids = Array.Empty<Guid>();
            if (!TryRead(out var snapshot, out diagnosticTag)) return false;
            var result = new List<Guid>(snapshot.Records.Count);
            for (var index = 0; index < snapshot.Records.Count; index++)
            {
                var record = snapshot.Records[index];
                if (record.Status != PhysicalAnchorAdminRecordStatus.PendingIndex) result.Add(record.Uuid);
            }
            uuids = result.ToArray();
            return true;
        }

        public PhysicalAnchorAdminOperationResult RegisterSavedAnchor(Guid uuid)
        {
            if (uuid == Guid.Empty) return PhysicalAnchorAdminOperationResult.Failure("admin_binding.uuid_invalid", 0);
            var read = _reader.Read();
            if (!read.Succeeded)
            {
                _pendingIndex.Add(uuid);
                return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0, true);
            }
            if (read.Snapshot.TryGetAnchor(uuid, out _))
            {
                _pendingIndex.Remove(uuid);
                return PhysicalAnchorAdminOperationResult.Success(read.Snapshot.StoreVersion);
            }

            var result = _writer.RegisterUnassigned(uuid, read.Snapshot.StoreVersion, _clock());
            if (result.Succeeded)
            {
                _pendingIndex.Remove(uuid);
                return PhysicalAnchorAdminOperationResult.Success(result.StoreVersion);
            }
            _pendingIndex.Add(uuid);
            return PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion, true);
        }

        public PhysicalAnchorAdminOperationResult AssignOrRecalibrate(
            string pointId,
            Guid uuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale)
        {
            PhysicalAugmentationPointId parsedPointId;
            try
            {
                parsedPointId = new PhysicalAugmentationPointId(pointId);
            }
            catch (ArgumentException)
            {
                return PhysicalAnchorAdminOperationResult.Failure("admin_binding.point_invalid", 0);
            }

            var read = _reader.Read();
            if (!read.Succeeded)
                return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            if (!read.Snapshot.TryGetAnchor(uuid, out var anchor))
                return PhysicalAnchorAdminOperationResult.Failure("admin_binding.anchor_unindexed", read.Snapshot.StoreVersion);
            if (anchor.Status == PhysicalAnchorRecordStatus.Assigned &&
                (!read.Snapshot.TryGetBinding(parsedPointId, out var sameBinding) || sameBinding.AnchorUuid != uuid))
                return PhysicalAnchorAdminOperationResult.Failure("admin_binding.anchor_assigned_elsewhere", read.Snapshot.StoreVersion);

            PhysicalAnchorBindingMutationResult result;
            if (read.Snapshot.TryGetBinding(parsedPointId, out var current))
            {
                result = current.AnchorUuid == uuid
                    ? _writer.Recalibrate(parsedPointId, augmentationPoseInAnchorSpace, uniformScale, read.Snapshot.StoreVersion, _clock())
                    : _writer.SwapBinding(parsedPointId, uuid, augmentationPoseInAnchorSpace, uniformScale, read.Snapshot.StoreVersion, _clock());
            }
            else
            {
                result = _writer.Assign(parsedPointId, uuid, augmentationPoseInAnchorSpace, uniformScale, read.Snapshot.StoreVersion, _clock());
            }

            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult ReplaceUnassignedAnchorIdentity(
            Guid oldUuid,
            Guid newUuid,
            int stableDisplayNumber,
            string stableCustomName)
        {
            var read = _reader.Read();
            if (!read.Succeeded)
                return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            var result = _writer.ReplaceUnassignedIdentity(
                oldUuid,
                newUuid,
                stableDisplayNumber,
                stableCustomName,
                read.Snapshot.StoreVersion,
                _clock());
            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult ReconcileConfiguredBindings(
            IReadOnlyList<PhysicalAnchorAdminConfiguredBinding> configuredBindings)
        {
            if (configuredBindings == null)
                return PhysicalAnchorAdminOperationResult.Failure(
                    "admin_binding.configuration_missing",
                    0);
            var installationBindings = new PhysicalAnchorConfiguredBinding[configuredBindings.Count];
            try
            {
                for (var index = 0; index < configuredBindings.Count; index++)
                    installationBindings[index] = new PhysicalAnchorConfiguredBinding(
                        new PhysicalAugmentationPointId(configuredBindings[index].PointId),
                        configuredBindings[index].AnchorDisplayNumber);
            }
            catch (ArgumentException)
            {
                return PhysicalAnchorAdminOperationResult.Failure(
                    "admin_binding.configuration_invalid",
                    0);
            }
            var read = _reader.Read();
            if (!read.Succeeded)
                return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            var result = _writer.ReconcileConfiguredBindings(
                installationBindings,
                read.Snapshot.StoreVersion,
                _clock());
            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult PrepareErase(Guid uuid)
        {
            if (_pendingIndex.Contains(uuid)) return PhysicalAnchorAdminOperationResult.Success(CurrentVersionOrZero());
            var read = _reader.Read();
            if (!read.Succeeded) return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            if (!read.Snapshot.TryGetAnchor(uuid, out var anchor))
                return PhysicalAnchorAdminOperationResult.Failure("admin_binding.anchor_unindexed", read.Snapshot.StoreVersion);
            if (anchor.Status == PhysicalAnchorRecordStatus.PendingErase)
                return PhysicalAnchorAdminOperationResult.Success(read.Snapshot.StoreVersion);
            var result = _writer.Unassign(uuid, read.Snapshot.StoreVersion, _clock());
            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult RenameAnchor(Guid uuid, string customName)
        {
            if (uuid == Guid.Empty)
                return PhysicalAnchorAdminOperationResult.Failure("admin_binding.uuid_invalid", 0);
            var read = _reader.Read();
            if (!read.Succeeded)
                return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            if (!read.Snapshot.TryGetAnchor(uuid, out _))
                return PhysicalAnchorAdminOperationResult.Failure(
                    "admin_binding.anchor_unindexed",
                    read.Snapshot.StoreVersion);
            var result = _writer.RenameAnchor(uuid, customName, read.Snapshot.StoreVersion, _clock());
            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult CompleteErase(Guid uuid)
        {
            if (_pendingIndex.Remove(uuid)) return PhysicalAnchorAdminOperationResult.Success(CurrentVersionOrZero());
            var read = _reader.Read();
            if (!read.Succeeded) return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
            if (!read.Snapshot.TryGetAnchor(uuid, out _))
                return PhysicalAnchorAdminOperationResult.Success(read.Snapshot.StoreVersion);
            var result = _writer.RemoveUnassigned(uuid, read.Snapshot.StoreVersion);
            return result.Succeeded
                ? PhysicalAnchorAdminOperationResult.Success(result.StoreVersion)
                : PhysicalAnchorAdminOperationResult.Failure(result.DiagnosticTag, result.StoreVersion);
        }

        public PhysicalAnchorAdminOperationResult ImportLegacyAsUnassigned(ILegacyAnchorUuidMigrationSource legacy, out int importedCount)
        {
            if (legacy == null) throw new ArgumentNullException(nameof(legacy));
            importedCount = 0;
            var candidates = legacy.ReadCandidates();
            for (var index = 0; index < candidates.Length; index++)
            {
                var read = _reader.Read();
                if (!read.Succeeded) return PhysicalAnchorAdminOperationResult.Failure(read.DiagnosticTag, 0);
                if (read.Snapshot.TryGetAnchor(candidates[index], out _)) continue;
                var mutation = _writer.RegisterUnassigned(candidates[index], read.Snapshot.StoreVersion, _clock());
                if (!mutation.Succeeded)
                    return PhysicalAnchorAdminOperationResult.Failure(mutation.DiagnosticTag, mutation.StoreVersion);
                importedCount++;
            }
            legacy.CompleteMigration();
            return PhysicalAnchorAdminOperationResult.Success(CurrentVersionOrZero());
        }

        public bool TryGetEraseCandidates(out Guid[] uuids, out string diagnosticTag)
        {
            uuids = Array.Empty<Guid>();
            if (!TryRead(out var snapshot, out diagnosticTag)) return false;
            uuids = new Guid[snapshot.Records.Count];
            for (var index = 0; index < uuids.Length; index++) uuids[index] = snapshot.Records[index].Uuid;
            return true;
        }

        long CurrentVersionOrZero()
        {
            var read = _reader.Read();
            return read.Succeeded ? read.Snapshot.StoreVersion : 0;
        }

        static bool ContainsUuid(List<PhysicalAnchorAdminRecordSnapshot> records, Guid uuid)
        {
            for (var index = 0; index < records.Count; index++)
                if (records[index].Uuid == uuid) return true;
            return false;
        }
    }

    public static class PhysicalAnchorAdminBindingServices
    {
        static PhysicalAnchorAdminBindingSession _current;

        public static PhysicalAnchorAdminBindingSession Current
        {
            get
            {
                if (_current != null) return _current;
                var store = PhysicalAnchorBindingStore.OpenDefault();
                _current = new PhysicalAnchorAdminBindingSession(store, store);
                return _current;
            }
        }
    }
}
