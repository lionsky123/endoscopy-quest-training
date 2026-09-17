using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Model.Frontend;
using BotanicalGardenQR.Narration.Frontend;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using BotanicalGardenQR.Video.Contracts;
using BotanicalGardenQR.Video.Frontend;
using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    [Serializable]
    public sealed class FeaturePageBindings
    {
        [SerializeField] VideoFrontend _video;
        [SerializeField] PanoramaFrontend _panorama;
        [SerializeField] ModelFrontend _model;
        [SerializeField] NarrationFrontend _narration;

        public NarrationFrontend NarrationFrontend => _narration;
        internal PanoramaFrontend PanoramaFrontend => _panorama;

        internal IFeaturePageLifecycle[] Create(
            IGlobalFrontendShell shell,
            PublishedSceneResolver definitions,
            IVideoController video,
            IPanoramaController panorama,
            IModelController model,
            PanoramaImageRingBinding imageRing,
            Transform panoramaRuntimeRoot,
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont)
        {
            Validate();
            if (imageRing == null) throw new ArgumentNullException(nameof(imageRing));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));
            return new IFeaturePageLifecycle[]
            {
                new VideoPageLifecycleBinding(shell, definitions, video, _video),
                new PanoramaPageLifecycleBinding(
                    shell,
                    definitions,
                    panorama,
                    _panorama,
                    panoramaRuntimeRoot,
                    viewer,
                    gazeSurfaces,
                    sharedFont,
                    new PanoramaAuxiliaryExperienceBinding(
                        imageRing.HasDefinition,
                        imageRing.Open,
                        imageRing.Close)),
                new ModelPageLifecycleBinding(shell, definitions, model, _model)
            };
        }

        internal void Validate()
        {
            if (_video == null || _panorama == null || _model == null || _narration == null)
                throw new InvalidOperationException("VisitorInstaller requires Video, Panorama, Model, and Narration Dock frontend references.");
        }
    }
}
