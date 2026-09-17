/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Linq;
using Meta.XR.Samples;
using UnityEngine;

/// <summary>
/// Read-only, explicitly invoked migration source for UUIDs written by the original Meta sample.
/// Production Admin and Visitor loading never read this source.
/// </summary>
[MetaCodeSample("StarterSample-SpatialAnchor")]
public sealed class LegacyAnchorUuidMigrationSource : ILegacyAnchorUuidMigrationSource
{
    const string NumUuidsPlayerPref = "numUuids";
    const string MigrationCompletedPlayerPref = "physicalAnchorLegacyMigrationCompletedV1";

    public bool IsCompleted => PlayerPrefs.GetInt(MigrationCompletedPlayerPref, 0) == 1;

    public Guid[] ReadCandidates()
    {
        if (IsCompleted) return Array.Empty<Guid>();
        var count = Math.Max(0, PlayerPrefs.GetInt(NumUuidsPlayerPref, 0));
        return Enumerable
            .Range(0, count)
            .Select(GetUuidKey)
            .Select(PlayerPrefs.GetString)
            .Select(str => Guid.TryParse(str, out var uuid) ? uuid : Guid.Empty)
            .Where(uuid => uuid != Guid.Empty)
            .Distinct()
            .OrderBy(uuid => uuid)
            .ToArray();
    }

    public void CompleteMigration()
    {
        var count = Math.Max(0, PlayerPrefs.GetInt(NumUuidsPlayerPref, 0));
        for (var index = 0; index < count; index++) PlayerPrefs.DeleteKey(GetUuidKey(index));
        PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
        PlayerPrefs.SetInt(MigrationCompletedPlayerPref, 1);
        PlayerPrefs.Save();
    }

    static string GetUuidKey(int index) => $"uuid{index}";
}

public interface ILegacyAnchorUuidMigrationSource
{
    Guid[] ReadCandidates();
    void CompleteMigration();
}
