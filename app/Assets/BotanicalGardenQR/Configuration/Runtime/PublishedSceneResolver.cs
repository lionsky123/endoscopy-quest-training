using System;
using System.Collections.Generic;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Narration.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.Video.Contracts;

namespace BotanicalGardenQR.Configuration.Runtime
{
    public interface IPhysicalAugmentationSceneAssociationSource
    {
        IReadOnlyList<PhysicalAugmentationPointId> GetPhysicalAugmentationPoints(SceneId sceneId);
    }

    public sealed class PublishedSceneResolver : ISceneFlowDescriptorSource, IVideoDefinitionSource,
        IPanoramaDefinitionSource, IModelDefinitionSource, INarrationDefinitionSource,
        IEffectDefinitionSource, IImageRingDefinitionSource, IKnowledgeMiniGameDefinitionSource,
        IPhysicalAugmentationSceneAssociationSource
    {
        readonly ContentSceneLibrary _library;
        public PublishedSceneResolver(ContentSceneLibrary library) => _library = library != null ? library : throw new System.ArgumentNullException(nameof(library));

        public bool TryGet(SceneId sceneId, out SceneFlowDescriptor descriptor)
        {
            if (_library.TryGet(sceneId, out var package))
            { descriptor = new SceneFlowDescriptor(package.SceneId, package.Title, package.Subtitle, package.Summary, package.AvailablePages); return true; }
            descriptor = null; return false;
        }

        public bool TryGetPresentation(SceneId sceneId, out PresentationSpec presentation)
        {
            if (_library.TryGet(sceneId, out var package))
            {
                presentation = package.Presentation;
                return presentation != null;
            }
            presentation = null;
            return false;
        }

        public bool IsPanoramaReadyForTeaching(SceneId sceneId)
            => _library.TryGet(sceneId, out var package) && package.PanoramaReadyForTeaching;

        public bool TryGetLearningImages(SceneId sceneId, out ImageRingDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateImageRing(out definition);
            definition = null;
            return false;
        }

        public IReadOnlyList<PhysicalAugmentationPointId> GetPhysicalAugmentationPoints(SceneId sceneId)
        {
            if (_library.TryGet(sceneId, out var package))
                return package.PhysicalAugmentationPointIds;
            return Array.Empty<PhysicalAugmentationPointId>();
        }

        bool IVideoDefinitionSource.TryGet(SceneId sceneId, out VideoDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateVideo(out definition);
            definition = null; return false;
        }
        bool IPanoramaDefinitionSource.TryGet(SceneId sceneId, out PanoramaDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreatePanorama(out definition);
            definition = null; return false;
        }
        bool IImageRingDefinitionSource.TryGet(SceneId sceneId, out ImageRingDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateImageRing(out definition);
            definition = null; return false;
        }
        bool IKnowledgeMiniGameDefinitionSource.TryGet(
            SceneId sceneId,
            out KnowledgeMiniGameDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package))
                return package.TryCreateKnowledgeMiniGame(out definition);
            definition = null;
            return false;
        }
        bool IModelDefinitionSource.TryGet(SceneId sceneId, out ModelDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateModel(out definition);
            definition = null; return false;
        }
        bool INarrationDefinitionSource.TryGet(SceneId sceneId, out NarrationDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateNarration(out definition);
            definition = null; return false;
        }
        bool IEffectDefinitionSource.TryGet(SceneId sceneId, out EffectDefinition definition)
        {
            if (_library.TryGet(sceneId, out var package)) return package.TryCreateEffect(out definition);
            definition = null; return false;
        }
    }
}
