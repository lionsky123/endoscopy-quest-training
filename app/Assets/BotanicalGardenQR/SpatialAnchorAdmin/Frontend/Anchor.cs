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
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Meta.XR.Samples;
using UnityEngine.Serialization;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;
using BotanicalGardenQR.SpatialAnchorAdmin.Persistence;

/// <summary>
/// Specific functionality for spawned anchors
/// </summary>
[RequireComponent(typeof(OVRSpatialAnchor))]
[MetaCodeSample("StarterSample-SpatialAnchor")]
public class Anchor : MonoBehaviour, IOfficialSpatialAnchorHandle
{
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    [SerializeField, FormerlySerializedAs("canvas_")]
    private Canvas _canvas;

    [SerializeField, FormerlySerializedAs("pivot_")]
    private Transform _pivot;

    [SerializeField, FormerlySerializedAs("anchorMenu_")]
    private GameObject _anchorMenu;

    private bool _isSelected;

    private bool _isHovered;

    [SerializeField, FormerlySerializedAs("anchorName_")]
    private TextMeshProUGUI _anchorName;

    [SerializeField, FormerlySerializedAs("saveIcon_")]
    private GameObject _saveIcon;

    [SerializeField, FormerlySerializedAs("labelImage_")]
    private Image _labelImage;

    [SerializeField, FormerlySerializedAs("labelBaseColor_")]
    private Color _labelBaseColor;

    [SerializeField, FormerlySerializedAs("labelHighlightColor_")]
    private Color _labelHighlightColor;

    [SerializeField, FormerlySerializedAs("labelSelectedColor_")]
    private Color _labelSelectedColor;

    [SerializeField, FormerlySerializedAs("uiManager_")]
    private AnchorUIManager _uiManager;

    [SerializeField, FormerlySerializedAs("renderers_")]
    private MeshRenderer[] _renderers;

    private MaterialPropertyBlock _emissionPropertyBlock;

    private OVRSpatialAnchor _spatialAnchor;

    private GameObject _icon;

    private bool _isSaved;

    private bool _isSaveInProgress;

    private bool _isPendingIndex;
    private string _adminDisplayLabel = string.Empty;
    private string _adminRecordStatus = string.Empty;

    private Camera _mainCamera;

    private bool _hasNameState;
    private bool _lastCreated;
    private bool _lastTracked;
    private bool _lastSaved;
    private Guid _lastUuid;
    private string _lastAdminDisplayLabel = string.Empty;
    private string _lastAdminRecordStatus = string.Empty;

    public event Action<IOfficialSpatialAnchorHandle> Ready;
    public event Action<IOfficialSpatialAnchorHandle> SaveSucceeded;
    public event Action<IOfficialSpatialAnchorHandle, string> SaveFailed;
    public event Action<IOfficialSpatialAnchorHandle, string> IndexingFailed;

    #region Monobehaviour Methods

    private void Awake()
    {
        if (_anchorMenu) _anchorMenu.SetActive(false);
        _renderers = GetComponentsInChildren<MeshRenderer>();
        _mainCamera = Camera.main;
        if (_canvas)
        {
            _canvas.worldCamera = _mainCamera;
            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = false;
            foreach (var graphic in _canvas.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        _spatialAnchor = GetComponent<OVRSpatialAnchor>();
        _icon = GetComponent<Transform>().FindChildRecursive("Sphere").gameObject;
        _isSaved = _saveIcon.activeSelf;
    }

    private IEnumerator Start()
    {
        while (_spatialAnchor && _spatialAnchor.PendingCreation)
        {
            yield return null;
        }

        if (_spatialAnchor)
        {
            AnchorUIManager.Instance?.RegisterAnchor(this);
            SetAnchorName();
            Ready?.Invoke(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetAnchorName()
    {
        if (!_spatialAnchor) return;
        bool created = _spatialAnchor.Created;
        bool tracked = _spatialAnchor.IsTracked;
        Guid uuid = _spatialAnchor.Uuid;
        if (_hasNameState &&
            created == _lastCreated &&
            tracked == _lastTracked &&
            uuid == _lastUuid &&
            _isSaved == _lastSaved &&
            string.Equals(_adminDisplayLabel, _lastAdminDisplayLabel, StringComparison.Ordinal) &&
            string.Equals(_adminRecordStatus, _lastAdminRecordStatus, StringComparison.Ordinal))
        {
            return;
        }

        _hasNameState = true;
        _lastCreated = created;
        _lastTracked = tracked;
        _lastUuid = uuid;
        _lastSaved = _isSaved;
        _lastAdminDisplayLabel = _adminDisplayLabel;
        _lastAdminRecordStatus = _adminRecordStatus;

        if (!created)
        {
            _anchorName.text = "锚点创建失败";
            return;
        }

        var trackingState = tracked ? "已跟踪" : "未跟踪";
        var saveState = _isSaved ? "已保存" : "未保存";
        var displayLabel = string.IsNullOrWhiteSpace(_adminDisplayLabel)
            ? "锚点（待写入编号）"
            : _adminDisplayLabel;
        var recordState = string.IsNullOrWhiteSpace(_adminRecordStatus)
            ? string.Empty
            : $" · {_adminRecordStatus}";
        _anchorName.text = $"{displayLabel}\n{trackingState} · {saveState}{recordState}";
    }

    private void Update()
    {
        if (!_mainCamera)
        {
            _mainCamera = Camera.main;
        }

        // Billboard the boundary
        BillboardPanel(_canvas.transform);

        // Billboard the menu
        BillboardPanel(_pivot);

        //Billboard the icon
        BillboardPanel(_icon.transform);

        if (_spatialAnchor)
        {
            SetAnchorName();
        }
    }

    private void OnDisable()
    {
        _isHovered = false;
        SetEmissionColor(Color.clear);
    }

    #endregion // MonoBehaviour Methods

    #region UI Event Listeners

    /// <summary>
    /// UI callback for the anchor menu's Save button
    /// </summary>
    public void OnSaveLocalButtonPressed()
    {
        RequestSaveLocal();
    }

    public bool RequestSaveLocal()
    {
        if (!_spatialAnchor || !_spatialAnchor.Created || _spatialAnchor.Uuid == Guid.Empty ||
            _isSaved || _isSaveInProgress)
            return false;

        _isSaveInProgress = true;
        try
        {
            _spatialAnchor.SaveAnchorAsync().ContinueWith((result, anchor) =>
            {
                anchor._isSaveInProgress = false;
                if (result.Success)
                {
                    anchor.OnSave();
                    return;
                }

                var message = $"Failed to save anchor {anchor._spatialAnchor.Uuid} with error {result.Status}.";
                Debug.LogError(message, anchor);
                anchor.SaveFailed?.Invoke(anchor, message);
            }, this);
            return true;
        }
        catch (Exception exception)
        {
            _isSaveInProgress = false;
            var message = $"Failed to submit anchor {_spatialAnchor.Uuid} for saving: {exception.Message}";
            Debug.LogError(message, this);
            SaveFailed?.Invoke(this, message);
            return false;
        }
    }

    void OnSave()
    {
        ShowSaveIcon = true;
        (_uiManager != null ? _uiManager : AnchorUIManager.Instance)
            ?.NotifyAnchorStateChanged(this);
        var index = PhysicalAnchorAdminBindingServices.Current.RegisterSavedAnchor(_spatialAnchor.Uuid);
        if (index.Succeeded)
        {
            _isPendingIndex = false;
            SaveSucceeded?.Invoke(this);
            return;
        }

        _isPendingIndex = true;
        var message = "Meta 锚点已保存，但设备安装索引写入失败。请留在管理员模式并重试索引，或显式擦除该锚点。";
        Debug.LogError($"[PhysicalAnchorAdmin] {message} ({index.DiagnosticTag})", this);
        IndexingFailed?.Invoke(this, message);
    }

    public bool RetryPendingIndex(out string error)
    {
        error = string.Empty;
        if (!_isPendingIndex || !_spatialAnchor || _spatialAnchor.Uuid == Guid.Empty)
        {
            error = "当前锚点没有待重试的安装索引。";
            return false;
        }

        var result = PhysicalAnchorAdminBindingServices.Current.RegisterSavedAnchor(_spatialAnchor.Uuid);
        if (!result.Succeeded)
        {
            error = "安装索引仍未写入，请检查设备存储后重试。";
            return false;
        }
        _isPendingIndex = false;
        SaveSucceeded?.Invoke(this);
        return true;
    }

    /// <summary>
    /// UI callback for the anchor menu's Hide button
    /// </summary>
    public void OnHideButtonPressed()
    {
        Destroy(gameObject);
    }

    /// <summary>
    /// UI callback for the anchor menu's Erase button
    /// </summary>
    public void OnEraseButtonPressed()
    {
        if (!_spatialAnchor) return;

        var manager = _uiManager != null ? _uiManager : AnchorUIManager.Instance;
        if (manager != null && !manager.TryPrepareEraseAnchor(_spatialAnchor.Uuid, out var reason))
        {
            Debug.LogWarning($"Cannot erase spatial anchor {_spatialAnchor.Uuid}: {reason}", this);
            return;
        }

        EraseAnchor();
    }

    void EraseAnchor()
    {
        _spatialAnchor.EraseAnchorAsync().ContinueWith((result, anchor) =>
        {
            if (result.Success)
            {
                anchor.OnErase();
            }
            else
            {
                Debug.LogError($"Failed to erase anchor {anchor._spatialAnchor.Uuid} with result {result.Status}");
            }
        }, this);
    }

    void OnErase()
    {
        var cleanup = PhysicalAnchorAdminBindingServices.Current.CompleteErase(_spatialAnchor.Uuid);
        if (!cleanup.Succeeded)
            Debug.LogError(
                $"[PhysicalAnchorAdmin] Meta 锚点已擦除，但待清理安装记录移除失败；可从管理员清理页重试。 ({cleanup.DiagnosticTag})",
                this);
        ShowSaveIcon = false;
        (_uiManager != null ? _uiManager : AnchorUIManager.Instance)
            ?.NotifyAnchorStateChanged(this);
        Destroy(gameObject);
    }

    #endregion // UI Event Listeners

    #region Public Methods

    public bool ShowSaveIcon
    {
        set
        {
            _isSaved = value;
            _saveIcon.SetActive(value);
            SetAnchorName();
        }
    }

    public Guid Uuid => _spatialAnchor ? _spatialAnchor.Uuid : Guid.Empty;
    public bool IsCreated => _spatialAnchor && _spatialAnchor.Created;
    public bool IsTracked => _spatialAnchor && _spatialAnchor.IsTracked;
    public bool IsSaved => _isSaved && Uuid != Guid.Empty;
    public bool IsSaveInProgress => _isSaveInProgress;
    public bool IsPendingIndex => _isPendingIndex;
    public Pose WorldPose => new Pose(transform.position, transform.rotation);

    public void SetAdminDisplayIdentity(string displayLabel, string recordStatus)
    {
        _adminDisplayLabel = displayLabel ?? string.Empty;
        _adminRecordStatus = recordStatus ?? string.Empty;
        _hasNameState = false;
        SetAnchorName();
    }

    public bool DiscardIfUnsaved()
    {
        if (_isSaved || _isSaveInProgress) return false;
        Destroy(gameObject);
        return true;
    }

    public void Release()
    {
        if (this) Destroy(gameObject);
    }

    /// <summary>
    /// Handles interaction when anchor is hovered
    /// </summary>
    public void OnHoverStart()
    {
        if (_isHovered)
        {
            return;
        }

        _isHovered = true;

        SetEmissionColor(Color.yellow);

        _labelImage.color = _labelHighlightColor;
    }

    /// <summary>
    /// Handles interaction when anchor is no longer hovered
    /// </summary>
    public void OnHoverEnd()
    {
        if (!_isHovered)
        {
            return;
        }

        _isHovered = false;

        SetEmissionColor(Color.clear);

        if (_isSelected)
        {
            _labelImage.color = _labelSelectedColor;
        }
        else
        {
            _labelImage.color = _labelBaseColor;
        }
    }

    /// <summary>
    /// Handles interaction when anchor is selected
    /// </summary>
    public void OnSelect()
    {
        _isSelected = !_isSelected;
        if (_anchorMenu) _anchorMenu.SetActive(false);
        _labelImage.color = _isHovered
            ? _labelHighlightColor
            : _isSelected
                ? _labelSelectedColor
                : _labelBaseColor;
    }

    private void OnDestroy()
    {
        SetEmissionColor(Color.clear);
        AnchorUIManager.Instance?.UnregisterAnchor(this);
    }

    #endregion // Public Methods

    #region Private Methods

    private void SetEmissionColor(Color color)
    {
        if (_renderers == null) return;
        _emissionPropertyBlock ??= new MaterialPropertyBlock();
        foreach (var renderer in _renderers)
        {
            if (!renderer) continue;
            renderer.GetPropertyBlock(_emissionPropertyBlock);
            _emissionPropertyBlock.SetColor(EmissionColorProperty, color);
            renderer.SetPropertyBlock(_emissionPropertyBlock);
        }
    }

    private void BillboardPanel(Transform panel)
    {
        // The z axis of the panel faces away from the side that is rendered, therefore this code is actually looking away from the camera
        panel.LookAt(
            new Vector3(panel.position.x * 2 - _mainCamera.transform.position.x,
                panel.position.y * 2 - _mainCamera.transform.position.y,
                panel.position.z * 2 - _mainCamera.transform.position.z), Vector3.up);
    }

    #endregion // Private Methods
}
