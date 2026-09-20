using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Bootstrap
{
    // A recovery notice is the only head-relative surface. Lesson surfaces and
    // the room never move. No automatic recenter, teleport, or guessed alignment.
    internal sealed class VirtualRoomTrackingGuard : IDisposable
    {
        readonly VirtualRoomTrackingOrigin _origin;
        readonly Transform _viewer, _interaction;
        readonly TMP_FontAsset _font;
        GameObject _notice;
        TMP_Text _text;
        bool _disabledInteraction, _disposed;

        public VirtualRoomTrackingGuard(VirtualRoomTrackingOrigin origin, Transform viewer,
            Transform interaction, TMP_FontAsset font)
        {
            _origin = origin; _viewer = viewer; _interaction = interaction; _font = font;
            if (interaction && (viewer == interaction || viewer.IsChildOf(interaction)))
                throw new ArgumentException("Tracking recovery must not disable the camera rig.");
            _origin.StateChanged += Refresh;
            Refresh();
        }

        void Refresh()
        {
            if (_disposed) return;
            if (!_origin.CanInteract && _interaction && _interaction.gameObject.activeSelf)
            {
                _disabledInteraction = true;
                _interaction.gameObject.SetActive(false);
            }
            else if (_origin.CanInteract && _disabledInteraction)
            {
                _disabledInteraction = false;
                if (_interaction) _interaction.gameObject.SetActive(true);
            }
            // A valid pending event needs no flashing notice. Lost tracking does.
            var visible = _origin.RecoveryRequired || !_origin.TrackingAvailable;
            if (visible)
            {
                EnsureNotice();
                _text.text = _origin.RecoveryRequired
                    ? "定位已发生变化\n\n无法可靠恢复原位置。\n请先停止走动，退出并重新打开应用。"
                    : "正在恢复定位\n\n请先停止走动，等待头显恢复追踪。";
            }
            if (_notice) _notice.SetActive(visible);
        }

        void EnsureNotice()
        {
            if (_notice) return;
            _notice = new GameObject("VRTrackingRecoveryNotice", typeof(RectTransform), typeof(Canvas), typeof(Image));
            _notice.transform.SetParent(_viewer, false);
            _notice.transform.localPosition = new Vector3(0, -.04f, .65f);
            _notice.transform.localScale = Vector3.one * .00065f;
            ((RectTransform)_notice.transform).sizeDelta = new Vector2(820, 300);
            var canvas = _notice.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true; canvas.sortingOrder = 32000;
            canvas.worldCamera = _viewer.GetComponent<Camera>();
            var background = _notice.GetComponent<Image>();
            background.color = new Color(.025f, .04f, .06f, 1); background.raycastTarget = false;
            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_notice.transform, false);
            var rect = (RectTransform)label.transform;
            rect.sizeDelta = new Vector2(770, 260);
            _text = label.GetComponent<TextMeshProUGUI>();
            _text.font = _font; _text.fontSize = 32; _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.Center; _text.raycastTarget = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _origin.StateChanged -= Refresh;
            if (_disabledInteraction && _interaction) _interaction.gameObject.SetActive(true);
            if (_notice)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_notice);
                else UnityEngine.Object.DestroyImmediate(_notice);
            }
        }
    }
}
