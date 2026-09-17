using System;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Video.Frontend
{
    // XREAL's embedded video surface is a RawImage inside VideoStage. The
    // target keeps that UI detail in Frontend while the Backend only receives
    // a typed texture target through Video.Contracts.
    public sealed class VideoSurfaceTarget : MonoBehaviour, IVideoSurfaceTarget
    {
        RawImage _videoImage;
        AspectRatioFitter _aspectRatioFitter;
        RectTransform _contentRoot;
        Vector2 _availableSize;
        bool _disposed;

        public Transform ContentRoot => _contentRoot;
        public Vector2 AvailableSize => _availableSize;

        public static VideoSurfaceTarget Create(Transform stageRoot, Vector2 availableSize)
        {
            if (stageRoot == null) throw new ArgumentNullException(nameof(stageRoot));
            if (availableSize.x <= 0f || availableSize.y <= 0f ||
                float.IsNaN(availableSize.x) || float.IsNaN(availableSize.y) ||
                float.IsInfinity(availableSize.x) || float.IsInfinity(availableSize.y))
                throw new ArgumentOutOfRangeException(nameof(availableSize));

            var surfaceObject = new GameObject("VideoSurface", typeof(RectTransform), typeof(RectMask2D));
            surfaceObject.transform.SetParent(stageRoot, false);
            var surfaceRoot = surfaceObject.GetComponent<RectTransform>();
            Stretch(surfaceRoot);

            var imageObject = new GameObject("VideoImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(surfaceRoot, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            Stretch(imageRect);
            var image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            var fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            imageObject.SetActive(false);

            var target = surfaceObject.AddComponent<VideoSurfaceTarget>();
            target._videoImage = image;
            target._aspectRatioFitter = fitter;
            target._contentRoot = surfaceRoot;
            target._availableSize = availableSize;
            return target;
        }

        public void SetTexture(Texture texture, float aspectRatio)
        {
            if (_disposed) return;
            _videoImage.texture = texture;
            if (aspectRatio > 0f && !float.IsNaN(aspectRatio) && !float.IsInfinity(aspectRatio))
                _aspectRatioFitter.aspectRatio = aspectRatio;
            _videoImage.gameObject.SetActive(texture != null);
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            if (_videoImage != null)
                _videoImage.gameObject.SetActive(visible && _videoImage.texture != null);
        }

        public void Clear()
        {
            if (_disposed) return;
            if (_videoImage != null)
            {
                _videoImage.texture = null;
                _videoImage.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (gameObject != null)
                Destroy(gameObject);
        }

        static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
