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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meta.XR.Samples;
using UnityEngine;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;
using BotanicalGardenQR.SpatialAnchorAdmin.Persistence;

/// <summary>
/// Demonstrates loading existing spatial anchors from storage.
/// </summary>
/// <remarks>
/// Loading existing anchors involves two asynchronous methods:
/// 1. Call <see cref="OVRSpatialAnchor.LoadUnboundAnchorsAsync"/>
/// 2. For each unbound anchor you wish to localize, invoke <see cref="OVRSpatialAnchor.UnboundAnchor.Localize"/>.
/// 3. Once localized, your callback will receive an <see cref="OVRSpatialAnchor.UnboundAnchor"/>. Instantiate an
/// <see cref="OVRSpatialAnchor"/> component and bind it to the `UnboundAnchor` by calling
/// <see cref="OVRSpatialAnchor.UnboundAnchor.BindTo"/>.
/// </remarks>
[MetaCodeSample("StarterSample-SpatialAnchor")]
public class SpatialAnchorLoader : MonoBehaviour
{
    [SerializeField]
    OVRSpatialAnchor _anchorPrefab;

    readonly HashSet<Guid> _materializingUuids = new();

    bool _destroyed;
    bool _loadInProgress;
    int _loadGeneration;

    public bool IsLoading => _loadInProgress;
    public OfficialAnchorLoadState LoadState { get; private set; }
        = new OfficialAnchorLoadState(OfficialAnchorLoadPhase.Idle, string.Empty);
    public event Action<OfficialAnchorLoadState> LoadStateChanged;

    public void LoadAnchorsByUuid() => TryLoadAnchorsByUuid();

    public bool TryLoadAnchorsByUuid()
    {
        if (!TryBeginLoad(out var generation)) return false;
        _ = RunLoadAsync(generation);
        return true;
    }

    async Task RunLoadAsync(int generation)
    {
        try
        {
            var loadedCount = await LoadAnchorsByUuidAsync(generation);
            if (!IsCurrent(generation)) return;
            CompleteLoad(generation, OfficialAnchorLoadPhase.Completed, $"加载完成：新增 {loadedCount} 个锚点。");
        }
        catch (Exception exception)
        {
            if (!IsCurrent(generation)) return;
            LogError($"{nameof(LoadAnchorsByUuid)} failed: {exception}");
            CompleteLoad(generation, OfficialAnchorLoadPhase.Failed, $"加载失败：{exception.Message}");
        }
    }

    async Task<int> LoadAnchorsByUuidAsync(int generation)
    {
        if (!PhysicalAnchorAdminBindingServices.Current.TryGetIndexedUuids(out var uuids, out var diagnosticTag))
            throw new InvalidOperationException($"设备安装索引不可用：{diagnosticTag}");
        if (uuids.Length == 0)
        {
            LogWarning($"There are no anchors to load.");
            return 0;
        }

        var loadedCount = 0;
        var batchCount = Math.Ceiling((float)uuids.Length / 50);
        for (int i = 0, batchIndex = 1; i < uuids.Length; i += 50, batchIndex++)
        {
            var uuidBatch = uuids.Skip(i).Take(50).ToArray();
            Log($"Attempting to load batch {batchIndex} of {batchCount} with {uuidBatch.Length} anchor(s) by UUID: " +
                $"[{string.Join($", ", uuidBatch.Select(uuid => uuid.ToString()))}]");

            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuidBatch, unboundAnchors);
            if (!IsCurrent(generation)) return loadedCount;

            if (result.Success)
            {
                loadedCount += await ProcessUnboundAnchors(result.Value, generation);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{nameof(OVRSpatialAnchor.LoadUnboundAnchorsAsync)} failed with error {result.Status}.");
            }
        }
        return loadedCount;
    }

    private void OnDestroy()
    {
        _destroyed = true;
        _loadInProgress = false;
        _loadGeneration++;
        _materializingUuids.Clear();
    }

    private async Task<int> ProcessUnboundAnchors(
        IReadOnlyList<OVRSpatialAnchor.UnboundAnchor> unboundAnchors,
        int generation)
    {
        if (!IsCurrent(generation)) return 0;

        Log($"{nameof(OVRSpatialAnchor.LoadUnboundAnchorsAsync)} found {unboundAnchors.Count} unbound anchors: " +
            $"[{string.Join(", ", unboundAnchors.Select(a => a.Uuid.ToString()))}]");

        var tasks = new List<Task<bool>>(unboundAnchors.Count);
        foreach (var anchor in unboundAnchors)
        {
            if (!IsCurrent(generation)) break;
            if (anchor.Localizing && !anchor.Localized) continue;
            tasks.Add(LocalizeAndBindAnchorAsync(anchor, generation));
        }
        var results = await Task.WhenAll(tasks);
        return results.Count(loaded => loaded);
    }

    private async Task<bool> LocalizeAndBindAnchorAsync(
        OVRSpatialAnchor.UnboundAnchor unboundAnchor,
        int generation)
    {
        if (!unboundAnchor.Localized)
        {
            var success = await unboundAnchor.LocalizeAsync();
            if (!IsCurrent(generation)) return false;
            if (!success)
                throw new InvalidOperationException($"{unboundAnchor} Localization failed.");
        }
        return BindLocalizedAnchor(unboundAnchor, generation);
    }

    private bool BindLocalizedAnchor(
        OVRSpatialAnchor.UnboundAnchor unboundAnchor,
        int generation)
    {
        if (!IsCurrent(generation)) return false;
        var manager = GetComponent<AnchorUIManager>();
        if (manager != null && manager.ActiveAnchors.Any(anchor =>
                anchor != null && anchor.Uuid == unboundAnchor.Uuid))
        {
            LogWarning($"Skipping already loaded anchor {unboundAnchor.Uuid}.");
            return false;
        }
        if (!_materializingUuids.Add(unboundAnchor.Uuid)) return false;

        var isPoseValid = unboundAnchor.TryGetPose(out var pose);
        if (!isPoseValid)
        {
            Debug.LogWarning("Unable to acquire initial anchor pose. Instantiating prefab at the origin.");
        }

        OVRSpatialAnchor spatialAnchor = null;
        try
        {
            spatialAnchor = isPoseValid
                ? Instantiate(_anchorPrefab, pose.position, pose.rotation)
                : Instantiate(_anchorPrefab);
            unboundAnchor.BindTo(spatialAnchor);
            if (!spatialAnchor.TryGetComponent<Anchor>(out var anchor))
                throw new InvalidOperationException("Loaded anchor prefab is missing the official Anchor component.");
            // We just loaded it, so we know it exists in persistent storage.
            anchor.ShowSaveIcon = true;
            return true;
        }
        catch
        {
            _materializingUuids.Remove(unboundAnchor.Uuid);
            if (spatialAnchor != null)
            {
                if (Application.isPlaying) Destroy(spatialAnchor.gameObject);
                else DestroyImmediate(spatialAnchor.gameObject);
            }
            throw;
        }
    }

    bool TryBeginLoad(out int generation)
    {
        generation = _loadGeneration;
        if (_destroyed || _loadInProgress)
        {
            if (_loadInProgress) LogWarning("Anchor loading is already in progress; ignoring duplicate request.");
            return false;
        }
        generation = ++_loadGeneration;
        _loadInProgress = true;
        PublishLoadState(OfficialAnchorLoadPhase.Loading, "正在加载已保存锚点…");
        return true;
    }

    bool IsCurrent(int generation) => !_destroyed && generation == _loadGeneration;

    void CompleteLoad(int generation, OfficialAnchorLoadPhase phase, string message)
    {
        if (!IsCurrent(generation)) return;
        _loadInProgress = false;
        _materializingUuids.Clear();
        PublishLoadState(phase, message);
    }

    void PublishLoadState(OfficialAnchorLoadPhase phase, string message)
    {
        LoadState = new OfficialAnchorLoadState(phase, message);
        try
        {
            LoadStateChanged?.Invoke(LoadState);
        }
        catch (Exception exception)
        {
            LogError($"Load-state subscriber failed: {exception}");
        }
    }

    private static void Log(LogType logType, object message)
        => Debug.unityLogger.Log(logType, "[SpatialAnchorSample]", message);

    private static void Log(object message) => Log(LogType.Log, message);

    private static void LogWarning(object message) => Log(LogType.Warning, message);

    private static void LogError(object message) => Log(LogType.Error, message);
}
