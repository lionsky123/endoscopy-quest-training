using System;
using System.Collections.Generic;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Collection Catalog", fileName = "CollectionCatalog")]
    public sealed class CollectionCatalogAsset : ScriptableObject, ICollectionCatalogSource
    {
        [SerializeField] CollectionArtifactRecord[] _artifacts = Array.Empty<CollectionArtifactRecord>();
        [SerializeField] CollectionMilestoneRecord[] _milestones = Array.Empty<CollectionMilestoneRecord>();
        [SerializeField] CollectionPresentationThemeRecord _presentationTheme = new CollectionPresentationThemeRecord();

        public IReadOnlyList<CollectionArtifactRecord> Artifacts => _artifacts ?? Array.Empty<CollectionArtifactRecord>();
        public IReadOnlyList<CollectionMilestoneRecord> Milestones => _milestones ?? Array.Empty<CollectionMilestoneRecord>();
        public CollectionPresentationThemeRecord PresentationTheme => _presentationTheme;

        bool ICollectionCatalogSource.TryGetCollectionCatalog(out CollectionCatalog catalog)
            => TryBuild(out catalog, out _);

        public bool TryBuild(out CollectionCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;
            try
            {
                var definitions = new List<CollectionArtifactDefinition>(Artifacts.Count);
                for (var index = 0; index < Artifacts.Count; index++)
                {
                    var record = Artifacts[index];
                    if (record == null) throw new ArgumentException($"Collection artifact {index} is missing.");
                    definitions.Add(record.ToDefinition());
                }

                var milestones = new List<CollectionMilestoneDefinition>(Milestones.Count);
                for (var index = 0; index < Milestones.Count; index++)
                {
                    var record = Milestones[index];
                    if (record == null) throw new ArgumentException($"Collection milestone {index} is missing.");
                    milestones.Add(record.ToDefinition());
                }

                catalog = new CollectionCatalog(definitions, milestones);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Collection catalog is invalid: {exception.Message}";
                return false;
            }
        }

        public bool TryGetArtifactId(SceneId sceneId, out string artifactId)
        {
            artifactId = string.Empty;
            if (!sceneId.IsValid) return false;
            var records = Artifacts;
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record != null && record.Matches(sceneId))
                {
                    artifactId = record.ArtifactId;
                    return !string.IsNullOrWhiteSpace(artifactId);
                }
            }
            return false;
        }

        public bool TryGetArtifactPresentation(string artifactId, out CollectionArtifactRecord presentation)
        {
            presentation = null;
            if (string.IsNullOrWhiteSpace(artifactId)) return false;
            var canonicalId = artifactId.Trim();
            for (var index = 0; index < Artifacts.Count; index++)
            {
                var record = Artifacts[index];
                if (record != null && string.Equals(record.ArtifactId, canonicalId, StringComparison.Ordinal))
                {
                    presentation = record;
                    return record.PresentationPrefab != null;
                }
            }
            return false;
        }

        public bool TryValidatePresentation(out string error)
        {
            if (_presentationTheme == null)
            {
                error = "Collection presentation theme is missing.";
                return false;
            }
            if (!_presentationTheme.IsValid(out error)) return false;
            for (var index = 0; index < Artifacts.Count; index++)
            {
                var artifact = Artifacts[index];
                if (artifact == null)
                {
                    error = $"Collection artifact presentation {index} is missing.";
                    return false;
                }
                if (!artifact.IsPresentationValid(out error))
                {
                    error = $"Collection artifact presentation {index} is invalid: {error}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class CollectionArtifactRecord
    {
        [SerializeField] string _sceneId;
        [SerializeField] string _artifactId;
        [SerializeField] string _visitorTitle;
        [SerializeField, TextArea] string _summary;
        [SerializeField] string _category;
        [SerializeField] int _sortOrder;
        [SerializeField] string _slotKey;
        [SerializeField] bool _countInTotal = true;
        [Header("Presentation")]
        [SerializeField] GameObject _presentationPrefab;
        [SerializeField] Texture2D _foldoutImage;
        [SerializeField] Texture2D _foldoutDetailImage;
        [SerializeField] Vector3 _presentationScale = Vector3.one;
        [SerializeField] Vector3 _catchOffset = new Vector3(0.22f, -0.22f, 0f);
        [SerializeField] Vector3 _returnControlOffset = new Vector3(0f, 0.32f, 0.08f);
        [SerializeField, Min(0.1f)] float _dropDuration = 0.7f;
        [SerializeField, Min(0.1f)] float _returnDuration = 1.15f;
        [SerializeField] Color _accentColor = new Color(0.96f, 0.56f, 0.22f, 1f);
        [SerializeField] AudioClip _dropAudio;
        [SerializeField] AudioClip _collectAudio;
        [SerializeField] AudioClip _returnAudio;

        public string ArtifactId => _artifactId ?? string.Empty;
        public GameObject PresentationPrefab => _presentationPrefab;
        public Texture2D FoldoutImage => _foldoutImage;
        public Texture2D FoldoutDetailImage => _foldoutDetailImage;
        public Vector3 PresentationScale => _presentationScale;
        public Vector3 CatchOffset => _catchOffset;
        public Vector3 ReturnControlOffset => _returnControlOffset;
        public float DropDuration => _dropDuration;
        public float ReturnDuration => _returnDuration;
        public Color AccentColor => _accentColor;
        public AudioClip DropAudio => _dropAudio;
        public AudioClip CollectAudio => _collectAudio;
        public AudioClip ReturnAudio => _returnAudio;

        public CollectionArtifactDefinition ToDefinition()
            => new CollectionArtifactDefinition(
                ArtifactId,
                _visitorTitle,
                _summary,
                _category,
                _sortOrder,
                _slotKey,
                _countInTotal);

        public bool Matches(SceneId sceneId)
            => string.Equals(_sceneId?.Trim(), sceneId.Value, StringComparison.Ordinal);

        public bool IsPresentationValid(out string error)
        {
            if (_foldoutImage == null || _foldoutDetailImage == null)
            {
                error = $"Artifact '{ArtifactId}' requires its approved foldout images.";
                return false;
            }
            if (_presentationPrefab == null)
            {
                error = $"Artifact '{ArtifactId}' has no presentation prefab.";
                return false;
            }
            if (!IsPositiveFinite(_presentationScale.x) || !IsPositiveFinite(_presentationScale.y) ||
                !IsPositiveFinite(_presentationScale.z))
            {
                error = $"Artifact '{ArtifactId}' has an invalid presentation scale.";
                return false;
            }
            if (!IsFinite(_catchOffset) || !IsFinite(_returnControlOffset) ||
                !IsPositiveFinite(_dropDuration) || !IsPositiveFinite(_returnDuration))
            {
                error = $"Artifact '{ArtifactId}' has invalid motion parameters.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    [Serializable]
    public sealed class CollectionPresentationThemeRecord
    {
        [SerializeField] GameObject _worldPrefab;
        [SerializeField, Min(0.2f)] float _viewerDistance = 1.25f;
        [SerializeField] float _verticalOffset = -0.16f;
        [SerializeField, Min(0.1f)] float _rewardHoldSeconds = 1.35f;
        [SerializeField, Min(0.1f)] float _pageSealSeconds = 0.8f;
        [SerializeField] CollectionArtifactMotionThemeRecord _artifactMotion =
            new CollectionArtifactMotionThemeRecord();

        public GameObject WorldPrefab => _worldPrefab;
        public float ViewerDistance => _viewerDistance;
        public float VerticalOffset => _verticalOffset;
        public float RewardHoldSeconds => _rewardHoldSeconds;
        public float PageSealSeconds => _pageSealSeconds;
        public CollectionArtifactMotionThemeRecord ArtifactMotion => _artifactMotion;

        public bool IsValid(out string error)
        {
            if (_worldPrefab == null)
            {
                error = "Collection presentation theme has no world prefab.";
                return false;
            }
            if (!IsPositiveFinite(_viewerDistance) || !IsPositiveFinite(_rewardHoldSeconds) ||
                !IsPositiveFinite(_pageSealSeconds) || float.IsNaN(_verticalOffset) || float.IsInfinity(_verticalOffset))
            {
                error = "Collection presentation theme has invalid placement or timing values.";
                return false;
            }
            if (_artifactMotion == null)
            {
                error = "Collection presentation theme has no Artifact motion theme.";
                return false;
            }
            if (!_artifactMotion.IsValid(out error)) return false;
            error = string.Empty;
            return true;
        }

        static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class CollectionArtifactMotionThemeRecord
    {
        [SerializeField, Range(0.05f, 0.6f)] float _anticipationSeconds = 0.16f;
        [SerializeField, Range(0.05f, 0.6f)] float _readySettleSeconds = 0.18f;
        [SerializeField, Range(0.1f, 0.8f)] float _missReturnSeconds = 0.34f;
        [SerializeField, Range(0.1f, 0.8f)] float _placementSettleSeconds = 0.24f;
        [SerializeField, Range(0.1f, 0.8f)] float _bookRetreatSeconds = 0.34f;
        [SerializeField, Range(0f, 0.25f)] float _missReturnLift = 0.08f;
        [SerializeField, Range(1f, 1.2f)] float _readyPulseScale = 1.07f;
        [SerializeField, Range(1f, 1.2f)] float _heldScale = 1.08f;
        [SerializeField, Range(1f, 1.25f)] float _validScale = 1.12f;
        [SerializeField, Range(0.75f, 1f)] float _invalidScale = 0.94f;
        [SerializeField, Range(1f, 4f)] float _placementFeedbackRadiusMultiplier = 2f;
        [SerializeField, Range(0.2f, 3f)] float _readyPulseCyclesPerSecond = 1.1f;

        public float AnticipationSeconds => _anticipationSeconds;
        public float ReadySettleSeconds => _readySettleSeconds;
        public float MissReturnSeconds => _missReturnSeconds;
        public float PlacementSettleSeconds => _placementSettleSeconds;
        public float BookRetreatSeconds => _bookRetreatSeconds;
        public float MissReturnLift => _missReturnLift;
        public float ReadyPulseScale => _readyPulseScale;
        public float HeldScale => _heldScale;
        public float ValidScale => _validScale;
        public float InvalidScale => _invalidScale;
        public float PlacementFeedbackRadiusMultiplier => _placementFeedbackRadiusMultiplier;
        public float ReadyPulseCyclesPerSecond => _readyPulseCyclesPerSecond;

        public bool IsValid(out string error)
        {
            if (!IsPositiveFinite(_anticipationSeconds) || !IsPositiveFinite(_readySettleSeconds) ||
                !IsPositiveFinite(_missReturnSeconds) || !IsPositiveFinite(_placementSettleSeconds) ||
                !IsPositiveFinite(_bookRetreatSeconds) || !IsFiniteNonNegative(_missReturnLift) ||
                !IsPositiveFinite(_readyPulseScale) || !IsPositiveFinite(_heldScale) ||
                !IsPositiveFinite(_validScale) || !IsPositiveFinite(_invalidScale) ||
                !IsPositiveFinite(_placementFeedbackRadiusMultiplier) ||
                !IsPositiveFinite(_readyPulseCyclesPerSecond) ||
                _readyPulseScale < 1f || _heldScale < 1f || _validScale < 1f ||
                _invalidScale > 1f || _placementFeedbackRadiusMultiplier < 1f)
            {
                error = "Collection Artifact motion theme has invalid timing, lift, scale, or feedback radius values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFiniteNonNegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class CollectionMilestoneRecord
    {
        [SerializeField] string _milestoneId;
        [SerializeField] CollectionMilestoneKind _kind;
        [SerializeField, Min(0)] int _threshold;

        public CollectionMilestoneDefinition ToDefinition()
            => new CollectionMilestoneDefinition(_milestoneId, _kind, _threshold);
    }
}
