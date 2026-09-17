using System;
using System.Collections;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.ImageRing.Frontend
{
    public sealed class ImageRingFrontend : MonoBehaviour, IImageRingRuntime
    {
        const float AudioFadeOutSeconds = 0.15f;

        Transform _viewer;
        IFrontendGazeSurfaceRegistry _gazeSurfaces;
        TMP_FontAsset _sharedFont;
        ImageRingSpatialWorld _world;
        AudioSource _audioSource;
        Coroutine _audioFade;
        bool _initialized;
        bool _disposed;

        public static IImageRingRuntime Create(
            Transform runtimeParent,
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont)
        {
            if (runtimeParent == null) throw new ArgumentNullException(nameof(runtimeParent));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));

            var root = new GameObject("ImageRingModuleRuntime");
            root.transform.SetParent(runtimeParent, false);
            try
            {
                var frontend = root.AddComponent<ImageRingFrontend>();
                frontend.Initialize(viewer, gazeSurfaces, sharedFont);
                return frontend;
            }
            catch
            {
                DestroyRuntimeObject(root);
                throw;
            }
        }

        public void Open(
            SessionToken session,
            ImageRingDefinition definition,
            Action<ImageRingItemPlaybackIntent> dispatchPlayback,
            Action requestClose)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageRingFrontend));
            if (!_initialized) throw new InvalidOperationException("ImageRing frontend is not initialized.");
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (dispatchPlayback == null) throw new ArgumentNullException(nameof(dispatchPlayback));
            if (requestClose == null) throw new ArgumentNullException(nameof(requestClose));
            if (_world != null) throw new InvalidOperationException("ImageRing is already open.");

            try
            {
                StopAudio(true);
                _world = new ImageRingSpatialWorld(
                    transform,
                    _viewer,
                    _gazeSurfaces,
                    _sharedFont,
                    definition,
                    dispatchPlayback,
                    requestClose);
            }
            catch
            {
                _world?.Dispose();
                _world = null;
                throw;
            }
        }

        public void Close()
        {
            var world = _world;
            _world = null;
            try { StopAudio(true); }
            finally { world?.Dispose(); }
        }

        public void PlayAudio(AudioClip clip)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageRingFrontend));
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (_audioSource == null) throw new InvalidOperationException("ImageRing audio output is unavailable.");

            CancelAudioFade();
            _audioSource.Stop();
            _audioSource.clip = clip;
            _audioSource.timeSamples = 0;
            _audioSource.volume = 1f;
            _audioSource.Play();
        }

        public void StopAudio(bool immediate)
        {
            if (_audioSource == null) return;
            CancelAudioFade();
            if (immediate || !_audioSource.isPlaying)
            {
                CompleteAudioStop();
                return;
            }
            _audioFade = StartCoroutine(FadeOutAudio());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Close(); }
            finally { DestroyRuntimeObject(gameObject); }
        }

        void Initialize(
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont)
        {
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _gazeSurfaces = gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces));
            _sharedFont = sharedFont != null
                ? sharedFont
                : throw new ArgumentNullException(nameof(sharedFont));
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 1f;
            _initialized = true;
        }

        IEnumerator FadeOutAudio()
        {
            var elapsed = 0f;
            var initialVolume = _audioSource != null ? _audioSource.volume : 1f;
            while (_audioSource != null && _audioSource.isPlaying && elapsed < AudioFadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _audioSource.volume = Mathf.Lerp(
                    initialVolume,
                    0f,
                    Mathf.Clamp01(elapsed / AudioFadeOutSeconds));
                yield return null;
            }

            CompleteAudioStop();
            _audioFade = null;
        }

        void CancelAudioFade()
        {
            if (_audioFade == null) return;
            StopCoroutine(_audioFade);
            _audioFade = null;
        }

        void CompleteAudioStop()
        {
            if (_audioSource == null) return;
            _audioSource.Stop();
            _audioSource.clip = null;
            _audioSource.volume = 1f;
        }

        void OnDestroy()
        {
            if (_disposed) return;
            _disposed = true;
            try { Close(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
