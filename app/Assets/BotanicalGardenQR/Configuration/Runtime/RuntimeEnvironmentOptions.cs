using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Runtime Environment Options", fileName = "RuntimeEnvironmentOptions")]
    public sealed class RuntimeEnvironmentOptions : ScriptableObject
    {
        [SerializeField] RecognitionAdapterOption[] _recognitionAdapters = System.Array.Empty<RecognitionAdapterOption>();
        [SerializeField, Min(0f)] float _recognitionDebounceSeconds = 0.35f;
        [SerializeField, Min(0.1f)] float _scanConfirmationSeconds = 1f;
        [SerializeField, Min(0f)] float _scanLostGraceSeconds = 0.2f;
        [SerializeField, Range(0f, 10f)] float _scanFocusPaddingDegrees = 2f;
        [SerializeField, Min(0f)] float _scanGazeLostGraceSeconds = 0.15f;
        [SerializeField, Min(0.1f)] float _videoPrepareTimeoutSeconds = 15f;
        [SerializeField] bool _fieldbookEnabled;
        [SerializeField, Min(.1f)] float _arrivalRadius = 1.2f;
        [SerializeField, Min(.1f)] float _arrivalExitRadius = 1.5f;
        [SerializeField, Min(.1f)] float _arrivalStableSeconds = .35f;
        public bool FieldbookEnabled => _fieldbookEnabled;
        public float ArrivalRadius => _arrivalRadius;
        public float ArrivalExitRadius => _arrivalExitRadius;
        public float ArrivalStableSeconds => _arrivalStableSeconds;
        public IReadOnlyList<RecognitionAdapterOption> RecognitionAdapters => _recognitionAdapters;
        public float RecognitionDebounceSeconds => _recognitionDebounceSeconds;
        public float ScanConfirmationSeconds => _scanConfirmationSeconds;
        public float ScanLostGraceSeconds => _scanLostGraceSeconds;
        public float ScanFocusPaddingDegrees => _scanFocusPaddingDegrees;
        public float ScanGazeLostGraceSeconds => _scanGazeLostGraceSeconds;
        public float VideoPrepareTimeoutSeconds => _videoPrepareTimeoutSeconds;
        public VideoRuntimeOptions Video => new VideoRuntimeOptions(_videoPrepareTimeoutSeconds);
    }

    [System.Serializable]
    public sealed class RecognitionAdapterOption
    {
        [SerializeField] string _sourceKind;
        [SerializeField] bool _enabled = true;
        public SourceKind SourceKind => new SourceKind(_sourceKind);
        public string SerializedSourceKind => _sourceKind ?? string.Empty;
        public bool Enabled => _enabled;
    }
}
