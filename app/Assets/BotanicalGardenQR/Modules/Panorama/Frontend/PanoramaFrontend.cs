using System;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Frontend
{
    // Panorama owns the spatial presentation. FrontendShell still owns the
    // navigation intent supplied through the exit callback.
    public sealed class PanoramaFrontend : MonoBehaviour, IPanoramaStateSink
    {
        IFrontendGazeSurfaceRegistry _gazeSurfaces;
        Transform _viewer;
        Transform _runtimeRoot;
        TMP_FontAsset _sharedFont;
        Action _requestExit;
        Action _requestClinicalCompletion;
        Action _requestImageRing;
        IReadOnlyList<PanoramaEnvironmentMomentDefinition> _environmentMoments =
            Array.Empty<PanoramaEnvironmentMomentDefinition>();
        bool _hasImageRing;
        bool _visible;
        bool _tutorialHintVisible;
        string _tutorialHintCopy = string.Empty;
        PanoramaSpatialWorld _spatialWorld;
        SessionToken _session;
        bool _bound;
        bool _clinicalLearning;
        float _panoramaYaw;
        Texture _clinicalTexture;
        IReadOnlyList<Texture> _teachingComparisons;

        public event Action<bool> SurfaceVisibilityChanged;
        public event Action ExitSelected;

        public void Bind(
            SessionToken session,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            Transform viewer,
            Transform runtimeRoot,
            TMP_FontAsset sharedFont,
            Action requestExit,
            IReadOnlyList<PanoramaEnvironmentMomentDefinition> environmentMoments,
            bool hasImageRing,
            Action requestImageRing,
            bool clinicalLearning = false,
            Texture clinicalTexture = null,
            IReadOnlyList<Texture> teachingComparisons = null,
            Action requestClinicalCompletion = null,
            float panoramaYaw = 0)
        {
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            _gazeSurfaces = gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces));
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _runtimeRoot = runtimeRoot != null ? runtimeRoot : throw new ArgumentNullException(nameof(runtimeRoot));
            _sharedFont = sharedFont != null
                ? sharedFont
                : throw new ArgumentNullException(nameof(sharedFont));
            _requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));
            _environmentMoments = environmentMoments ?? throw new ArgumentNullException(nameof(environmentMoments));
            _hasImageRing = hasImageRing;
            _requestImageRing = hasImageRing
                ? requestImageRing ?? throw new ArgumentNullException(nameof(requestImageRing))
                : null;
            _session = session;
            _clinicalLearning = clinicalLearning;
            _panoramaYaw = panoramaYaw;
            _clinicalTexture = clinicalTexture;
            _teachingComparisons = teachingComparisons;
            _requestClinicalCompletion = requestClinicalCompletion;
            _bound = true;
            SetVisible(false);
        }

        public void Unbind()
        {
            SetVisible(false);
            _spatialWorld?.Dispose();
            _spatialWorld = null;
            _bound = false;
            _session = default;
            _gazeSurfaces = null;
            _viewer = null;
            _runtimeRoot = null;
            _sharedFont = null;
            _requestExit = null;
            _requestClinicalCompletion = null;
            _requestImageRing = null;
            _clinicalTexture = null;
            _teachingComparisons = null;
            _environmentMoments = Array.Empty<PanoramaEnvironmentMomentDefinition>();
            _hasImageRing = false;
            _tutorialHintVisible = false;
            _tutorialHintCopy = string.Empty;
        }

        public void SetVisible(bool visible)
        {
            if (!_bound && visible) return;
            if (!visible)
            {
                _spatialWorld?.SetVisible(false);
                SetSurfaceVisible(false);
                return;
            }

            EnsureSpatialWorld();
            _spatialWorld.SetVisible(true);
            _spatialWorld.SetTutorialHint(_tutorialHintCopy, _tutorialHintVisible);
            SetSurfaceVisible(true);
        }

        public void SetControlsVisible(bool visible)
        {
            if (!_bound) return;
            _spatialWorld?.SetControlsVisible(visible);
        }

        public void SetTutorialHint(string copy, bool visible)
        {
            if (visible && string.IsNullOrWhiteSpace(copy))
                throw new ArgumentException("Visible Panorama tutorial copy is required.", nameof(copy));
            if (!string.IsNullOrWhiteSpace(copy)) _tutorialHintCopy = copy.Trim();
            _tutorialHintVisible = visible;
            _spatialWorld?.SetTutorialHint(_tutorialHintCopy, visible);
        }

        public void Publish(PanoramaState state)
        {
            if (!_bound || state.Session != _session) return;
            // State remains available to the binding for diagnostics while the
            // immersive shell owns the single exit affordance.
        }

        void EnsureSpatialWorld()
        {
            if (_spatialWorld != null) return;
            _spatialWorld = new PanoramaSpatialWorld(
                _runtimeRoot,
                _viewer,
                _gazeSurfaces,
                _sharedFont,
                HandleExitSelected,
                _hasImageRing,
                _requestImageRing,
                _environmentMoments,
                clinicalLearning: _clinicalLearning,
                clinicalTexture: _clinicalTexture,
                teachingComparisons: _teachingComparisons,
                requestClinicalCompletion: _requestClinicalCompletion,
                panoramaYaw: _panoramaYaw);
        }

        void HandleExitSelected()
        {
            if (!_bound || !_visible) return;
            ExitSelected?.Invoke();
            _requestExit?.Invoke();
        }

        void SetSurfaceVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            SurfaceVisibilityChanged?.Invoke(visible);
        }

        void OnDestroy()
        {
            Unbind();
            SurfaceVisibilityChanged = null;
            ExitSelected = null;
        }
    }
}
