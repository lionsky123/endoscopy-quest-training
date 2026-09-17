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
using UnityEngine.Serialization;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;

/// <summary>
/// Manages UI of anchor sample.
/// </summary>
[RequireComponent(typeof(SpatialAnchorLoader))]
[MetaCodeSample("StarterSample-SpatialAnchor")]
public class AnchorUIManager : MonoBehaviour, IOfficialSpatialAnchorLifecycle
{
    /// <summary>
    /// Anchor UI manager singleton instance
    /// </summary>
    public static AnchorUIManager Instance;

    /// <summary>
    /// Anchor Mode switches between create and select
    /// </summary>
    public enum AnchorMode
    {
        Create,
        Select
    };

    [SerializeField, FormerlySerializedAs("createModeButton_")]
    private GameObject _createModeButton;

    [SerializeField, FormerlySerializedAs("selectModeButton_")]
    private GameObject _selectModeButton;

    [SerializeField, FormerlySerializedAs("trackedDevice_")]
    private Transform _trackedDevice;

    private Transform _raycastOrigin;

    private bool _drawRaycast = false;

    [SerializeField, FormerlySerializedAs("lineRenderer_")]
    private LineRenderer _lineRenderer;

    private Anchor _hoveredAnchor;
    private SpatialAnchorLoader _loader;

    private AnchorMode _mode = AnchorMode.Select;

    public AnchorMode Mode => _mode;
    public Anchor HoveredAnchor => _hoveredAnchor;
    public event Action<IOfficialSpatialAnchorHandle> AnchorPlaced;
    public event Action<AnchorMode> ModeChanged;
    public event Action<bool> CreateModeChanged;
    public event Action AnchorsChanged;
    public event Action<OfficialAnchorLoadState> LoadStateChanged;

    readonly List<Anchor> _activeAnchors = new List<Anchor>();
    IOfficialSpatialAnchorAdminCommands _eraseGuard;

    [SerializeField]
    private Anchor _anchorPrefab;

    public Anchor AnchorPrefab => _anchorPrefab;
    public IReadOnlyList<Anchor> ActiveAnchors => _activeAnchors;
    bool IOfficialSpatialAnchorLifecycle.IsCreateMode => _mode == AnchorMode.Create;
    public bool IsLoading => ResolveLoader() != null && ResolveLoader().IsLoading;
    public OfficialAnchorLoadState LoadState => ResolveLoader() != null
        ? ResolveLoader().LoadState
        : new OfficialAnchorLoadState(OfficialAnchorLoadPhase.Idle, string.Empty);
    IOfficialSpatialAnchorHandle IOfficialSpatialAnchorLifecycle.HoveredAnchor => _hoveredAnchor;
    IReadOnlyList<IOfficialSpatialAnchorHandle> IOfficialSpatialAnchorLifecycle.ActiveAnchors
        => _activeAnchors.Cast<IOfficialSpatialAnchorHandle>().ToArray();

    [SerializeField, FormerlySerializedAs("placementPreview_")]
    private GameObject _placementPreview;

    [SerializeField, FormerlySerializedAs("anchorPlacementTransform_")]
    private Transform _anchorPlacementTransform;

    [SerializeField]
    private Canvas _placementInstructionCanvas;

    // Matches the drawn raycast line length; anchors beyond it show no hover feedback anyway.
    private const float MaxRaycastDistance = 10f;

    #region Monobehaviour Methods

    private void Awake()
    {
        _loader = GetComponent<SpatialAnchorLoader>();
        if (_loader != null) _loader.LoadStateChanged += HandleLoadStateChanged;
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_loader != null) _loader.LoadStateChanged -= HandleLoadStateChanged;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        _raycastOrigin = _trackedDevice;

        // Start in select mode
        _mode = AnchorMode.Select;
        StartSelectMode();

        _lineRenderer.startWidth = 0.005f;
        _lineRenderer.endWidth = 0.005f;

        GetComponent<SpatialAnchorLoader>().LoadAnchorsByUuid();
    }

    private void Update()
    {
        if (_mode == AnchorMode.Create)
        {
            DrawPlacementGuide();
        }
        else if (_drawRaycast)
        {
            ControllerRaycast();
        }

    }

    #endregion // Monobehaviour Methods


    #region Menu UI Callbacks

    /// <summary>
    /// Create mode button pressed UI callback. Referenced by the Create button in the menu.
    /// </summary>
    public void OnCreateModeButtonPressed()
    {
        SetCreateMode(_mode != AnchorMode.Create);
    }

    /// <summary>
    /// Load anchors button pressed UI callback. Referenced by the Load Anchors button in the menu.
    /// </summary>
    public void OnLoadAnchorsButtonPressed()
    {
        RequestLoad();
    }

    public void RequestLoad() => ResolveLoader()?.LoadAnchorsByUuid();

    #endregion // Menu UI Callbacks

    public void RegisterAnchor(Anchor anchor)
    {
        if (anchor == null || _activeAnchors.Contains(anchor)) return;
        _activeAnchors.Add(anchor);
        AnchorsChanged?.Invoke();
    }

    public void UnregisterAnchor(Anchor anchor)
    {
        if (anchor == null || !_activeAnchors.Remove(anchor)) return;
        if (_hoveredAnchor == anchor) _hoveredAnchor = null;
        AnchorsChanged?.Invoke();
    }

    public void NotifyAnchorStateChanged(Anchor anchor)
    {
        if (anchor != null && _activeAnchors.Contains(anchor))
            AnchorsChanged?.Invoke();
    }

    public void DestroyAllLoadedAnchors()
    {
        var loaded = _activeAnchors.ToArray();
        _activeAnchors.Clear();
        _hoveredAnchor = null;
        for (var index = 0; index < loaded.Length; index++)
        {
            var anchor = loaded[index];
            if (anchor == null) continue;
            if (Application.isPlaying) Destroy(anchor.gameObject);
            else DestroyImmediate(anchor.gameObject);
        }
        AnchorsChanged?.Invoke();
    }

    public void SetEraseGuard(IOfficialSpatialAnchorAdminCommands eraseGuard)
    {
        _eraseGuard = eraseGuard;
    }

    public bool TryPrepareEraseAnchor(Guid uuid, out string reason)
    {
        if (_eraseGuard == null)
        {
            reason = string.Empty;
            return true;
        }
        return _eraseGuard.TryPrepareEraseAnchor(uuid, out reason);
    }

    #region Mode Handling

    public void SetCreateMode(bool createMode)
    {
        var requestedMode = createMode ? AnchorMode.Create : AnchorMode.Select;
        if (_mode == requestedMode) return;

        if (requestedMode == AnchorMode.Create)
        {
            _mode = AnchorMode.Create;
            EndSelectMode();
            StartPlacementMode();
        }
        else
        {
            _mode = AnchorMode.Select;
            EndPlacementMode();
            StartSelectMode();
        }

        // The single Admin workspace owns the placement action. Its panel is hidden
        // during Create mode, so the action remains authored and returns with it.
        if (_createModeButton) _createModeButton.SetActive(true);
        if (_selectModeButton) _selectModeButton.SetActive(false);
        ModeChanged?.Invoke(_mode);
        CreateModeChanged?.Invoke(_mode == AnchorMode.Create);
    }

    private void StartPlacementMode()
    {
        ShowAnchorPreview();
        ShowPlacementGuide();
        SetPlacementInstructionVisible(true);
    }

    private void EndPlacementMode()
    {
        SetPlacementInstructionVisible(false);
        HideAnchorPreview();
        HidePlacementGuide();
    }

    private void StartSelectMode()
    {
        SetPlacementInstructionVisible(false);
        ShowRaycastLine();
    }

    private void EndSelectMode()
    {
        HideRaycastLine();
    }

    #endregion // Mode Handling


    #region Private Methods

    private void ShowAnchorPreview()
    {
        _placementPreview.SetActive(true);
    }

    private void HideAnchorPreview()
    {
        _placementPreview.SetActive(false);
    }

    public void ShowCandidateReviewPose(Pose worldPose)
    {
        if (_anchorPlacementTransform == null || _placementPreview == null) return;
        _anchorPlacementTransform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
        _placementPreview.SetActive(true);
    }

    public void HideCandidateReviewPose()
    {
        HideAnchorPreview();
    }

    private void ShowPlacementGuide()
    {
        _lineRenderer.gameObject.SetActive(true);
        DrawPlacementGuide();
    }

    private void HidePlacementGuide()
    {
        _lineRenderer.gameObject.SetActive(false);
    }

    private void SetPlacementInstructionVisible(bool visible)
    {
        if (_placementInstructionCanvas != null)
        {
            _placementInstructionCanvas.enabled = visible;
        }
    }

    private void DrawPlacementGuide()
    {
        if (!_lineRenderer.gameObject.activeInHierarchy) return;
        _lineRenderer.SetPosition(0, _trackedDevice.position);
        _lineRenderer.SetPosition(1, _anchorPlacementTransform.position);
    }

    public bool PlaceAnchorAtPose(Pose worldPose)
    {
        if (_anchorPrefab == null) return false;
        var anchor = Instantiate(
            _anchorPrefab,
            worldPose.position,
            worldPose.rotation);
        AnchorPlaced?.Invoke(anchor);
        return true;
    }

    public async Task<OfficialAnchorEraseResult> EraseAnchorsAsync(
        IReadOnlyList<Guid> anchorUuids)
    {
        var uuids = anchorUuids == null
            ? Array.Empty<Guid>()
            : anchorUuids.Where(uuid => uuid != Guid.Empty).Distinct().ToArray();
        if (uuids.Length == 0)
            return new OfficialAnchorEraseResult(true, "no_anchors");
        var result = await OVRSpatialAnchor.EraseAnchorsAsync(null, uuids);
        return new OfficialAnchorEraseResult(result.Success, result.Status.ToString());
    }

    SpatialAnchorLoader ResolveLoader()
    {
        if (_loader == null) _loader = GetComponent<SpatialAnchorLoader>();
        return _loader;
    }

    void HandleLoadStateChanged(OfficialAnchorLoadState state)
        => LoadStateChanged?.Invoke(state);

    private void ShowRaycastLine()
    {
        _drawRaycast = true;
        _lineRenderer.gameObject.SetActive(true);
    }

    private void HideRaycastLine()
    {
        _drawRaycast = false;
        _lineRenderer.gameObject.SetActive(false);
    }

    private void ControllerRaycast()
    {
        Ray ray = new Ray(_raycastOrigin.position, _raycastOrigin.TransformDirection(Vector3.forward));
        _lineRenderer.SetPosition(0, _raycastOrigin.position);
        _lineRenderer.SetPosition(1,
            _raycastOrigin.position + _raycastOrigin.TransformDirection(Vector3.forward) * MaxRaycastDistance);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, MaxRaycastDistance))
        {
            Anchor anchorObject = hit.collider.GetComponent<Anchor>();
            if (anchorObject != null)
            {
                _lineRenderer.SetPosition(1, hit.point);

                HoverAnchor(anchorObject);
                return;
            }
        }

        UnhoverAnchor();
    }

    private void HoverAnchor(Anchor anchor)
    {
        UnhoverAnchor();
        _hoveredAnchor = anchor;
        _hoveredAnchor.OnHoverStart();
    }

    private void UnhoverAnchor()
    {
        if (_hoveredAnchor == null)
        {
            return;
        }

        _hoveredAnchor.OnHoverEnd();
        _hoveredAnchor = null;
    }

    #endregion // Private Methods
}
