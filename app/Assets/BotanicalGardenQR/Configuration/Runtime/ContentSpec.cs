using System;
using System.Collections.Generic;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Narration.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;
using UnityEngine.Video;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [System.Serializable]
    public sealed class ContentSpec
    {
        [SerializeField] string _title;
        [SerializeField] string _subtitle;
        [SerializeField, TextArea] string _summary;
        [SerializeReference] VideoContentSpec _video;
        [SerializeReference] PanoramaContentSpec _panorama;
        [SerializeReference] ImageRingContentSpec _imageRing;
        [SerializeReference] KnowledgeMiniGameContentSpec _knowledgeMiniGame;
        [SerializeReference] ModelContentSpec _model;
        [SerializeReference] NarrationContentSpec _narration;
        [SerializeReference] EffectContentSpec _effect;
        [SerializeField] string[] _physicalAugmentationPointIds = Array.Empty<string>();

        public string Title => _title ?? string.Empty;
        public string Subtitle => _subtitle ?? string.Empty;
        public string Summary => _summary ?? string.Empty;
        public VideoContentSpec Video => _video;
        public PanoramaContentSpec Panorama => _panorama;
        public ImageRingContentSpec ImageRing => _imageRing;
        public KnowledgeMiniGameContentSpec KnowledgeMiniGame => _knowledgeMiniGame;
        public ModelContentSpec Model => _model;
        public NarrationContentSpec Narration => _narration;
        public EffectContentSpec Effect => _effect;
        public IReadOnlyList<string> SerializedPhysicalAugmentationPointIds =>
            _physicalAugmentationPointIds ?? Array.Empty<string>();

        public PhysicalAugmentationPointId[] ParsePhysicalAugmentationPointIds()
        {
            var source = _physicalAugmentationPointIds ?? Array.Empty<string>();
            var pointIds = new PhysicalAugmentationPointId[source.Length];
            for (var index = 0; index < source.Length; index++)
                pointIds[index] = new PhysicalAugmentationPointId(source[index]);
            return pointIds;
        }

        internal ContentSpec DeepCopy()
            => new ContentSpec
            {
                _title = _title,
                _subtitle = _subtitle,
                _summary = _summary,
                _video = _video?.DeepCopy(),
                _panorama = _panorama?.DeepCopy(),
                _imageRing = _imageRing?.DeepCopy(),
                _knowledgeMiniGame = _knowledgeMiniGame?.DeepCopy(),
                _model = _model?.DeepCopy(),
                _narration = _narration?.DeepCopy(),
                _effect = _effect?.DeepCopy(),
                _physicalAugmentationPointIds = _physicalAugmentationPointIds == null
                    ? Array.Empty<string>()
                    : (string[])_physicalAugmentationPointIds.Clone()
            };
    }

    [System.Serializable]
    public sealed class VideoContentSpec
    {
        [SerializeField] VideoClip _clip;
        [SerializeField] string _streamingAssetsPath;
        [SerializeField] bool _loop;
        public VideoClip Clip => _clip;
        public string StreamingAssetsPath => _streamingAssetsPath ?? string.Empty;
        public bool Loop => _loop;
        internal VideoContentSpec DeepCopy() => (VideoContentSpec)MemberwiseClone();
    }

    [System.Serializable]
    public sealed class PanoramaContentSpec
    {
        [SerializeField] bool _readyForTeaching;
        public bool ReadyForTeaching => _readyForTeaching;
        [SerializeField] Texture _texture;
        [SerializeField] string _streamingAssetsPath;
        [SerializeField] float _initialYawDegrees;
        [SerializeField] PanoramaEnvironmentMomentProfile[] _environmentMoments =
            System.Array.Empty<PanoramaEnvironmentMomentProfile>();
        public Texture Texture => _texture;
        public string StreamingAssetsPath => _streamingAssetsPath ?? string.Empty;
        public float InitialYawDegrees => _initialYawDegrees;
        public System.Collections.Generic.IReadOnlyList<PanoramaEnvironmentMomentProfile> EnvironmentMoments
            => _environmentMoments ?? System.Array.Empty<PanoramaEnvironmentMomentProfile>();
        internal PanoramaContentSpec DeepCopy()
        {
            var copy = (PanoramaContentSpec)MemberwiseClone();
            copy._environmentMoments = _environmentMoments == null
                ? System.Array.Empty<PanoramaEnvironmentMomentProfile>()
                : (PanoramaEnvironmentMomentProfile[])_environmentMoments.Clone();
            return copy;
        }
    }

    [System.Serializable]
    public sealed class ImageRingContentSpec
    {
        [SerializeField] ImageRingItemContentSpec[] _items = System.Array.Empty<ImageRingItemContentSpec>();

        public System.Collections.Generic.IReadOnlyList<ImageRingItemContentSpec> Items
            => _items ?? System.Array.Empty<ImageRingItemContentSpec>();

        internal ImageRingContentSpec DeepCopy()
        {
            var copy = (ImageRingContentSpec)MemberwiseClone();
            if (_items == null)
            {
                copy._items = System.Array.Empty<ImageRingItemContentSpec>();
                return copy;
            }

            copy._items = new ImageRingItemContentSpec[_items.Length];
            for (var index = 0; index < _items.Length; index++)
                copy._items[index] = _items[index]?.DeepCopy();
            return copy;
        }
    }

    [System.Serializable]
    public sealed class ImageRingItemContentSpec
    {
        [SerializeField] Texture2D _image;
        [SerializeField] AudioClip _audio;
        [SerializeField] string _title;
        [SerializeField, TextArea] string _description;

        public Texture2D Image => _image;
        public AudioClip Audio => _audio;
        public string Title => _title ?? string.Empty;
        public string Description => _description ?? string.Empty;

        internal ImageRingItemContentSpec DeepCopy()
            => (ImageRingItemContentSpec)MemberwiseClone();

        internal ImageRingItemDefinition CreateDefinition()
            => new ImageRingItemDefinition(_image, _audio, Title, Description);
    }

    [System.Serializable]
    public sealed class KnowledgeMiniGameContentSpec
    {
        [SerializeField] KnowledgeMiniGameQuestionContentSpec[] _questions =
            System.Array.Empty<KnowledgeMiniGameQuestionContentSpec>();

        public System.Collections.Generic.IReadOnlyList<KnowledgeMiniGameQuestionContentSpec> Questions
            => _questions ?? System.Array.Empty<KnowledgeMiniGameQuestionContentSpec>();

        internal KnowledgeMiniGameContentSpec DeepCopy()
        {
            var copy = (KnowledgeMiniGameContentSpec)MemberwiseClone();
            if (_questions == null)
            {
                copy._questions = System.Array.Empty<KnowledgeMiniGameQuestionContentSpec>();
                return copy;
            }

            copy._questions = new KnowledgeMiniGameQuestionContentSpec[_questions.Length];
            for (var index = 0; index < _questions.Length; index++)
                copy._questions[index] = _questions[index]?.DeepCopy();
            return copy;
        }

        internal KnowledgeMiniGameDefinition CreateDefinition()
        {
            var questions = new KnowledgeMiniGameQuestionDefinition[Questions.Count];
            for (var index = 0; index < questions.Length; index++)
                questions[index] = Questions[index].CreateDefinition();
            return new KnowledgeMiniGameDefinition(questions);
        }
    }

    [System.Serializable]
    public sealed class KnowledgeMiniGameQuestionContentSpec
    {
        [SerializeField, TextArea] string _question;
        [SerializeField] KnowledgeMiniGameOptionContentSpec[] _options =
            System.Array.Empty<KnowledgeMiniGameOptionContentSpec>();
        [SerializeField] string _correctAnswerId;
        [SerializeField, TextArea] string _successExplanation;
        [SerializeField, TextArea] string _retryHint;

        public string Question => _question ?? string.Empty;
        public System.Collections.Generic.IReadOnlyList<KnowledgeMiniGameOptionContentSpec> Options
            => _options ?? System.Array.Empty<KnowledgeMiniGameOptionContentSpec>();
        public string CorrectAnswerId => _correctAnswerId ?? string.Empty;
        public string SuccessExplanation => _successExplanation ?? string.Empty;
        public string RetryHint => _retryHint ?? string.Empty;

        internal KnowledgeMiniGameQuestionContentSpec DeepCopy()
        {
            var copy = (KnowledgeMiniGameQuestionContentSpec)MemberwiseClone();
            if (_options == null)
            {
                copy._options = System.Array.Empty<KnowledgeMiniGameOptionContentSpec>();
                return copy;
            }

            copy._options = new KnowledgeMiniGameOptionContentSpec[_options.Length];
            for (var index = 0; index < _options.Length; index++)
                copy._options[index] = _options[index]?.DeepCopy();
            return copy;
        }

        internal KnowledgeMiniGameQuestionDefinition CreateDefinition()
        {
            var options = new KnowledgeMiniGameOptionDefinition[Options.Count];
            for (var index = 0; index < options.Length; index++)
                options[index] = Options[index].CreateDefinition();
            return new KnowledgeMiniGameQuestionDefinition(
                Question,
                options,
                CorrectAnswerId,
                SuccessExplanation,
                RetryHint);
        }
    }

    [System.Serializable]
    public sealed class KnowledgeMiniGameOptionContentSpec
    {
        [SerializeField] string _answerId;
        [SerializeField] string _text;

        public string AnswerId => _answerId ?? string.Empty;
        public string Text => _text ?? string.Empty;
        internal KnowledgeMiniGameOptionContentSpec DeepCopy()
            => (KnowledgeMiniGameOptionContentSpec)MemberwiseClone();
        internal KnowledgeMiniGameOptionDefinition CreateDefinition()
            => new KnowledgeMiniGameOptionDefinition(AnswerId, Text);
    }

    [System.Serializable]
    public sealed class ModelContentSpec
    {
        [SerializeField] ModelSourceKind _sourceKind = ModelSourceKind.Prefab;
        [SerializeField] GameObject _prefab;
        [SerializeField] UnityEngine.Object _glbAsset;
        [SerializeField] string _glbStreamingAssetsPath;
        [SerializeField] Vector3 _localPosition;
        [SerializeField] Vector3 _localEulerAngles;
        [SerializeField] Vector3 _localScale = Vector3.one;
        [SerializeField] Material _materialOverride;
        [SerializeField] bool _allowRotation = true;
        [SerializeField, Min(0f)] float _rotationDegreesPerSecond = 24f;
        [SerializeField, Min(0f)] float _bobAmplitude = 0.01f;
        [SerializeField, Min(0f)] float _bobFrequency = 0.5f;
        [SerializeReference] ModelAnimationContentSpec _animation;
        public ModelSourceKind SourceKind => _sourceKind;
        public GameObject Prefab => _prefab;
        public UnityEngine.Object GlbAsset => _glbAsset;
        public string GlbStreamingAssetsPath => _glbStreamingAssetsPath ?? string.Empty;
        public Vector3 LocalPosition => _localPosition;
        public Vector3 LocalEulerAngles => _localEulerAngles;
        public Vector3 LocalScale => _localScale;
        public Material MaterialOverride => _materialOverride;
        public bool AllowRotation => _allowRotation;
        public float RotationDegreesPerSecond => _rotationDegreesPerSecond;
        public float BobAmplitude => _bobAmplitude;
        public float BobFrequency => _bobFrequency;
        public ModelAnimationContentSpec Animation => _animation;
        internal ModelContentSpec DeepCopy()
        {
            var copy = (ModelContentSpec)MemberwiseClone();
            copy._animation = _animation?.DeepCopy();
            return copy;
        }
    }

    [System.Serializable]
    public sealed class ModelAnimationContentSpec
    {
        [SerializeField] ModelAnimationDriver _driver;
        [SerializeField] RuntimeAnimatorController _controller;
        [SerializeField] AnimationClip _clip;
        [SerializeField] string _initialState;
        [SerializeField] ModelAnimationStartPolicy _startPolicy;
        public ModelAnimationDriver Driver => _driver;
        public RuntimeAnimatorController Controller => _controller;
        public AnimationClip Clip => _clip;
        public string InitialState => _initialState ?? string.Empty;
        public ModelAnimationStartPolicy StartPolicy => _startPolicy;
        internal ModelAnimationContentSpec DeepCopy() => (ModelAnimationContentSpec)MemberwiseClone();
    }

    [System.Serializable]
    public sealed class NarrationContentSpec
    {
        [SerializeField] AudioClip _clip;
        [SerializeField] NarrationStartPolicy _startPolicy = NarrationStartPolicy.OnVisitorCommand;
        public AudioClip Clip => _clip;
        public NarrationStartPolicy StartPolicy => _startPolicy;
        internal NarrationContentSpec DeepCopy() => (NarrationContentSpec)MemberwiseClone();
    }

    [System.Serializable]
    public sealed class EffectContentSpec
    {
        [SerializeField] GameObject _prefab;
        [SerializeField] EffectTriggerPolicy _triggerPolicy;
        [SerializeField, Min(0.01f)] float _scale = 1f;
        public GameObject Prefab => _prefab;
        public EffectTriggerPolicy TriggerPolicy => _triggerPolicy;
        public float Scale => _scale;
        internal EffectContentSpec DeepCopy() => (EffectContentSpec)MemberwiseClone();
    }
}
