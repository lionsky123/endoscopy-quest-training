using System;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Contracts
{
    public interface IPanoramaEnvironmentMomentEffect
    {
        void Activate();
        void Deactivate();
    }

    [CreateAssetMenu(
        fileName = "PanoramaEnvironmentMomentProfile",
        menuName = "Botanical Garden QR/Content/Panorama Environment Moment Profile")]
    public sealed class PanoramaEnvironmentMomentProfile : ScriptableObject
    {
        [SerializeField] string _momentId;
        [SerializeField] GameObject _prefab;
        [SerializeField] Color _accent = new Color(0.48f, 0.88f, 0.96f, 1f);
        [SerializeField, Min(0.01f)] float _scale = 1f;

        public string MomentId => _momentId ?? string.Empty;
        public GameObject Prefab => _prefab;
        public Color Accent => _accent;
        public float Scale => _scale;

        public bool IsValid(out string reason)
        {
            if (!PanoramaEnvironmentMomentDefinition.IsValidMomentId(MomentId))
            {
                reason = "Environment moment ID must contain 1-64 lowercase letters, digits, '-' or '_'.";
                return false;
            }
            if (_prefab == null)
            {
                reason = $"Environment moment '{MomentId}' requires a prefab.";
                return false;
            }
            if (!PanoramaEnvironmentMomentDefinition.IsFinite(_scale) || _scale <= 0f)
            {
                reason = $"Environment moment '{MomentId}' scale must be positive and finite.";
                return false;
            }
            if (!PanoramaEnvironmentMomentDefinition.IsFinite(_accent.r) ||
                !PanoramaEnvironmentMomentDefinition.IsFinite(_accent.g) ||
                !PanoramaEnvironmentMomentDefinition.IsFinite(_accent.b) ||
                !PanoramaEnvironmentMomentDefinition.IsFinite(_accent.a))
            {
                reason = $"Environment moment '{MomentId}' accent must be finite.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public PanoramaEnvironmentMomentDefinition CreateDefinition()
        {
            if (!IsValid(out var reason))
                throw new InvalidOperationException(reason);
            return new PanoramaEnvironmentMomentDefinition(MomentId, _prefab, _accent, _scale);
        }
    }

    public sealed class PanoramaEnvironmentMomentDefinition
    {
        public PanoramaEnvironmentMomentDefinition(
            string momentId,
            GameObject prefab,
            Color accent,
            float scale = 1f)
        {
            if (!IsValidMomentId(momentId))
                throw new ArgumentException(
                    "Environment moment ID must contain 1-64 lowercase letters, digits, '-' or '_'.",
                    nameof(momentId));
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (!IsFinite(scale) || scale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (!IsFinite(accent.r) || !IsFinite(accent.g) ||
                !IsFinite(accent.b) || !IsFinite(accent.a))
                throw new ArgumentException("Environment moment accent must be finite.", nameof(accent));

            MomentId = momentId;
            Prefab = prefab;
            Accent = new Color(accent.r, accent.g, accent.b, 1f);
            Scale = scale;
        }

        public string MomentId { get; }
        public GameObject Prefab { get; }
        public Color Accent { get; }
        public float Scale { get; }

        internal static bool IsValidMomentId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '-' || character == '_')
                    continue;
                return false;
            }
            return true;
        }

        internal static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
