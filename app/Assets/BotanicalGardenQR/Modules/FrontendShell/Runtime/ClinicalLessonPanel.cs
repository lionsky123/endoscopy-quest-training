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
        bool _disposed;

        public ClinicalLessonPanel(Transform parent, TMP_FontAsset font, IFrontendGazeSurfaceRegistry surfaces,
            Action panorama)
        {
            _root = new GameObject("ClinicalLessonPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            var root = (RectTransform)_root.transform;
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(700, 300);
            root.localScale = Vector3.one * .6f;
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;
            Frame(root);
            var tag = Label(root, font, "LessonNumber", 0, 111, 620, 30, 20);
            tag.text = "第 1 关  /  房间观察";
            tag.color = Accent;
            var title = Label(root, font, "LessonTitle", 0, 58, 620, 50, 34);
            title.text = "跟着图谱，观察清洗消毒室";
            _hint = Label(root, font, "ObservationTask", 0, 0, 620, 54, 23);
            _hint.color = Muted;
            _start = ClinicalPanelStyle.Button(root, font, "EnterPanorama", "开始观察", 0, -94, 340, 64, panorama, true);
            _registration = surfaces.RegisterGazeSurface(root, 220, "ClinicalLessonPanel");
            _root.SetActive(false);
        }
        public void Present(ExperienceFlowState state, ImageRingDefinition images, bool panoramaReady)
        {
            _start.interactable = panoramaReady;
            _hint.text = panoramaReady ? "先转身看看房间，图谱会依次带你观察三个要点。" : "房间全景尚未准备好。";
        }
        public void SetVisible(bool visible) { if (!_disposed) _root.SetActive(visible); }
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
