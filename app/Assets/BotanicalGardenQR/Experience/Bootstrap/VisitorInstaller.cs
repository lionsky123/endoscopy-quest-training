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
    [DefaultExecutionOrder(-10000)]
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
        internal FullScriptJourneyRuntime Journey => _fullScript;
        FullScriptJourneyRuntime _fullScript;
        readonly VisitorDiagnosticThrottle _unityDiagnosticThrottle = new VisitorDiagnosticThrottle(
            UnityDiagnosticRepeatInterval,
            MaximumTrackedUnityDiagnosticSignatures);
        string _diagnosticPath;
        bool _diagnosticsAttached;

        void Awake()
        {
            BeginDiagnostics();
            RecordStartup("installer.awake");
            if (_runtimeOptions == null || !_runtimeOptions.VirtualRoomEnabled)
            {
                RecordStartup("installer.configuration.failed",
                    "The formal visitor scene requires the stationary full-script configuration.");
                PrimeStartup("当前体验暂不可用，请联系工作人员。");
                return;
            }

            try
            {
                var bindings = CreateValidatedBindings();
                RecordStartup("installer.bindings.validated");
                var preview=false;
#if UNITY_EDITOR
                preview=InspectionEditorPreview.Enabled;
                if(preview)InspectionEditorPreview.Configure(bindings);
#endif
                _fullScript = new FullScriptJourneyRuntime(bindings, RecordDiagnostic, editorPreview:preview);
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
            if (_composition == null && _fullScript == null) return;
            try
            {
                _fullScript?.StartExperience();
                _composition?.StartExperience();
                RecordStartup("installer.journey.started");
            }
            catch (Exception exception)
            {
                RecordStartup(
                    "installer.journey.failed",
                    $"{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
                Debug.LogException(exception, this);
                PrimeStartup("应用启动失败，请联系工作人员。");
            }
        }

        void Update()
        {
            _composition?.Tick(Time.unscaledDeltaTime);
            _fullScript?.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            _composition?.Dispose();
            _fullScript?.Dispose();
            _fullScript = null;
            _composition = null;
            RecordStartup("installer.destroyed");
            if (_diagnosticsAttached)
            {
                Application.logMessageReceivedThreaded -= CaptureUnityLog;
                _diagnosticsAttached = false;
            }
        }

#if UNITY_EDITOR
        // Explicit historical test/preview opt-in only. Not compiled into the player
        // and never called by Awake or the current scene authoring command.
        internal void ConfigureArchivedBindingsForEditor()
        {
            T Load<T>(string guid) where T : UnityEngine.Object
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (!asset) throw new InvalidOperationException("Archived configuration missing: " + guid);
                return asset;
            }
            _sceneLibrary = Load<ContentSceneLibrary>("6882fa79dfb755e4a8ec85c493af5106");
            _contentEntries = Load<ContentEntryCatalog>("65c027806e6ed8a49b7d3c80e994262c");
            _collectionCatalog = Load<CollectionCatalogAsset>("48a9027c1b724684b651cd26c95146f6");
            _physicalAugmentationCatalog = Load<PhysicalAugmentationCatalogAsset>("c72db1d79b984f2fb572b2329aff3fa2");
        }
#endif

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
            var stationary = _runtimeOptions != null && _runtimeOptions.VirtualRoomEnabled;
            var presentation = new VisitorRuntimeBindings.PresentationBindings(
                _frontendShell,
                _observationCompletionFrontend,
                _headGazeInteraction,
                _startupRecallPresentation,
                _gazeReticlePresentation,
                _spatialDisplayRoot,
                _atlasHubPresentationPrefab,
                stationary ? null : _featurePages,
                stationary);
            var runtimeRoots = new VisitorRuntimeBindings.RuntimeRootBindings(
                _videoRuntimeRoot,
                _panoramaRuntimeRoot,
                _modelRuntimeRoot,
                _narrationRuntimeRoot,
                _fairyRuntimeRoot,
                _effectRuntimeRoot,
                _physicalAugmentationRuntimeHost,
                _activationDriver,
                stationary);
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
                                 (condition.StartsWith("[LobbyGaussian]", StringComparison.Ordinal) ||
                                  condition.IndexOf("QRCode", StringComparison.OrdinalIgnoreCase) >= 0 ||
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

        long _cachedDiagnosticBytes = -1;

        void RecordStartup(string stage, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(_diagnosticPath)) return;
            try
            {
                var line = $"{DateTimeOffset.UtcNow:O}\t{stage}\t{Sanitize(detail)}{Environment.NewLine}";
                var byteCount = DiagnosticEncoding.GetByteCount(line);
                lock (DiagnosticWriteLock)
                {
                    if (_cachedDiagnosticBytes < 0)
                        _cachedDiagnosticBytes = File.Exists(_diagnosticPath) ? new FileInfo(_diagnosticPath).Length : 0;
                    if (_cachedDiagnosticBytes + byteCount > DiagnosticMaximumBytes)
                    {
                        EnsureDiagnosticCapacity(byteCount);
                        _cachedDiagnosticBytes = File.Exists(_diagnosticPath) ? new FileInfo(_diagnosticPath).Length : 0;
                    }
                    File.AppendAllText(_diagnosticPath, line, DiagnosticEncoding);
                    _cachedDiagnosticBytes += byteCount;
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

}
