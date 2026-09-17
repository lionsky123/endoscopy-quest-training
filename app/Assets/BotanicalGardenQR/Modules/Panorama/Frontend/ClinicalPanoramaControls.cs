using System;
using System.Collections.Generic;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Panorama.Frontend
{
    // One spatial card at a time. Readiness is an explicit near touch, never a timer or gaze dwell.
    internal sealed class ClinicalPanoramaControls : IDisposable
    {
        readonly GameObject _root;
        readonly RectTransform _card;
        readonly CanvasGroup _cardGroup;
        readonly RawImage _image;
        readonly TMP_Text _progress, _copy, _nextLabel;
        readonly Button _next;
        readonly Texture _panorama;
        readonly IReadOnlyList<Texture> _comparisons;
        readonly Action _complete;
        readonly IFrontendGazeSurfaceRegistration _registration;
        readonly Rect[] _crops = {
            new Rect(.225f, .25f, .135f, .47f),
            new Rect(.423f, .30f, .205f, .33f),
            new Rect(.758f, .23f, .242f, .44f)
        };
        readonly string[] _topics = { "隔断与门", "设备分设", "单向流程" };
        readonly string[] _detailCopy = {
            "先看门与相邻墙体，留意隔断边缘和门的闭合状态。",
            "观察两组工位，清洗槽和清洗消毒机都要分别配置。",
            "观察四类槽位与干燥工位的衔接，顺着由污到洁的方向看。"
        };
        readonly string[] _comparisonCopy = {
            "左侧门关闭，右侧门敞开。实体隔断完整和门关闭都需要核对。",
            "左侧分别配置，右侧共用。两类内镜的槽与机器不能只靠标签区分。",
            "左侧单向衔接，右侧发生回流。清洗 → 漂洗 → 消毒 → 终末漂洗 → 干燥。"
        };
        int _step;
        int _pending;
        float _transition = -1;
        Vector3 _settledPosition;
        bool _swapped, _completed, _disposed;
        const float TransitionSeconds = .42f;

        public ClinicalPanoramaControls(Transform parent, TMP_FontAsset font,
            IFrontendGazeSurfaceRegistry surfaces, Action complete, Texture panorama, IReadOnlyList<Texture> comparisons)
        {
            _complete = complete;
            _panorama = panorama;
            _comparisons = comparisons;
            _root = new GameObject("ClinicalPanoramaControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            var root = (RectTransform)_root.transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0, -.07f, .60f);
            root.localScale = Vector3.one * .00065f;
            root.sizeDelta = new Vector2(880, 690);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;

            _card = Rect(root, "SequentialCard", 0, 95, 840, 466);
            Frame(_card);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            _image = Rect(_card, "CurrentTeachingImage", 0, 0, 808, 434).gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            var caption = Rect(root, "CurrentCaption", 0, -199, 840, 132);
            Frame(caption);
            _progress = Label(caption, font, "SequenceProgress", 0, 38, 780, 28, 20);
            _progress.color = Accent;
            _copy = Label(caption, font, "CurrentInstruction", 0, -14, 780, 68, 24);
            _next = ClinicalPanelStyle.Button(root, font, "ContinueSequence", "看好了 · 继续", 0, -313, 350, 66, Advance, true);
            _nextLabel = _next.GetComponentInChildren<TMP_Text>();
            _registration = surfaces.RegisterGazeSurface(root, 260, "ClinicalPanoramaControls");
            Reset();
        }
        public void Reset()
        {
            _completed = false;
            _transition = -1;
            _next.interactable = true;
            Show(0);
        }
        void Advance()
        {
            if (_disposed || !_root.activeInHierarchy || !_next.interactable || _transition >= 0 || _completed) return;
            if (_step == 6)
            {
                _completed = true;
                _next.interactable = false;
                _complete?.Invoke();
                return;
            }
            _pending = _step + 1;
            if (!Application.isPlaying) { Show(_pending); return; }
            _transition = 0;
            _swapped = false;
            // Keep the same poke surface alive until the finger releases. Advance's
            // transition guard ignores additional taps without rearming a held finger.
        }
        public void Tick(float deltaTime)
        {
            if (_disposed || _transition < 0) return;
            _transition += deltaTime;
            float t = Mathf.Clamp01(_transition / TransitionSeconds);
            if (t >= .5f && !_swapped) { Show(_pending); _swapped = true; }
            float opacity = t < .5f ? 1 - t * 2 : (t - .5f) * 2;
            _cardGroup.alpha = Mathf.SmoothStep(0, 1, opacity);
            _card.localPosition = _settledPosition + Vector3.right * (t < .5f ? -20 * t : 20 * (1 - t));
            if (t < 1) return;
            _transition = -1;
            _cardGroup.alpha = 1;
            _card.localPosition = _settledPosition;
            _next.interactable = true;
        }
        void Show(int step)
        {
            _step = step;
            _cardGroup.alpha = 1;
            _card.gameObject.SetActive(step > 0);
            if (step == 0)
            {
                _progress.text = "先观察整个房间";
                _copy.text = "转身看看房间、门和工位。看好后，图谱会从隔断与门开始。";
                _nextLabel.text = "看好了 · 开始图谱";
                return;
            }
            int topic = (step - 1) / 2;
            bool comparison = step % 2 == 0;
            _image.texture = comparison ? _comparisons[topic] : _panorama;
            _image.uvRect = comparison ? new Rect(0, 0, 1, 1) : _crops[topic];
            float aspect = _image.texture.width * _image.uvRect.width / (_image.texture.height * _image.uvRect.height);
            var size = new Vector2(Mathf.Min(808, 434 * aspect), Mathf.Min(434, 808 / aspect));
            _image.rectTransform.sizeDelta = size;
            _card.sizeDelta = size + Vector2.one * 32;
            ((RectTransform)_card.Find("FineBorder")).sizeDelta = _card.sizeDelta;
            // Small arc in arm's reach; only the current card exists visually, so no target hunting.
            float yaw = (topic - 1) * 10f;
            _card.localRotation = Quaternion.Euler(0, yaw, 0);
            _settledPosition = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * 700, 95, -20);
            _card.localPosition = _settledPosition;
            _progress.text = $"{step:00} / 06   ·   {_topics[topic]}   ·   {(comparison ? "正误对照" : "现场特写")}";
            _copy.text = comparison ? _comparisonCopy[topic] : _detailCopy[topic];
            _nextLabel.text = step == 6 ? "看完了 · 开始答题" : comparison ? "继续 · 下一个观察点" : "继续 · 看正误对照";
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
