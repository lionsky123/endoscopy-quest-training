using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Installation;
using BotanicalGardenQR.SpatialAnchorAdmin.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAnchorBindingStoreTests
    {
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);
        readonly PhysicalAugmentationPointId _pointId = new PhysicalAugmentationPointId("cactus_flower");
        InMemoryStorage _storage;
        PhysicalAnchorBindingStore _store;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemoryStorage();
            _store = new PhysicalAnchorBindingStore(_storage);
        }

        [Test]
        public void MissingStore_ReadsAsVersionZeroEmptySnapshot()
        {
            var result = _store.Read();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.StoreVersion, Is.Zero);
            Assert.That(result.Snapshot.Anchors, Is.Empty);
            Assert.That(result.Snapshot.Bindings, Is.Empty);
        }

        [Test]
        public void RegisterAndAssign_RoundTripsOneCoherentRecord()
        {
            var uuid = Guid.NewGuid();

            var mutation = _store.RegisterAndAssign(_pointId, uuid, Pose.identity, 1f, 0, Now);
            var read = _store.Read();

            Assert.That(mutation.Succeeded, Is.True);
            Assert.That(read.Succeeded, Is.True);
            Assert.That(read.Snapshot.StoreVersion, Is.EqualTo(1));
            Assert.That(read.Snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(uuid));
            Assert.That(binding.CalibrationRevision, Is.EqualTo(1));
            Assert.That(read.Snapshot.TryGetAnchor(uuid, out var anchor), Is.True);
            Assert.That(anchor.Status, Is.EqualTo(PhysicalAnchorRecordStatus.Assigned));
            Assert.That(anchor.DisplayNumber, Is.EqualTo(1));
            Assert.That(anchor.DisplayLabel, Is.EqualTo("锚点 01"));
        }

        [Test]
        public void AnchorDisplayIdentity_AutoNumbersAndPersistsCustomName()
        {
            var firstUuid = Guid.NewGuid();
            var secondUuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(firstUuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(secondUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);

            var renamed = _store.RenameAnchor(secondUuid, "北侧巨人柱", 2, Now.AddMinutes(1));
            var snapshot = _store.Read().Snapshot;

            Assert.That(renamed.Succeeded, Is.True);
            Assert.That(snapshot.TryGetAnchor(firstUuid, out var first), Is.True);
            Assert.That(snapshot.TryGetAnchor(secondUuid, out var second), Is.True);
            Assert.That(first.DisplayNumber, Is.EqualTo(1));
            Assert.That(first.DisplayLabel, Is.EqualTo("锚点 01"));
            Assert.That(second.DisplayNumber, Is.EqualTo(2));
            Assert.That(second.CustomName, Is.EqualTo("北侧巨人柱"));
            Assert.That(second.DisplayLabel, Is.EqualTo("锚点 02 · 北侧巨人柱"));
        }

        [Test]
        public void DeletedDisplayNumber_IsReusedByNextIndependentAnchor()
        {
            var firstUuid = Guid.NewGuid();
            var secondUuid = Guid.NewGuid();
            var replacementUuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(firstUuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(secondUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);
            Assert.That(_store.RemoveUnassigned(firstUuid, 2).Succeeded, Is.True);

            Assert.That(_store.RegisterUnassigned(replacementUuid, 3, Now.AddSeconds(2)).Succeeded, Is.True);
            var snapshot = _store.Read().Snapshot;

            Assert.That(snapshot.TryGetAnchor(secondUuid, out var second), Is.True);
            Assert.That(snapshot.TryGetAnchor(replacementUuid, out var replacement), Is.True);
            Assert.That(second.DisplayNumber, Is.EqualTo(2));
            Assert.That(replacement.DisplayNumber, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredBindings_AssignOnlyMatchingStableDisplayNumbers()
        {
            var firstUuid = Guid.NewGuid();
            var secondUuid = Guid.NewGuid();
            var secondPointId = new PhysicalAugmentationPointId("bat_roost");
            Assert.That(_store.RegisterUnassigned(firstUuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(secondUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);

            var firstReconciliation = _store.ReconcileConfiguredBindings(
                new[] { new PhysicalAnchorConfiguredBinding(_pointId, 1) },
                2,
                Now.AddMinutes(1));
            var unchangedReconciliation = _store.ReconcileConfiguredBindings(
                new[] { new PhysicalAnchorConfiguredBinding(_pointId, 1) },
                3,
                Now.AddMinutes(2));
            var secondReconciliation = _store.ReconcileConfiguredBindings(
                new[]
                {
                    new PhysicalAnchorConfiguredBinding(_pointId, 1),
                    new PhysicalAnchorConfiguredBinding(secondPointId, 2)
                },
                3,
                Now.AddMinutes(3));
            var snapshot = _store.Read().Snapshot;

            Assert.That(firstReconciliation.Succeeded, Is.True);
            Assert.That(unchangedReconciliation.Succeeded, Is.True);
            Assert.That(unchangedReconciliation.StoreVersion, Is.EqualTo(3));
            Assert.That(secondReconciliation.Succeeded, Is.True);
            Assert.That(snapshot.TryGetBinding(_pointId, out var firstBinding), Is.True);
            Assert.That(snapshot.TryGetBinding(secondPointId, out var secondBinding), Is.True);
            Assert.That(firstBinding.AnchorUuid, Is.EqualTo(firstUuid));
            Assert.That(secondBinding.AnchorUuid, Is.EqualTo(secondUuid));
            Assert.That(snapshot.Anchors.All(anchor => anchor.Status == PhysicalAnchorRecordStatus.Assigned), Is.True);
        }

        [Test]
        public void ConfiguredDisplayNumberWithoutAnchor_RemainsSafelyUnboundUntilPlacement()
        {
            var configured = new[] { new PhysicalAnchorConfiguredBinding(_pointId, 1) };
            var beforePlacement = _store.ReconcileConfiguredBindings(configured, 0, Now);
            var uuid = Guid.NewGuid();
            var placed = _store.RegisterUnassigned(uuid, 0, Now.AddMinutes(1));
            var afterPlacement = _store.ReconcileConfiguredBindings(configured, 1, Now.AddMinutes(2));
            var snapshot = _store.Read().Snapshot;

            Assert.That(beforePlacement.Succeeded, Is.True);
            Assert.That(beforePlacement.StoreVersion, Is.Zero);
            Assert.That(placed.Succeeded, Is.True);
            Assert.That(afterPlacement.Succeeded, Is.True);
            Assert.That(snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(uuid));
        }

        [Test]
        public void ConfiguredBindingRemap_IsAtomicAndLeavesPreviousAnchorUnassigned()
        {
            var firstUuid = Guid.NewGuid();
            var secondUuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(firstUuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(secondUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);
            Assert.That(_store.ReconcileConfiguredBindings(
                new[] { new PhysicalAnchorConfiguredBinding(_pointId, 1) },
                2,
                Now.AddMinutes(1)).Succeeded, Is.True);

            var remapped = _store.ReconcileConfiguredBindings(
                new[] { new PhysicalAnchorConfiguredBinding(_pointId, 2) },
                3,
                Now.AddMinutes(2));
            var snapshot = _store.Read().Snapshot;

            Assert.That(remapped.Succeeded, Is.True);
            Assert.That(snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(secondUuid));
            Assert.That(binding.CalibrationRevision, Is.EqualTo(2));
            Assert.That(snapshot.TryGetAnchor(firstUuid, out var first), Is.True);
            Assert.That(first.Status, Is.EqualTo(PhysicalAnchorRecordStatus.Unassigned));
            Assert.That(snapshot.TryGetAnchor(secondUuid, out var second), Is.True);
            Assert.That(second.Status, Is.EqualTo(PhysicalAnchorRecordStatus.Assigned));
        }

        [Test]
        public void UnassignedRetune_PreservesStableIdentityAndRetryIsIdempotent()
        {
            var oldUuid = Guid.NewGuid();
            var newUuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(oldUuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RenameAnchor(oldUuid, "备用点", 1, Now.AddSeconds(1)).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(newUuid, 2, Now.AddSeconds(2)).Succeeded, Is.True);

            var replaced = _store.ReplaceUnassignedIdentity(
                oldUuid, newUuid, 1, "备用点", 3, Now.AddMinutes(1));
            var retried = _store.ReplaceUnassignedIdentity(
                oldUuid, newUuid, 1, "备用点", 4, Now.AddMinutes(2));
            var snapshot = _store.Read().Snapshot;

            Assert.That(replaced.Succeeded, Is.True);
            Assert.That(retried.Succeeded, Is.True);
            Assert.That(retried.StoreVersion, Is.EqualTo(4));
            Assert.That(snapshot.TryGetAnchor(newUuid, out var replacement), Is.True);
            Assert.That(replacement.Status, Is.EqualTo(PhysicalAnchorRecordStatus.Unassigned));
            Assert.That(replacement.DisplayNumber, Is.EqualTo(1));
            Assert.That(replacement.CustomName, Is.EqualTo("备用点"));
            Assert.That(snapshot.TryGetAnchor(oldUuid, out var old), Is.True);
            Assert.That(old.DisplayNumber, Is.EqualTo(2));
        }

        [Test]
        public void LegacySchema_ReadsWithoutDeletingExistingAnchorAndUpgradesOnNextWrite()
        {
            var uuid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
            var document = new LegacyStoreDocument
            {
                schemaVersion = 1,
                storeVersion = 3,
                anchors = new[]
                {
                    new LegacyAnchorDocument
                    {
                        uuid = uuid.ToString("D"),
                        status = (int)PhysicalAnchorRecordStatus.Assigned,
                        maintainedAtUtc = Now.ToString("O")
                    }
                },
                bindings = new[]
                {
                    new LegacyBindingDocument
                    {
                        pointId = _pointId.Value,
                        anchorUuid = uuid.ToString("D"),
                        position = Vector3.zero,
                        rotation = Quaternion.identity,
                        uniformScale = 1f,
                        calibrationRevision = 1,
                        confirmedAtUtc = Now.ToString("O")
                    }
                },
                checksum = string.Empty
            };
            document.checksum = ComputeChecksum(JsonUtility.ToJson(document));
            _storage.Content = JsonUtility.ToJson(document, true);

            var legacyRead = _store.Read();
            Assert.That(legacyRead.Succeeded, Is.True);
            Assert.That(legacyRead.Snapshot.TryGetAnchor(uuid, out var legacyAnchor), Is.True);
            Assert.That(legacyAnchor.DisplayLabel, Is.EqualTo("锚点 01"));
            Assert.That(legacyRead.Snapshot.TryGetBinding(_pointId, out var legacyBinding), Is.True);
            Assert.That(legacyBinding.AnchorUuid, Is.EqualTo(uuid));

            var renamed = _store.RenameAnchor(uuid, "入口", 3, Now.AddMinutes(1));
            Assert.That(renamed.Succeeded, Is.True);
            Assert.That(_storage.Content, Does.Contain("\"schemaVersion\": 2"));
            Assert.That(_store.Read().Snapshot.TryGetAnchor(uuid, out var upgraded), Is.True);
            Assert.That(upgraded.DisplayLabel, Is.EqualTo("锚点 01 · 入口"));
            Assert.That(_store.Read().Snapshot.TryGetBinding(_pointId, out var upgradedBinding), Is.True);
            Assert.That(upgradedBinding.AnchorUuid, Is.EqualTo(uuid));
        }

        [Test]
        public void WriteFailure_RetainsPreviousValidDocument()
        {
            var firstUuid = Guid.NewGuid();
            Assert.That(_store.RegisterAndAssign(_pointId, firstUuid, Pose.identity, 1f, 0, Now).Succeeded, Is.True);
            var nextUuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(nextUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);
            var previousContent = _storage.Content;
            _storage.ThrowOnReplace = true;

            var result = _store.SwapBinding(_pointId, nextUuid, Pose.identity, 1f, 2, Now.AddMinutes(1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAnchorBindingFailureCode.StorageUnavailable));
            Assert.That(_storage.Content, Is.EqualTo(previousContent));
            Assert.That(_store.Read().Snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(firstUuid));
        }

        [Test]
        public void CorruptDocument_FailsClosed()
        {
            _storage.Content = "{not valid json";

            var result = _store.Read();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAnchorBindingFailureCode.CorruptStore));
        }

        [Test]
        public void StaleExpectedVersion_CannotOverwriteNewerState()
        {
            Assert.That(_store.RegisterUnassigned(Guid.NewGuid(), 0, Now).Succeeded, Is.True);

            var result = _store.RegisterUnassigned(Guid.NewGuid(), 0, Now);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAnchorBindingFailureCode.StaleVersion));
            Assert.That(result.StoreVersion, Is.EqualTo(1));
        }

        [Test]
        public void SwapBinding_IncrementsRevisionAndLeavesOldAnchorPendingErase()
        {
            var oldUuid = Guid.NewGuid();
            var newUuid = Guid.NewGuid();
            Assert.That(_store.RegisterAndAssign(_pointId, oldUuid, Pose.identity, 1f, 0, Now).Succeeded, Is.True);
            Assert.That(_store.RegisterUnassigned(newUuid, 1, Now.AddSeconds(1)).Succeeded, Is.True);

            var result = _store.SwapBinding(_pointId, newUuid, new Pose(Vector3.right, Quaternion.identity), 1.2f, 2, Now.AddMinutes(1));
            var snapshot = _store.Read().Snapshot;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(newUuid));
            Assert.That(binding.CalibrationRevision, Is.EqualTo(2));
            Assert.That(snapshot.TryGetAnchor(oldUuid, out var oldAnchor), Is.True);
            Assert.That(oldAnchor.Status, Is.EqualTo(PhysicalAnchorRecordStatus.PendingErase));
        }

        [Test]
        public void Recalibrate_KeepsAnchorAndIncrementsRevision()
        {
            var uuid = Guid.NewGuid();
            Assert.That(_store.RegisterAndAssign(_pointId, uuid, Pose.identity, 1f, 0, Now).Succeeded, Is.True);

            var result = _store.Recalibrate(
                _pointId,
                new Pose(new Vector3(0.1f, 0.2f, 0.3f), Quaternion.Euler(1f, 2f, 3f)),
                1.1f,
                1,
                Now.AddMinutes(1));

            Assert.That(result.Succeeded, Is.True);
            var snapshot = _store.Read().Snapshot;
            Assert.That(snapshot.TryGetBinding(_pointId, out var binding), Is.True);
            Assert.That(binding.AnchorUuid, Is.EqualTo(uuid));
            Assert.That(binding.CalibrationRevision, Is.EqualTo(2));
            Assert.That(binding.UniformScale, Is.EqualTo(1.1f).Within(0.0001f));
        }

        [Test]
        public void AssignedAnchor_MustBeUnassignedBeforeRemoval()
        {
            var uuid = Guid.NewGuid();
            Assert.That(_store.RegisterAndAssign(_pointId, uuid, Pose.identity, 1f, 0, Now).Succeeded, Is.True);

            var rejected = _store.RemoveUnassigned(uuid, 1);
            var unassigned = _store.Unassign(uuid, 1, Now.AddMinutes(1));
            var removed = _store.RemoveUnassigned(uuid, 2);

            Assert.That(rejected.FailureCode, Is.EqualTo(PhysicalAnchorBindingFailureCode.AnchorStillAssigned));
            Assert.That(unassigned.Succeeded, Is.True);
            Assert.That(removed.Succeeded, Is.True);
            Assert.That(_store.Read().Snapshot.Anchors, Is.Empty);
        }

        [Test]
        public void Snapshot_RejectsDuplicatePointAndUuidAuthorities()
        {
            var uuid = Guid.NewGuid();
            var anchors = new[]
            {
                new PhysicalAnchorRecord(
                    uuid,
                    PhysicalAnchorRecordStatus.Assigned,
                    Now,
                    1,
                    string.Empty)
            };
            var binding = new PhysicalAnchorBinding(_pointId, uuid, Pose.identity, 1f, 1, Now);

            Assert.Throws<ArgumentException>(() => new PhysicalAnchorBindingSnapshot(1, anchors, new[] { binding, binding }));
            Assert.Throws<ArgumentException>(() => new PhysicalAnchorBindingSnapshot(1, new[] { anchors[0], anchors[0] }, new[] { binding }));
            Assert.Throws<ArgumentException>(() => new PhysicalAnchorBindingSnapshot(
                1,
                new[]
                {
                    anchors[0],
                    new PhysicalAnchorRecord(Guid.NewGuid(), PhysicalAnchorRecordStatus.Unassigned, Now, 1, string.Empty)
                },
                new[] { binding }));
        }

        [Test]
        public void AdminSession_IndexFailureBecomesRetryablePendingIndex()
        {
            var storage = new InMemoryStorage { ThrowOnReplace = true };
            var store = new PhysicalAnchorBindingStore(storage);
            var session = new PhysicalAnchorAdminBindingSession(store, store, () => Now);
            var uuid = Guid.NewGuid();

            var failed = session.RegisterSavedAnchor(uuid);

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.PendingIndex, Is.True);
            Assert.That(session.TryRead(out var pending, out _), Is.True);
            Assert.That(pending.Records, Has.Count.EqualTo(1));
            Assert.That(pending.Records[0].Status, Is.EqualTo(PhysicalAnchorAdminRecordStatus.PendingIndex));

            storage.ThrowOnReplace = false;
            var retried = session.RegisterSavedAnchor(uuid);
            Assert.That(retried.Succeeded, Is.True);
            Assert.That(session.TryRead(out var indexed, out _), Is.True);
            Assert.That(indexed.Records[0].Status, Is.EqualTo(PhysicalAnchorAdminRecordStatus.Unassigned));
        }

        [Test]
        public void AdminSession_ReplacementSwitchesBindingBeforeOldAnchorCleanup()
        {
            var session = new PhysicalAnchorAdminBindingSession(_store, _store, () => Now);
            var oldUuid = Guid.NewGuid();
            var newUuid = Guid.NewGuid();
            Assert.That(session.RegisterSavedAnchor(oldUuid).Succeeded, Is.True);
            Assert.That(session.AssignOrRecalibrate(_pointId.Value, oldUuid, Pose.identity, 1f).Succeeded, Is.True);
            Assert.That(session.RenameAnchor(oldUuid, "主展品").Succeeded, Is.True);
            Assert.That(session.RegisterSavedAnchor(newUuid).Succeeded, Is.True);

            var swapped = session.AssignOrRecalibrate(
                _pointId.Value,
                newUuid,
                new Pose(Vector3.up, Quaternion.identity),
                1.2f);

            Assert.That(swapped.Succeeded, Is.True);
            Assert.That(session.TryRead(out var snapshot, out _), Is.True);
            Assert.That(snapshot.Records, Has.Count.EqualTo(2));
            Assert.That(snapshot.Records.Single(record => record.Uuid == oldUuid).Status,
                Is.EqualTo(PhysicalAnchorAdminRecordStatus.PendingErase));
            var current = snapshot.Records.Single(record => record.Uuid == newUuid);
            Assert.That(current.Status, Is.EqualTo(PhysicalAnchorAdminRecordStatus.Assigned));
            Assert.That(current.CalibrationRevision, Is.EqualTo(2));
            Assert.That(current.DisplayNumber, Is.EqualTo(1));
            Assert.That(current.DisplayLabel, Is.EqualTo("锚点 01 · 主展品"));
            var replaced = snapshot.Records.Single(record => record.Uuid == oldUuid);
            Assert.That(replaced.DisplayNumber, Is.EqualTo(2),
                "The replacement's temporary number moves to the retained old PendingErase record.");
            Assert.That(session.PrepareErase(oldUuid).Succeeded, Is.True);
            Assert.That(session.TryRead(out var beforePlatformErase, out _), Is.True);
            Assert.That(beforePlatformErase.Records.Single(record => record.Uuid == newUuid).Status,
                Is.EqualTo(PhysicalAnchorAdminRecordStatus.Assigned));
            Assert.That(session.CompleteErase(oldUuid).Succeeded, Is.True);
            Assert.That(session.TryRead(out var cleaned, out _), Is.True);
            Assert.That(cleaned.Records.Single().Uuid, Is.EqualTo(newUuid));
        }

        [Test]
        public void AdminSession_PrepareEraseRemovesBindingBeforePlatformCompletion()
        {
            var session = new PhysicalAnchorAdminBindingSession(_store, _store, () => Now);
            var uuid = Guid.NewGuid();
            Assert.That(session.RegisterSavedAnchor(uuid).Succeeded, Is.True);
            Assert.That(session.AssignOrRecalibrate(_pointId.Value, uuid, Pose.identity, 1f).Succeeded, Is.True);

            var prepared = session.PrepareErase(uuid);
            var beforePlatformErase = _store.Read().Snapshot;

            Assert.That(prepared.Succeeded, Is.True);
            Assert.That(beforePlatformErase.Bindings, Is.Empty);
            Assert.That(beforePlatformErase.TryGetAnchor(uuid, out var anchor), Is.True);
            Assert.That(anchor.Status, Is.EqualTo(PhysicalAnchorRecordStatus.PendingErase));
            Assert.That(session.CompleteErase(uuid).Succeeded, Is.True);
            Assert.That(_store.Read().Snapshot.Anchors, Is.Empty);
        }

        [Test]
        public void PendingEraseAnchor_IsNeverRevivedByConfiguredDisplayNumber()
        {
            var uuid = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(uuid, 0, Now).Succeeded, Is.True);
            Assert.That(_store.Unassign(uuid, 1, Now.AddMinutes(1)).Succeeded, Is.True);

            var reconciliation = _store.ReconcileConfiguredBindings(
                new[] { new PhysicalAnchorConfiguredBinding(_pointId, 1) },
                2,
                Now.AddMinutes(2));
            var snapshot = _store.Read().Snapshot;

            Assert.That(reconciliation.Succeeded, Is.True);
            Assert.That(snapshot.Bindings, Is.Empty);
            Assert.That(snapshot.TryGetAnchor(uuid, out var anchor), Is.True);
            Assert.That(anchor.Status, Is.EqualTo(PhysicalAnchorRecordStatus.PendingErase));
        }

        [Test]
        public void ExplicitLegacyImportCreatesOnlyUnassignedRecordsAndThenMarksComplete()
        {
            var existing = Guid.NewGuid();
            var imported = Guid.NewGuid();
            Assert.That(_store.RegisterUnassigned(existing, 0, Now).Succeeded, Is.True);
            var legacy = new FakeLegacyMigrationSource(existing, imported);
            var session = new PhysicalAnchorAdminBindingSession(_store, _store, () => Now);

            var result = session.ImportLegacyAsUnassigned(legacy, out var importedCount);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(importedCount, Is.EqualTo(1));
            Assert.That(legacy.Completed, Is.True);
            var snapshot = _store.Read().Snapshot;
            Assert.That(snapshot.Anchors, Has.Count.EqualTo(2));
            Assert.That(snapshot.Bindings, Is.Empty);
            Assert.That(snapshot.Anchors.All(anchor => anchor.Status == PhysicalAnchorRecordStatus.Unassigned), Is.True);
        }

        [Test]
        public void AnchorLocalPoseCompositionUsesAnchorRotationBeforeTranslation()
        {
            var anchor = new Pose(new Vector3(10f, 2f, -3f), Quaternion.Euler(0f, 90f, 0f));
            var local = new Pose(new Vector3(0f, 0f, 1f), Quaternion.Euler(10f, 0f, 0f));

            var resolved = PhysicalAnchorPoseResolver.Resolve(anchor, local);

            Assert.That(resolved.position, Is.EqualTo(anchor.position + anchor.rotation * local.position));
            Assert.That(Quaternion.Angle(resolved.rotation, anchor.rotation * local.rotation), Is.LessThan(0.001f));
        }

        sealed class InMemoryStorage : IPhysicalAnchorBindingStorage
        {
            public string Content { get; set; }
            public bool ThrowOnReplace { get; set; }
            public bool Exists => Content != null;
            public string ReadAllText() => Content;
            public void ReplaceAllText(string content)
            {
                if (ThrowOnReplace) throw new InvalidOperationException("Injected write failure.");
                Content = content;
            }
        }

        static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
            return builder.ToString();
        }

        [Serializable]
        sealed class LegacyStoreDocument
        {
            public int schemaVersion;
            public long storeVersion;
            public LegacyAnchorDocument[] anchors;
            public LegacyBindingDocument[] bindings;
            public string checksum;
        }

        [Serializable]
        sealed class LegacyAnchorDocument
        {
            public string uuid;
            public int status;
            public string maintainedAtUtc;
        }

        [Serializable]
        sealed class LegacyBindingDocument
        {
            public string pointId;
            public string anchorUuid;
            public Vector3 position;
            public Quaternion rotation;
            public float uniformScale;
            public long calibrationRevision;
            public string confirmedAtUtc;
        }


        sealed class FakeLegacyMigrationSource : ILegacyAnchorUuidMigrationSource
        {
            readonly Guid[] _candidates;
            public FakeLegacyMigrationSource(params Guid[] candidates) => _candidates = candidates;
            public bool Completed { get; private set; }
            public Guid[] ReadCandidates() => _candidates;
            public void CompleteMigration() => Completed = true;
        }
    }
}
