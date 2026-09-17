using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BotanicalGardenQR.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class VisitorInstaller : MonoBehaviour
    {
        [Header("Published configuration")]
        [SerializeField] ContentSceneLibrary _sceneLibrary;
        [SerializeField] ContentEntryCatalog _contentEntries;
        [SerializeField] RuntimeEnvironmentOptions _runtimeOptions;
        [SerializeField] FairyApplicationConfiguration _fairyConfiguration;
        [SerializeField] GlobalUiDefaults _globalUiDefaults;
        [SerializeField] CollectionCatalogAsset _collectionCatalog;
        [SerializeField] TextAsset _visitorMapDefinition;
        [SerializeField] Material _guidanceRouteMaterial;
        [SerializeField] VisitorPrologueThemeAsset _prologueTheme;
        [SerializeField] VisitorCoachThemeAsset _visitorCoachTheme;
        [SerializeField] PhysicalAugmentationCatalogAsset _physicalAugmentationCatalog;

        [Header("Platform roots")]
        [SerializeField] GameObject _xrRigRoot;
        [SerializeField] Transform _interactionRigRoot;
        [SerializeField] GameObject _mrukRoot;
        [SerializeField] GameObject _eventSystemRoot;
        [SerializeField] Transform _viewer;
        [SerializeField] OVRPassthroughLayer _fairyArrivalPassthroughLayer;
        [SerializeField] Light _fairyArrivalEnvironmentLight;
        [SerializeField] MonoBehaviour _spatialDataPermissionGate;
        [SerializeField] MonoBehaviour[] _recognitionSourceAdapters;

        [Header("Frontend and host")]
        [SerializeField] GlobalFrontendShell _frontendShell;
        [SerializeField] KnowledgeMiniGameFrontend _observationCompletionFrontend;
        [SerializeField] HeadGazeDwellController _headGazeInteraction;
        [SerializeField] StartupRecallPresenter _startupRecallPresentation;
        [SerializeField] GazeReticlePresenter _gazeReticlePresentation;
        [SerializeField] Transform _spatialDisplayRoot;
        [SerializeField] GameObject _atlasHubPresentationPrefab;
        [SerializeField] FeaturePageBindings _featurePages = new FeaturePageBindings();

        [Header("Feature runtime roots")]
        [SerializeField] Transform _videoRuntimeRoot;
        [SerializeField] Transform _panoramaRuntimeRoot;
        [SerializeField] Transform _modelRuntimeRoot;
        [SerializeField] Transform _narrationRuntimeRoot;
        [SerializeField] Transform _fairyRuntimeRoot;
        [SerializeField] Transform _effectRuntimeRoot;
        [SerializeField] PhysicalAugmentationRuntimeHost _physicalAugmentationRuntimeHost;

        [Header("Runtime drivers")]
        [SerializeField] ActivationCoordinatorDriver _activationDriver;

        const int DiagnosticMaximumBytes = 256 * 1024;
        const int DiagnosticRetainedBytes = 192 * 1024;
        const int MaximumDiagnosticDetailCharacters = 8192;
        const int MaximumDiagnosticSignatureCharacters = 512;
        const int MaximumTrackedUnityDiagnosticSignatures = 32;
        static readonly TimeSpan UnityDiagnosticRepeatInterval = TimeSpan.FromSeconds(5d);

        static readonly object DiagnosticWriteLock = new object();
        static readonly Encoding DiagnosticEncoding = new UTF8Encoding(false);
        static readonly Regex QrPayloadFieldPattern = new Regex(
            @"(?im)(\b(?:qr\s*)?payload\s*[:=]\s*)([^\r\n\t;]+)",
            RegexOptions.CultureInvariant);
        static readonly Regex QrSourceFieldPattern = new Regex(
            @"(?im)(\bsource\s*=\s*qr\s*:\s*)([^\r\n\t;]+)",
            RegexOptions.CultureInvariant);
        VisitorRuntimeComposition _composition;
        readonly VisitorDiagnosticThrottle _unityDiagnosticThrottle = new VisitorDiagnosticThrottle(
            UnityDiagnosticRepeatInterval,
            MaximumTrackedUnityDiagnosticSignatures);
        string _diagnosticPath;
        bool _diagnosticsAttached;

        void Awake()
        {
            BeginDiagnostics();
            RecordStartup("installer.awake");
            PrimeStartup(DefaultStartupHint());
            try
            {
                var bindings = CreateValidatedBindings();
                RecordStartup("installer.bindings.validated");
                RecordStartup(
                    "installer.recognition.created",
                    $"count={bindings.Platform.RecognitionSources.Count}");
                _composition = VisitorRuntimeComposition.Create(bindings, RecordDiagnostic);
                RecordStartup("installer.composition.ready");
            }
            catch (Exception exception)
            {
                var cause = RootCause(exception);
                RecordStartup(
                    "installer.composition.failed",
                    $"exception={exception.GetType().FullName}: {exception.Message}\n" +
                    $"root={cause.GetType().FullName}: {cause.Message}\n{exception.StackTrace}");
                Debug.LogException(exception, this);
                PrimeStartup("应用启动异常，请联系工作人员。");
            }
        }

        void Start()
        {
            if (_composition == null) return;
            try
            {
                _composition.StartExperience();
                RecordStartup("installer.prologue.started");
            }
            catch (Exception exception)
            {
                RecordStartup(
                    "installer.prologue.failed",
                    $"{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
                Debug.LogException(exception, this);
                PrimeStartup("探索序章启动失败，请联系工作人员。");
            }
        }

        void Update()
        {
            _composition?.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            _composition?.Dispose();
            _composition = null;
            RecordStartup("installer.destroyed");
            if (_diagnosticsAttached)
            {
                Application.logMessageReceivedThreaded -= CaptureUnityLog;
                _diagnosticsAttached = false;
            }
        }

        internal VisitorRuntimeBindings CreateValidatedBindings()
        {
            var configuration = new VisitorRuntimeBindings.ConfigurationBindings(
                _sceneLibrary,
                _contentEntries,
                _runtimeOptions,
                _fairyConfiguration,
                _globalUiDefaults,
                _collectionCatalog,
                _prologueTheme,
                _visitorCoachTheme,
                _physicalAugmentationCatalog, _visitorMapDefinition, _guidanceRouteMaterial);
            var platform = new VisitorRuntimeBindings.PlatformBindings(
                _xrRigRoot,
                _interactionRigRoot,
                _mrukRoot,
                _eventSystemRoot,
                _viewer,
                _fairyArrivalPassthroughLayer,
                _fairyArrivalEnvironmentLight,
                _spatialDataPermissionGate,
                _recognitionSourceAdapters,
                _runtimeOptions);
            var presentation = new VisitorRuntimeBindings.PresentationBindings(
                _frontendShell,
                _observationCompletionFrontend,
                _headGazeInteraction,
                _startupRecallPresentation,
                _gazeReticlePresentation,
                _spatialDisplayRoot,
                _atlasHubPresentationPrefab,
                _featurePages);
            var runtimeRoots = new VisitorRuntimeBindings.RuntimeRootBindings(
                _videoRuntimeRoot,
                _panoramaRuntimeRoot,
                _modelRuntimeRoot,
                _narrationRuntimeRoot,
                _fairyRuntimeRoot,
                _effectRuntimeRoot,
                _physicalAugmentationRuntimeHost,
                _activationDriver);
            return new VisitorRuntimeBindings(configuration, platform, presentation, runtimeRoots);
        }

        string DefaultStartupHint()
            => _globalUiDefaults != null && !string.IsNullOrWhiteSpace(_globalUiDefaults.StartupHint)
                ? _globalUiDefaults.StartupHint
                : string.Empty;

        static Exception RootCause(Exception exception)
        {
            var cause = exception ?? throw new ArgumentNullException(nameof(exception));
            while (cause.InnerException != null) cause = cause.InnerException;
            return cause;
        }

        void PrimeStartup(string startupHint)
        {
            try
            {
                if (_viewer != null && _gazeReticlePresentation != null)
                {
                    _gazeReticlePresentation.Prime(_viewer);
                    RecordStartup("startup.reticle.primed");
                }
            }
            catch (Exception exception)
            {
                RecordStartup("startup.reticle.failed", $"{exception.GetType().FullName}: {exception.Message}");
                Debug.LogException(exception, this);
            }

            try
            {
                if (_viewer != null && _startupRecallPresentation != null)
                {
                    _startupRecallPresentation.Prime(_viewer, startupHint);
                    RecordStartup("startup.prompt.primed");
                }
            }
            catch (Exception exception)
            {
                RecordStartup("startup.prompt.failed", $"{exception.GetType().FullName}: {exception.Message}");
                Debug.LogException(exception, this);
            }
        }

        void BeginDiagnostics()
        {
            try
            {
                var directory = Path.Combine(
                    Application.persistentDataPath,
                    "BotanicalGardenQR",
                    "Diagnostics");
                Directory.CreateDirectory(directory);
                _diagnosticPath = Path.Combine(directory, "startup.log");
                Application.logMessageReceivedThreaded += CaptureUnityLog;
                _diagnosticsAttached = true;
            }
            catch
            {
                _diagnosticPath = null;
                _diagnosticsAttached = false;
            }
        }

        void CaptureUnityLog(string condition, string stackTrace, LogType type)
        {
            var trackerMessage = condition != null &&
                                 (condition.IndexOf("QRCode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  condition.IndexOf("MRUK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  condition.IndexOf("PhysicalAugmentation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  condition.IndexOf("physical_locator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  condition.IndexOf("physical_augmentation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  condition.IndexOf("EnvironmentDepth", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!trackerMessage && type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            var detail = string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace;
            var signature = BuildDiagnosticSignature(type, condition, stackTrace);
            if (!_unityDiagnosticThrottle.TryAccept(signature, DateTimeOffset.UtcNow, out var suppressedRepeats))
                return;
            if (suppressedRepeats > 0)
                detail = $"suppressed_repeats={suppressedRepeats}; {detail}";
            RecordStartup($"unity.{type}", detail);
        }

        void RecordStartup(string stage, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(_diagnosticPath)) return;
            try
            {
                var line = $"{DateTimeOffset.UtcNow:O}\t{stage}\t{Sanitize(detail)}{Environment.NewLine}";
                lock (DiagnosticWriteLock)
                {
                    EnsureDiagnosticCapacity(DiagnosticEncoding.GetByteCount(line));
                    File.AppendAllText(_diagnosticPath, line, DiagnosticEncoding);
                }
            }
            catch
            {
                // Diagnostics must never become a startup dependency.
            }
        }

        void RecordDiagnostic(DiagnosticEvent diagnostic)
        {
            if (diagnostic == null) return;
            RecordStartup(
                $"diagnostic.{diagnostic.OwningLine}.{diagnostic.Code}",
                $"stage={diagnostic.Stage}; source={diagnostic.SourceKey}; session={diagnostic.SessionToken}; asset={diagnostic.AssetPath}; message={diagnostic.Message}");
        }

        void EnsureDiagnosticCapacity(int incomingBytes)
        {
            if (!File.Exists(_diagnosticPath)) return;

            var currentBytes = new FileInfo(_diagnosticPath).Length;
            if (currentBytes + incomingBytes <= DiagnosticMaximumBytes) return;

            var retainedBytes = (int)Math.Min(
                (long)DiagnosticRetainedBytes,
                Math.Max(0L, (long)DiagnosticMaximumBytes - incomingBytes));
            if (retainedBytes <= 0)
            {
                File.WriteAllText(_diagnosticPath, string.Empty, DiagnosticEncoding);
                return;
            }

            var bytesToRead = (int)Math.Min(currentBytes, retainedBytes);
            var tail = new byte[bytesToRead];
            using (var stream = new FileStream(_diagnosticPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(-bytesToRead, SeekOrigin.End);
                var offset = 0;
                while (offset < tail.Length)
                {
                    var read = stream.Read(tail, offset, tail.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }

                if (offset != tail.Length)
                    Array.Resize(ref tail, offset);
            }

            var retained = DiagnosticEncoding.GetString(tail);
            var firstCompleteLine = retained.IndexOf('\n');
            if (firstCompleteLine >= 0)
                retained = retained.Substring(firstCompleteLine + 1);
            File.WriteAllText(_diagnosticPath, retained, DiagnosticEncoding);
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var redacted = QrPayloadFieldPattern.Replace(value, "$1<redacted>");
            redacted = QrSourceFieldPattern.Replace(redacted, "$1<redacted>");
            if (redacted.Length > MaximumDiagnosticDetailCharacters)
                redacted = redacted.Substring(0, MaximumDiagnosticDetailCharacters) + "...<truncated>";
            return redacted.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        static string BuildDiagnosticSignature(LogType type, string condition, string stackTrace)
        {
            var signature = $"{type}|{Sanitize(FirstLine(condition))}|{Sanitize(FirstLine(stackTrace))}";
            return signature.Length <= MaximumDiagnosticSignatureCharacters
                ? signature
                : signature.Substring(0, MaximumDiagnosticSignatureCharacters);
        }

        static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var carriageReturn = value.IndexOf('\r');
            var lineFeed = value.IndexOf('\n');
            var separator = carriageReturn < 0
                ? lineFeed
                : lineFeed < 0 ? carriageReturn : Math.Min(carriageReturn, lineFeed);
            return separator < 0 ? value : value.Substring(0, separator);
        }
    }

    internal sealed class VisitorDiagnosticThrottle
    {
        readonly object _sync = new object();
        readonly TimeSpan _repeatInterval;
        readonly int _capacity;
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        internal VisitorDiagnosticThrottle(TimeSpan repeatInterval, int capacity)
        {
            if (repeatInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(repeatInterval));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _repeatInterval = repeatInterval;
            _capacity = capacity;
        }

        internal bool TryAccept(string signature, DateTimeOffset timestamp, out int suppressedRepeats)
        {
            signature = signature ?? string.Empty;
            lock (_sync)
            {
                if (_entries.TryGetValue(signature, out var entry))
                {
                    if (timestamp - entry.LastAcceptedAt < _repeatInterval)
                    {
                        if (entry.SuppressedRepeats < int.MaxValue) entry.SuppressedRepeats++;
                        suppressedRepeats = 0;
                        return false;
                    }

                    suppressedRepeats = entry.SuppressedRepeats;
                    entry.LastAcceptedAt = timestamp;
                    entry.SuppressedRepeats = 0;
                    return true;
                }

                if (_entries.Count >= _capacity) RemoveOldest();
                _entries.Add(signature, new Entry(timestamp));
                suppressedRepeats = 0;
                return true;
            }
        }

        void RemoveOldest()
        {
            string oldestSignature = null;
            var oldestTimestamp = DateTimeOffset.MaxValue;
            foreach (var pair in _entries)
            {
                if (pair.Value.LastAcceptedAt >= oldestTimestamp) continue;
                oldestTimestamp = pair.Value.LastAcceptedAt;
                oldestSignature = pair.Key;
            }
            if (oldestSignature != null) _entries.Remove(oldestSignature);
        }

        sealed class Entry
        {
            internal Entry(DateTimeOffset lastAcceptedAt) => LastAcceptedAt = lastAcceptedAt;
            internal DateTimeOffset LastAcceptedAt;
            internal int SuppressedRepeats;
        }
    }
}
