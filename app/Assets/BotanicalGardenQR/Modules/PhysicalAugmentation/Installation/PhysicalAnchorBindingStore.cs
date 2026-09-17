using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Installation
{
    public sealed class PhysicalAnchorBindingStore : IPhysicalAnchorBindingReader, IPhysicalAnchorBindingWriter
    {
        const int CurrentSchemaVersion = 2;
        const int LegacySchemaVersion = 1;
        // Keep the established path so existing Quest installations migrate in place.
        const string DefaultFileName = "physical-anchor-bindings-v1.json";
        readonly IPhysicalAnchorBindingStorage _storage;

        public PhysicalAnchorBindingStore(string filePath)
            : this(new AtomicPhysicalAnchorBindingFileStorage(filePath))
        {
        }

        internal PhysicalAnchorBindingStore(IPhysicalAnchorBindingStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public static PhysicalAnchorBindingStore OpenDefault()
            => new PhysicalAnchorBindingStore(Path.Combine(Application.persistentDataPath, DefaultFileName));

        public PhysicalAnchorBindingReadResult Read()
        {
            try
            {
                if (!_storage.Exists) return PhysicalAnchorBindingReadResult.Success(PhysicalAnchorBindingSnapshot.Empty());
                var json = _storage.ReadAllText();
                if (string.IsNullOrWhiteSpace(json)) return ReadFailure("binding_store.empty");
                var header = JsonUtility.FromJson<SchemaHeader>(json);
                if (header == null || header.storeVersion < 0)
                    return ReadFailure("binding_store.schema_invalid");
                if (header.schemaVersion == LegacySchemaVersion)
                {
                    var legacy = JsonUtility.FromJson<LegacyStoreDocument>(json);
                    if (legacy == null || !ChecksumMatches(legacy))
                        return ReadFailure("binding_store.checksum_invalid");
                    return PhysicalAnchorBindingReadResult.Success(ToSnapshot(legacy));
                }
                if (header.schemaVersion != CurrentSchemaVersion)
                    return ReadFailure("binding_store.schema_invalid");
                var document = JsonUtility.FromJson<StoreDocument>(json);
                if (document == null || !ChecksumMatches(document))
                    return ReadFailure("binding_store.checksum_invalid");
                return PhysicalAnchorBindingReadResult.Success(ToSnapshot(document));
            }
            catch (IOException)
            {
                return PhysicalAnchorBindingReadResult.Failure(
                    PhysicalAnchorBindingFailureCode.StorageUnavailable,
                    "binding_store.read_unavailable");
            }
            catch (UnauthorizedAccessException)
            {
                return PhysicalAnchorBindingReadResult.Failure(
                    PhysicalAnchorBindingFailureCode.StorageUnavailable,
                    "binding_store.read_denied");
            }
            catch (Exception)
            {
                return ReadFailure("binding_store.read_failed");
            }
        }

        public PhysicalAnchorBindingMutationResult RegisterUnassigned(Guid uuid, long expectedStoreVersion, DateTimeOffset maintainedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (uuid == Guid.Empty) return Mutation.Invalid(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.uuid_invalid");
                if (snapshot.TryGetAnchor(uuid, out _)) return Mutation.Invalid(PhysicalAnchorBindingFailureCode.DuplicateAnchor, "binding_store.anchor_duplicate");
                var anchors = CopyAnchors(snapshot);
                anchors.Add(CreateNewAnchorRecord(
                    snapshot,
                    uuid,
                    PhysicalAnchorRecordStatus.Unassigned,
                    maintainedAt));
                return Mutation.Valid(anchors, CopyBindings(snapshot));
            });

        public PhysicalAnchorBindingMutationResult RegisterAndAssign(
            PhysicalAugmentationPointId pointId,
            Guid uuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!pointId.IsValid || uuid == Guid.Empty)
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.assignment_invalid");
                if (snapshot.TryGetAnchor(uuid, out _))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.DuplicateAnchor, "binding_store.anchor_duplicate");
                if (snapshot.TryGetBinding(pointId, out _))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.DuplicatePoint, "binding_store.point_duplicate");
                var anchors = CopyAnchors(snapshot);
                anchors.Add(CreateNewAnchorRecord(
                    snapshot,
                    uuid,
                    PhysicalAnchorRecordStatus.Assigned,
                    confirmedAt));
                var bindings = CopyBindings(snapshot);
                bindings.Add(new PhysicalAnchorBinding(pointId, uuid, augmentationPoseInAnchorSpace, uniformScale, 1, confirmedAt));
                return Mutation.Valid(anchors, bindings);
            });

        public PhysicalAnchorBindingMutationResult Assign(
            PhysicalAugmentationPointId pointId,
            Guid uuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (snapshot.TryGetBinding(pointId, out _))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.DuplicatePoint, "binding_store.point_duplicate");
                if (!snapshot.TryGetAnchor(uuid, out var anchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                if (anchor.Status != PhysicalAnchorRecordStatus.Unassigned)
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotUnassigned, "binding_store.anchor_assigned");
                var anchors = CopyAnchors(snapshot);
                ReplaceAnchor(anchors, CopyAnchorWithStatus(anchor, PhysicalAnchorRecordStatus.Assigned, confirmedAt));
                var bindings = CopyBindings(snapshot);
                bindings.Add(new PhysicalAnchorBinding(pointId, uuid, augmentationPoseInAnchorSpace, uniformScale, 1, confirmedAt));
                return Mutation.Valid(anchors, bindings);
            });

        public PhysicalAnchorBindingMutationResult SwapBinding(
            PhysicalAugmentationPointId pointId,
            Guid newUuid,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!snapshot.TryGetBinding(pointId, out var previous))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.PointNotBound, "binding_store.point_unbound");
                if (newUuid == Guid.Empty || !snapshot.TryGetAnchor(newUuid, out var newAnchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                if (newAnchor.Status != PhysicalAnchorRecordStatus.Unassigned)
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotUnassigned, "binding_store.anchor_assigned");
                if (previous.AnchorUuid == newUuid)
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.swap_same_anchor");
                if (!snapshot.TryGetAnchor(previous.AnchorUuid, out var previousAnchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                var anchors = CopyAnchors(snapshot);
                // The logical installation keeps its visible identity when a retune replaces the Meta UUID.
                // The temporary replacement number moves to the old PendingErase record, keeping labels unique
                // without letting configuration revive the UUID that is waiting for platform cleanup.
                ReplaceAnchor(anchors, new PhysicalAnchorRecord(
                    previous.AnchorUuid,
                    PhysicalAnchorRecordStatus.PendingErase,
                    confirmedAt,
                    newAnchor.DisplayNumber,
                    newAnchor.CustomName));
                ReplaceAnchor(anchors, new PhysicalAnchorRecord(
                    newUuid,
                    PhysicalAnchorRecordStatus.Assigned,
                    confirmedAt,
                    previousAnchor.DisplayNumber,
                    previousAnchor.CustomName));
                var bindings = CopyBindings(snapshot);
                RemoveBinding(bindings, pointId);
                bindings.Add(new PhysicalAnchorBinding(
                    pointId,
                    newUuid,
                    augmentationPoseInAnchorSpace,
                    uniformScale,
                    checked(previous.CalibrationRevision + 1),
                    confirmedAt));
                return Mutation.Valid(anchors, bindings);
            });

        public PhysicalAnchorBindingMutationResult Recalibrate(
            PhysicalAugmentationPointId pointId,
            Pose augmentationPoseInAnchorSpace,
            float uniformScale,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!snapshot.TryGetBinding(pointId, out var previous))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.PointNotBound, "binding_store.point_unbound");
                var bindings = CopyBindings(snapshot);
                RemoveBinding(bindings, pointId);
                bindings.Add(new PhysicalAnchorBinding(
                    pointId,
                    previous.AnchorUuid,
                    augmentationPoseInAnchorSpace,
                    uniformScale,
                    checked(previous.CalibrationRevision + 1),
                    confirmedAt));
                return Mutation.Valid(CopyAnchors(snapshot), bindings);
            });

        public PhysicalAnchorBindingMutationResult RenameAnchor(
            Guid uuid,
            string customName,
            long expectedStoreVersion,
            DateTimeOffset maintainedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!snapshot.TryGetAnchor(uuid, out var anchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                var normalized = PhysicalAnchorDisplayIdentity.NormalizeCustomName(customName);
                if (string.Equals(anchor.CustomName, normalized, StringComparison.Ordinal))
                    return Mutation.NoChange();
                var anchors = CopyAnchors(snapshot);
                ReplaceAnchor(anchors, new PhysicalAnchorRecord(
                    uuid,
                    anchor.Status,
                    maintainedAt,
                    anchor.DisplayNumber,
                    normalized));
                return Mutation.Valid(anchors, CopyBindings(snapshot));
            });

        public PhysicalAnchorBindingMutationResult ReplaceUnassignedIdentity(
            Guid oldUuid,
            Guid newUuid,
            int stableDisplayNumber,
            string stableCustomName,
            long expectedStoreVersion,
            DateTimeOffset maintainedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (oldUuid == Guid.Empty || newUuid == Guid.Empty || oldUuid == newUuid ||
                    stableDisplayNumber <= 0)
                    return Mutation.Invalid(
                        PhysicalAnchorBindingFailureCode.InvalidMutation,
                        "binding_store.replacement_invalid");
                var normalizedName = PhysicalAnchorDisplayIdentity.NormalizeCustomName(stableCustomName);
                if (!snapshot.TryGetAnchor(newUuid, out var newAnchor))
                    return Mutation.Invalid(
                        PhysicalAnchorBindingFailureCode.AnchorNotFound,
                        "binding_store.anchor_missing");
                if (newAnchor.Status != PhysicalAnchorRecordStatus.Unassigned)
                    return Mutation.Invalid(
                        PhysicalAnchorBindingFailureCode.AnchorNotUnassigned,
                        "binding_store.anchor_assigned");
                if (newAnchor.DisplayNumber == stableDisplayNumber &&
                    string.Equals(newAnchor.CustomName, normalizedName, StringComparison.Ordinal))
                    return Mutation.NoChange();
                if (!snapshot.TryGetAnchor(oldUuid, out var oldAnchor))
                    return Mutation.Invalid(
                        PhysicalAnchorBindingFailureCode.AnchorNotFound,
                        "binding_store.anchor_missing");
                if (oldAnchor.Status != PhysicalAnchorRecordStatus.Unassigned ||
                    oldAnchor.DisplayNumber != stableDisplayNumber ||
                    !string.Equals(oldAnchor.CustomName, normalizedName, StringComparison.Ordinal))
                    return Mutation.Invalid(
                        PhysicalAnchorBindingFailureCode.AnchorNotUnassigned,
                        "binding_store.replacement_identity_changed");

                var anchors = CopyAnchors(snapshot);
                ReplaceAnchor(anchors, new PhysicalAnchorRecord(
                    oldUuid,
                    PhysicalAnchorRecordStatus.PendingErase,
                    maintainedAt,
                    newAnchor.DisplayNumber,
                    newAnchor.CustomName));
                ReplaceAnchor(anchors, new PhysicalAnchorRecord(
                    newUuid,
                    PhysicalAnchorRecordStatus.Unassigned,
                    maintainedAt,
                    stableDisplayNumber,
                    normalizedName));
                return Mutation.Valid(anchors, CopyBindings(snapshot));
            });

        public PhysicalAnchorBindingMutationResult ReconcileConfiguredBindings(
            IReadOnlyList<PhysicalAnchorConfiguredBinding> configuredBindings,
            long expectedStoreVersion,
            DateTimeOffset confirmedAt)
        {
            if (configuredBindings == null)
                return PhysicalAnchorBindingMutationResult.Failure(
                    PhysicalAnchorBindingFailureCode.InvalidMutation,
                    "binding_store.configuration_missing",
                    expectedStoreVersion);
            return Mutate(expectedStoreVersion, snapshot =>
            {
                var configuredPointIds = new HashSet<PhysicalAugmentationPointId>();
                var configuredDisplayNumbers = new HashSet<int>();
                for (var index = 0; index < configuredBindings.Count; index++)
                {
                    var configured = configuredBindings[index];
                    if (!configured.PointId.IsValid || configured.AnchorDisplayNumber <= 0)
                        return Mutation.Invalid(
                            PhysicalAnchorBindingFailureCode.InvalidMutation,
                            "binding_store.configuration_invalid");
                    if (!configuredPointIds.Add(configured.PointId))
                        return Mutation.Invalid(
                            PhysicalAnchorBindingFailureCode.DuplicatePoint,
                            "binding_store.configuration_point_duplicate");
                    if (!configuredDisplayNumbers.Add(configured.AnchorDisplayNumber))
                        return Mutation.Invalid(
                            PhysicalAnchorBindingFailureCode.DuplicateAnchor,
                            "binding_store.configuration_anchor_duplicate");
                }

                var anchorsByDisplayNumber = new Dictionary<int, PhysicalAnchorRecord>();
                for (var index = 0; index < snapshot.Anchors.Count; index++)
                {
                    var anchor = snapshot.Anchors[index];
                    anchorsByDisplayNumber.Add(anchor.DisplayNumber, anchor);
                }

                var desiredBindings = new List<PhysicalAnchorBinding>();
                var desiredAssignedUuids = new HashSet<Guid>();
                for (var index = 0; index < configuredBindings.Count; index++)
                {
                    var configured = configuredBindings[index];
                    if (!anchorsByDisplayNumber.TryGetValue(configured.AnchorDisplayNumber, out var anchor))
                        continue;
                    if (anchor.Status == PhysicalAnchorRecordStatus.PendingErase)
                        continue;
                    desiredAssignedUuids.Add(anchor.Uuid);
                    if (snapshot.TryGetBinding(configured.PointId, out var previous) &&
                        previous.AnchorUuid == anchor.Uuid && IsCanonicalConfiguredBinding(previous))
                    {
                        desiredBindings.Add(previous);
                        continue;
                    }

                    var nextRevision = snapshot.TryGetBinding(configured.PointId, out previous)
                        ? checked(previous.CalibrationRevision + 1)
                        : 1;
                    desiredBindings.Add(new PhysicalAnchorBinding(
                        configured.PointId,
                        anchor.Uuid,
                        Pose.identity,
                        1f,
                        nextRevision,
                        confirmedAt));
                }

                var desiredAnchors = CopyAnchors(snapshot);
                for (var index = 0; index < desiredAnchors.Count; index++)
                {
                    var anchor = desiredAnchors[index];
                    if (anchor.Status == PhysicalAnchorRecordStatus.PendingErase)
                        continue;
                    var desiredStatus = desiredAssignedUuids.Contains(anchor.Uuid)
                        ? PhysicalAnchorRecordStatus.Assigned
                        : PhysicalAnchorRecordStatus.Unassigned;
                    if (anchor.Status == desiredStatus) continue;
                    desiredAnchors[index] = CopyAnchorWithStatus(anchor, desiredStatus, confirmedAt);
                }

                return IsEquivalent(snapshot, desiredAnchors, desiredBindings)
                    ? Mutation.NoChange()
                    : Mutation.Valid(desiredAnchors, desiredBindings);
            });
        }

        public PhysicalAnchorBindingMutationResult Unassign(Guid uuid, long expectedStoreVersion, DateTimeOffset maintainedAt)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!snapshot.TryGetAnchor(uuid, out var anchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                if (anchor.Status == PhysicalAnchorRecordStatus.PendingErase)
                    return Mutation.NoChange();
                var anchors = CopyAnchors(snapshot);
                ReplaceAnchor(anchors, CopyAnchorWithStatus(anchor, PhysicalAnchorRecordStatus.PendingErase, maintainedAt));
                var bindings = CopyBindings(snapshot);
                if (anchor.Status == PhysicalAnchorRecordStatus.Assigned)
                    RemoveBindingByUuid(bindings, uuid);
                return Mutation.Valid(anchors, bindings);
            });

        public PhysicalAnchorBindingMutationResult RemoveUnassigned(Guid uuid, long expectedStoreVersion)
            => Mutate(expectedStoreVersion, snapshot =>
            {
                if (!snapshot.TryGetAnchor(uuid, out var anchor))
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorNotFound, "binding_store.anchor_missing");
                if (anchor.Status != PhysicalAnchorRecordStatus.Unassigned &&
                    anchor.Status != PhysicalAnchorRecordStatus.PendingErase)
                    return Mutation.Invalid(PhysicalAnchorBindingFailureCode.AnchorStillAssigned, "binding_store.anchor_still_assigned");
                var anchors = CopyAnchors(snapshot);
                anchors.RemoveAll(candidate => candidate.Uuid == uuid);
                return Mutation.Valid(anchors, CopyBindings(snapshot));
            });

        PhysicalAnchorBindingMutationResult Mutate(long expectedStoreVersion, Func<PhysicalAnchorBindingSnapshot, Mutation> mutation)
        {
            if (expectedStoreVersion < 0)
                return PhysicalAnchorBindingMutationResult.Failure(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.version_invalid", 0);
            var read = Read();
            if (!read.Succeeded)
                return PhysicalAnchorBindingMutationResult.Failure(read.FailureCode, read.DiagnosticTag, 0);
            var snapshot = read.Snapshot;
            if (snapshot.StoreVersion != expectedStoreVersion)
                return PhysicalAnchorBindingMutationResult.Failure(PhysicalAnchorBindingFailureCode.StaleVersion, "binding_store.version_stale", snapshot.StoreVersion);
            try
            {
                var candidate = mutation(snapshot);
                if (!candidate.IsValid)
                    return PhysicalAnchorBindingMutationResult.Failure(candidate.FailureCode, candidate.DiagnosticTag, snapshot.StoreVersion);
                if (candidate.IsNoChange)
                    return PhysicalAnchorBindingMutationResult.Success(snapshot.StoreVersion);
                var next = new PhysicalAnchorBindingSnapshot(checked(snapshot.StoreVersion + 1), candidate.Anchors, candidate.Bindings);
                _storage.ReplaceAllText(Serialize(next));
                return PhysicalAnchorBindingMutationResult.Success(next.StoreVersion);
            }
            catch (ArgumentException)
            {
                return PhysicalAnchorBindingMutationResult.Failure(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.mutation_invalid", snapshot.StoreVersion);
            }
            catch (OverflowException)
            {
                return PhysicalAnchorBindingMutationResult.Failure(PhysicalAnchorBindingFailureCode.InvalidMutation, "binding_store.revision_overflow", snapshot.StoreVersion);
            }
            catch (Exception)
            {
                return PhysicalAnchorBindingMutationResult.Failure(PhysicalAnchorBindingFailureCode.StorageUnavailable, "binding_store.write_failed", snapshot.StoreVersion);
            }
        }

        static PhysicalAnchorBindingReadResult ReadFailure(string tag)
            => PhysicalAnchorBindingReadResult.Failure(PhysicalAnchorBindingFailureCode.CorruptStore, tag);

        static List<PhysicalAnchorRecord> CopyAnchors(PhysicalAnchorBindingSnapshot snapshot)
            => new List<PhysicalAnchorRecord>(snapshot.Anchors);

        static List<PhysicalAnchorBinding> CopyBindings(PhysicalAnchorBindingSnapshot snapshot)
            => new List<PhysicalAnchorBinding>(snapshot.Bindings);

        static PhysicalAnchorRecord CreateNewAnchorRecord(
            PhysicalAnchorBindingSnapshot snapshot,
            Guid uuid,
            PhysicalAnchorRecordStatus status,
            DateTimeOffset maintainedAt)
            => new PhysicalAnchorRecord(
                uuid,
                status,
                maintainedAt,
                NextDisplayNumber(snapshot),
                string.Empty);

        static PhysicalAnchorRecord CopyAnchorWithStatus(
            PhysicalAnchorRecord source,
            PhysicalAnchorRecordStatus status,
            DateTimeOffset maintainedAt)
            => new PhysicalAnchorRecord(
                source.Uuid,
                status,
                maintainedAt,
                source.DisplayNumber,
                source.CustomName);

        static int NextDisplayNumber(PhysicalAnchorBindingSnapshot snapshot)
        {
            var used = new HashSet<int>();
            for (var index = 0; index < snapshot.Anchors.Count; index++)
                used.Add(snapshot.Anchors[index].DisplayNumber);
            for (var candidate = 1; candidate <= snapshot.Anchors.Count; candidate++)
                if (!used.Contains(candidate)) return candidate;
            return checked(snapshot.Anchors.Count + 1);
        }

        static void ReplaceAnchor(List<PhysicalAnchorRecord> anchors, PhysicalAnchorRecord replacement)
        {
            for (var index = 0; index < anchors.Count; index++)
                if (anchors[index].Uuid == replacement.Uuid)
                {
                    anchors[index] = replacement;
                    return;
                }
            throw new ArgumentException("Anchor record was not found.");
        }

        static void RemoveBinding(List<PhysicalAnchorBinding> bindings, PhysicalAugmentationPointId pointId)
        {
            if (bindings.RemoveAll(binding => binding.PointId == pointId) != 1)
                throw new ArgumentException("Point binding was not found.");
        }

        static void RemoveBindingByUuid(List<PhysicalAnchorBinding> bindings, Guid uuid)
        {
            if (bindings.RemoveAll(binding => binding.AnchorUuid == uuid) != 1)
                throw new ArgumentException("Anchor binding was not found.");
        }

        static bool IsCanonicalConfiguredBinding(PhysicalAnchorBinding binding)
            => binding.AugmentationPoseInAnchorSpace.position == Vector3.zero &&
               binding.AugmentationPoseInAnchorSpace.rotation == Quaternion.identity &&
               Mathf.Approximately(binding.UniformScale, 1f);

        static bool IsEquivalent(
            PhysicalAnchorBindingSnapshot snapshot,
            IReadOnlyList<PhysicalAnchorRecord> anchors,
            IReadOnlyList<PhysicalAnchorBinding> bindings)
        {
            if (snapshot.Anchors.Count != anchors.Count || snapshot.Bindings.Count != bindings.Count)
                return false;
            for (var index = 0; index < anchors.Count; index++)
            {
                var candidate = anchors[index];
                if (!snapshot.TryGetAnchor(candidate.Uuid, out var current) ||
                    current.Status != candidate.Status ||
                    current.MaintainedAt != candidate.MaintainedAt ||
                    current.DisplayNumber != candidate.DisplayNumber ||
                    !string.Equals(current.CustomName, candidate.CustomName, StringComparison.Ordinal))
                    return false;
            }
            for (var index = 0; index < bindings.Count; index++)
            {
                var candidate = bindings[index];
                if (!snapshot.TryGetBinding(candidate.PointId, out var current) ||
                    current.AnchorUuid != candidate.AnchorUuid ||
                    current.AugmentationPoseInAnchorSpace.position != candidate.AugmentationPoseInAnchorSpace.position ||
                    current.AugmentationPoseInAnchorSpace.rotation != candidate.AugmentationPoseInAnchorSpace.rotation ||
                    !Mathf.Approximately(current.UniformScale, candidate.UniformScale) ||
                    current.CalibrationRevision != candidate.CalibrationRevision ||
                    current.ConfirmedAt != candidate.ConfirmedAt)
                    return false;
            }
            return true;
        }

        static string Serialize(PhysicalAnchorBindingSnapshot snapshot)
        {
            var document = FromSnapshot(snapshot);
            document.checksum = string.Empty;
            document.checksum = ComputeChecksum(JsonUtility.ToJson(document));
            return JsonUtility.ToJson(document, true);
        }

        static PhysicalAnchorBindingSnapshot ToSnapshot(StoreDocument document)
        {
            var anchorDocuments = document.anchors ?? Array.Empty<AnchorDocument>();
            var bindingDocuments = document.bindings ?? Array.Empty<BindingDocument>();
            var anchors = new PhysicalAnchorRecord[anchorDocuments.Length];
            for (var index = 0; index < anchors.Length; index++)
            {
                var source = anchorDocuments[index] ?? throw new ArgumentException("Anchor record is missing.");
                anchors[index] = new PhysicalAnchorRecord(
                    Guid.ParseExact(source.uuid, "D"),
                    (PhysicalAnchorRecordStatus)source.status,
                    DateTimeOffset.Parse(source.maintainedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    source.displayNumber,
                    source.customName);
            }
            return new PhysicalAnchorBindingSnapshot(
                document.storeVersion,
                anchors,
                ParseBindings(bindingDocuments));
        }

        static PhysicalAnchorBindingSnapshot ToSnapshot(LegacyStoreDocument document)
        {
            var anchorDocuments = document.anchors ?? Array.Empty<LegacyAnchorDocument>();
            var seeds = new LegacyAnchorSeed[anchorDocuments.Length];
            for (var index = 0; index < seeds.Length; index++)
            {
                var source = anchorDocuments[index] ?? throw new ArgumentException("Anchor record is missing.");
                seeds[index] = new LegacyAnchorSeed(
                    Guid.ParseExact(source.uuid, "D"),
                    (PhysicalAnchorRecordStatus)source.status,
                    DateTimeOffset.Parse(
                        source.maintainedAtUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind));
            }
            Array.Sort(seeds, (left, right) => left.Uuid.CompareTo(right.Uuid));
            var anchors = new PhysicalAnchorRecord[seeds.Length];
            for (var index = 0; index < anchors.Length; index++)
                anchors[index] = new PhysicalAnchorRecord(
                    seeds[index].Uuid,
                    seeds[index].Status,
                    seeds[index].MaintainedAt,
                    index + 1,
                    string.Empty);
            return new PhysicalAnchorBindingSnapshot(
                document.storeVersion,
                anchors,
                ParseBindings(document.bindings ?? Array.Empty<BindingDocument>()));
        }

        static PhysicalAnchorBinding[] ParseBindings(BindingDocument[] bindingDocuments)
        {
            var bindings = new PhysicalAnchorBinding[bindingDocuments.Length];
            for (var index = 0; index < bindings.Length; index++)
            {
                var source = bindingDocuments[index] ?? throw new ArgumentException("Binding record is missing.");
                bindings[index] = new PhysicalAnchorBinding(
                    new PhysicalAugmentationPointId(source.pointId),
                    Guid.ParseExact(source.anchorUuid, "D"),
                    new Pose(source.position, source.rotation),
                    source.uniformScale,
                    source.calibrationRevision,
                    DateTimeOffset.Parse(source.confirmedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            }
            return bindings;
        }

        static StoreDocument FromSnapshot(PhysicalAnchorBindingSnapshot snapshot)
        {
            var anchors = new AnchorDocument[snapshot.Anchors.Count];
            for (var index = 0; index < anchors.Length; index++)
            {
                var source = snapshot.Anchors[index];
                anchors[index] = new AnchorDocument
                {
                    uuid = source.Uuid.ToString("D"),
                    status = (int)source.Status,
                    maintainedAtUtc = source.MaintainedAt.ToUniversalTime().ToString("O"),
                    displayNumber = source.DisplayNumber,
                    customName = source.CustomName
                };
            }
            var bindings = new BindingDocument[snapshot.Bindings.Count];
            for (var index = 0; index < bindings.Length; index++)
            {
                var source = snapshot.Bindings[index];
                bindings[index] = new BindingDocument
                {
                    pointId = source.PointId.Value,
                    anchorUuid = source.AnchorUuid.ToString("D"),
                    position = source.AugmentationPoseInAnchorSpace.position,
                    rotation = source.AugmentationPoseInAnchorSpace.rotation,
                    uniformScale = source.UniformScale,
                    calibrationRevision = source.CalibrationRevision,
                    confirmedAtUtc = source.ConfirmedAt.ToUniversalTime().ToString("O")
                };
            }
            return new StoreDocument
            {
                schemaVersion = CurrentSchemaVersion,
                storeVersion = snapshot.StoreVersion,
                anchors = anchors,
                bindings = bindings,
                checksum = string.Empty
            };
        }

        static bool ChecksumMatches(StoreDocument document)
        {
            var expected = document.checksum ?? string.Empty;
            document.checksum = string.Empty;
            var actual = ComputeChecksum(JsonUtility.ToJson(document));
            document.checksum = expected;
            return expected.Length == 64 && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        static bool ChecksumMatches(LegacyStoreDocument document)
        {
            var expected = document.checksum ?? string.Empty;
            document.checksum = string.Empty;
            var actual = ComputeChecksum(JsonUtility.ToJson(document));
            document.checksum = expected;
            return expected.Length == 64 && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
            return builder.ToString();
        }

        readonly struct Mutation
        {
            Mutation(bool isValid, bool isNoChange, List<PhysicalAnchorRecord> anchors, List<PhysicalAnchorBinding> bindings, PhysicalAnchorBindingFailureCode failureCode, string diagnosticTag)
            {
                IsValid = isValid;
                IsNoChange = isNoChange;
                Anchors = anchors;
                Bindings = bindings;
                FailureCode = failureCode;
                DiagnosticTag = diagnosticTag;
            }

            public bool IsValid { get; }
            public bool IsNoChange { get; }
            public List<PhysicalAnchorRecord> Anchors { get; }
            public List<PhysicalAnchorBinding> Bindings { get; }
            public PhysicalAnchorBindingFailureCode FailureCode { get; }
            public string DiagnosticTag { get; }

            public static Mutation Valid(List<PhysicalAnchorRecord> anchors, List<PhysicalAnchorBinding> bindings)
                => new Mutation(true, false, anchors, bindings, PhysicalAnchorBindingFailureCode.None, string.Empty);

            public static Mutation NoChange()
                => new Mutation(true, true, null, null, PhysicalAnchorBindingFailureCode.None, string.Empty);

            public static Mutation Invalid(PhysicalAnchorBindingFailureCode code, string tag)
                => new Mutation(false, false, null, null, code, tag);
        }

        [Serializable]
        sealed class SchemaHeader
        {
            public int schemaVersion;
            public long storeVersion;
        }

        [Serializable]
        sealed class StoreDocument
        {
            public int schemaVersion;
            public long storeVersion;
            public AnchorDocument[] anchors;
            public BindingDocument[] bindings;
            public string checksum;
        }

        [Serializable]
        sealed class AnchorDocument
        {
            public string uuid;
            public int status;
            public string maintainedAtUtc;
            public int displayNumber;
            public string customName;
        }

        [Serializable]
        sealed class LegacyStoreDocument
        {
            public int schemaVersion;
            public long storeVersion;
            public LegacyAnchorDocument[] anchors;
            public BindingDocument[] bindings;
            public string checksum;
        }

        [Serializable]
        sealed class LegacyAnchorDocument
        {
            public string uuid;
            public int status;
            public string maintainedAtUtc;
        }

        readonly struct LegacyAnchorSeed
        {
            public LegacyAnchorSeed(
                Guid uuid,
                PhysicalAnchorRecordStatus status,
                DateTimeOffset maintainedAt)
            {
                Uuid = uuid;
                Status = status;
                MaintainedAt = maintainedAt;
            }

            public Guid Uuid { get; }
            public PhysicalAnchorRecordStatus Status { get; }
            public DateTimeOffset MaintainedAt { get; }
        }

        [Serializable]
        sealed class BindingDocument
        {
            public string pointId;
            public string anchorUuid;
            public Vector3 position;
            public Quaternion rotation;
            public float uniformScale;
            public long calibrationRevision;
            public string confirmedAtUtc;
        }
    }

    internal interface IPhysicalAnchorBindingStorage
    {
        bool Exists { get; }
        string ReadAllText();
        void ReplaceAllText(string content);
    }

    internal sealed class AtomicPhysicalAnchorBindingFileStorage : IPhysicalAnchorBindingStorage
    {
        readonly string _path;
        readonly string _temporaryPath;
        readonly string _backupPath;

        public AtomicPhysicalAnchorBindingFileStorage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A binding store path is required.", nameof(path));
            _path = Path.GetFullPath(path);
            _temporaryPath = _path + ".tmp";
            _backupPath = _path + ".bak";
        }

        public bool Exists => File.Exists(_path) || File.Exists(_backupPath);

        public string ReadAllText()
        {
            if (File.Exists(_path)) return File.ReadAllText(_path, Encoding.UTF8);
            if (File.Exists(_backupPath)) return File.ReadAllText(_backupPath, Encoding.UTF8);
            throw new FileNotFoundException("Physical anchor binding store does not exist.", _path);
        }

        public void ReplaceAllText(string content)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            DeleteIfExists(_temporaryPath);
            File.WriteAllText(_temporaryPath, content ?? string.Empty, new UTF8Encoding(false));
            if (!File.Exists(_path))
            {
                File.Move(_temporaryPath, _path);
                return;
            }

            DeleteIfExists(_backupPath);
            try
            {
                File.Replace(_temporaryPath, _path, _backupPath);
                DeleteIfExists(_backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithMoveFallback();
            }
        }

        void ReplaceWithMoveFallback()
        {
            File.Move(_path, _backupPath);
            try
            {
                File.Move(_temporaryPath, _path);
                DeleteIfExists(_backupPath);
            }
            catch
            {
                if (!File.Exists(_path) && File.Exists(_backupPath)) File.Move(_backupPath, _path);
                throw;
            }
        }

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
