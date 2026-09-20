using System;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // A single entry into the guided sequence; no media menu or quiz shortcut.
    public sealed class ClinicalLessonPanel : IDisposable
    {
        readonly GameObject _root;
        readonly Button _start;
        readonly TMP_Text _hint;
        readonly IFrontendGazeSurfaceRegistration _registration;
        readonly ClinicalWorldSurface _surface;
        bool _placed;
        bool _disposed;
        bool _ready;

        public ClinicalLessonPanel(Transform parent, TMP_FontAsset font, IFrontendGazeSurfaceRegistry surfaces,
            Action panorama)
        {
            _root = new GameObject("ClinicalLessonPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            _surface = _root.AddComponent<ClinicalWorldSurface>();
            var root = (RectTransform)_root.transform;
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(700, 300);
            root.localScale = Vector3.one * .6f;
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;
            Fill(Rect(root, "HintContrast", 0, 50, 760, 160), Surface);
            _hint = Label(root, font, "ObservationTask", 0, 50, 730, 150, 27);
            _hint.alignment = TextAlignmentOptions.Center;
            _start = ClinicalPanelStyle.Button(root, font, "StartObservation", "先学习操作", 0, -80, 420, 70,
                () => { if (!_disposed && _ready && _root.activeInHierarchy) panorama?.Invoke(); }, true);
            ClinicalNearTouch.Bind(root, () => !_disposed && _ready);
            _registration = surfaces.RegisterGazeSurface(root, 220, "ClinicalLessonPanel");
            _root.SetActive(false);
        }
        public void Present(ExperienceFlowState state, ImageRingDefinition images, bool panoramaReady)
        {
            _ready = panoramaReady; _start.interactable = panoramaReady;
            _hint.text = panoramaReady ? "第一站 · 先练习，再正式观察\n先学会握住手柄、举起镜片和对准光圈\n教学结束后，再开始三处观察；全程无需放下" : "房间全景尚未准备好。";
        }
        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            _root.SetActive(visible);
            if (!visible) { _placed = false; return; }
            var camera = _root.GetComponent<Canvas>().worldCamera;
            if (!camera) camera = Camera.main;
            if (!_placed && camera)
            {
                ClinicalWorldSurface.PlaceForHands(_root.transform, camera.transform, .00065f); _surface.Pin();
                _placed = true;
            }
            _surface.Restore();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registration.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
