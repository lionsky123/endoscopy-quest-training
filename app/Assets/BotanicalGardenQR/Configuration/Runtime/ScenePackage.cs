using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Narration.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [Serializable]
    public sealed class ScenePackage
    {
        [SerializeField] string _sceneId;
        [SerializeField] ContentSpec _content;
        [SerializeField] PresentationSpec _presentation;
        [SerializeField] FeaturePageId[] _availablePages = Array.Empty<FeaturePageId>();
        [NonSerialized] ReadOnlyCollection<FeaturePageId> _readOnlyPages;
        [NonSerialized] ReadOnlyCollection<PhysicalAugmentationPointId> _physicalAugmentationPointIds;

        public ScenePackage(string sceneId, ContentSpec content, PresentationSpec presentation, IReadOnlyList<FeaturePageId> availablePages)
        {
            _ = new SceneId(sceneId);
            _sceneId = sceneId;
            _content = (content ?? throw new ArgumentNullException(nameof(content))).DeepCopy();
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            if (availablePages == null) throw new ArgumentNullException(nameof(availablePages));
            _availablePages = new FeaturePageId[availablePages.Count];
            for (var index = 0; index < _availablePages.Length; index++) _availablePages[index] = availablePages[index];
        }

        public SceneId SceneId => new SceneId(_sceneId);
        public string Title => _content.Title;
        public string Subtitle => _content.Subtitle;
        public string Summary => _content.Summary;
        public bool PanoramaReadyForTeaching => _content.Panorama?.ReadyForTeaching == true;
        public PresentationSpec Presentation => _presentation;
        public IReadOnlyList<FeaturePageId> AvailablePages => _readOnlyPages ??= Array.AsReadOnly(_availablePages);

        public IReadOnlyList<PhysicalAugmentationPointId> PhysicalAugmentationPointIds =>
            _physicalAugmentationPointIds ??=
                Array.AsReadOnly(_content.ParsePhysicalAugmentationPointIds());

        internal bool TryCreateVideo(out VideoDefinition definition)
        {
            var spec = _content.Video;
            if (spec == null) { definition = null; return false; }
            var source = spec.Clip != null ? VideoSource.FromClip(spec.Clip) : VideoSource.FromStreamingAssetsPath(spec.StreamingAssetsPath);
            definition = new VideoDefinition(source, spec.Loop); return true;
        }

        internal bool TryCreatePanorama(out PanoramaDefinition definition)
        {
            var spec = _content.Panorama;
            if (spec == null) { definition = null; return false; }
            var source = spec.Texture != null ? PanoramaSource.FromTexture(spec.Texture) : PanoramaSource.FromStreamingAssetsPath(spec.StreamingAssetsPath);
            var environmentMoments = new PanoramaEnvironmentMomentDefinition[spec.EnvironmentMoments.Count];
            for (var index = 0; index < environmentMoments.Length; index++)
                environmentMoments[index] = spec.EnvironmentMoments[index].CreateDefinition();
            bool clinical = _sceneId == "giant_saguaro" && spec.ReadyForTeaching;
            var comparisons = new Texture[clinical ? _content.ImageRing?.Items.Count ?? 0 : 0];
            for (int i = 0; i < comparisons.Length; i++) comparisons[i] = _content.ImageRing.Items[i].Image;
            definition = new PanoramaDefinition(source, spec.InitialYawDegrees, environmentMoments, clinical, comparisons); return true;
        }

        internal bool TryCreateImageRing(out ImageRingDefinition definition)
        {
            var spec = _content.ImageRing;
            if (spec == null) { definition = null; return false; }
            var items = new ImageRingItemDefinition[spec.Items.Count];
            for (var index = 0; index < items.Length; index++)
                items[index] = spec.Items[index].CreateDefinition();
            definition = new ImageRingDefinition(items);
            return true;
        }

        internal bool TryCreateKnowledgeMiniGame(out KnowledgeMiniGameDefinition definition)
        {
            var spec = _content.KnowledgeMiniGame;
            if (spec == null) { definition = null; return false; }
            definition = spec.CreateDefinition();
            return true;
        }

        internal bool TryCreateModel(out ModelDefinition definition)
        {
            var spec = _content.Model;
            if (spec == null) { definition = null; return false; }
            ModelSource source;
            if (spec.SourceKind == ModelSourceKind.Prefab) source = ModelSource.FromPrefab(spec.Prefab);
            else if (spec.GlbAsset != null) source = ModelSource.FromGlbAsset(spec.GlbAsset);
            else source = ModelSource.FromGlbStreamingAssetsPath(spec.GlbStreamingAssetsPath);
            ModelAnimationSpec animation = null;
            if (spec.Animation != null)
                animation = spec.Animation.Driver == ModelAnimationDriver.AnimatorController
                    ? ModelAnimationSpec.FromController(spec.Animation.Controller, spec.Animation.InitialState, spec.Animation.StartPolicy)
                    : ModelAnimationSpec.FromClip(spec.Animation.Clip, spec.Animation.StartPolicy);
            var presentation = new ModelPresentationSpec(
                spec.LocalPosition,
                spec.LocalEulerAngles,
                spec.LocalScale,
                spec.MaterialOverride,
                spec.AllowRotation,
                spec.RotationDegreesPerSecond,
                spec.BobAmplitude,
                spec.BobFrequency);
            definition = new ModelDefinition(source, presentation, animation); return true;
        }

        internal bool TryCreateNarration(out NarrationDefinition definition)
        {
            var spec = _content.Narration;
            if (spec == null) { definition = null; return false; }
            definition = new NarrationDefinition(spec.Clip, spec.StartPolicy); return true;
        }

        internal bool TryCreateEffect(out EffectDefinition definition)
        {
            var spec = _content.Effect;
            if (spec == null) { definition = null; return false; }
            definition = new EffectDefinition(spec.Prefab, spec.TriggerPolicy, spec.Scale); return true;
        }
    }
}
