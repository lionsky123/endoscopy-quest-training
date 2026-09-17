using System;
using System.Collections.Generic;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace BotanicalGardenQR.Collection.Frontend
{
    public enum CollectionArtifactFeedbackState
    {
        Dormant = 0,
        Ready = 1,
        Hovered = 2,
        Held = 3,
        PlacementValid = 4,
        PlacementInvalid = 5
    }

    /// <summary>
    /// Keeps the current Interaction SDK grab alive until release and forwards
    /// one grab/release cycle to the Collection presentation. It owns no state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectionArtifactGrabRelay : MonoBehaviour, IGameObjectFilter
    {
        const string DiagnosticPrefix = "[CollectionArtifactGrab]";
        static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissiveColorProperty = Shader.PropertyToID("_EmissiveColor");

        [SerializeField, Interface(typeof(IPointable))] UnityEngine.Object _pointableObject;
        [SerializeField] Rigidbody _rigidbody;
        [SerializeField] Collider[] _grabVolumes = Array.Empty<Collider>();
        [SerializeField] Transform _visualRoot;
        [SerializeField] Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField] Color _idleEmission = new Color(0.08f, 0.24f, 0.12f, 1f);
        [SerializeField] Color _hoverEmission = new Color(0.3f, 1f, 0.62f, 1f);
        [SerializeField] Color _validEmission = new Color(0.24f, 1.35f, 0.56f, 1f);
        [SerializeField] Color _invalidEmission = new Color(1.35f, 0.24f, 0.08f, 1f);

        IPointable _pointable;
        HandGrabInteractable _interactable;
        Action<string, CollectionInstanceToken> _grabStarted;
        Action _grabReleased;
        string _artifactId;
        CollectionInstanceToken _instanceToken;
        CollectionArtifactMotionThemeRecord _motionTheme;
        Color _accentColor = new Color(0.96f, 0.56f, 0.22f, 1f);
        MaterialPropertyBlock _propertyBlock;
        RendererMaterialFeedback[] _materialFeedback = Array.Empty<RendererMaterialFeedback>();
        Vector3 _visualBaseScale;
        CollectionArtifactFeedbackState _feedbackState;
        float _feedbackClock;
        bool _armed;
        bool _isSelected;
        bool _subscribed;
        bool _initialized;
        bool _loggedPhysicalContact;
        bool _loggedHover;
        bool _loggedSelect;
        IFrontendGazeSurfaceRegistry _gazeInput;
        IDisposable _inputSuspension;
        readonly HashSet<int> _hoverPointers = new HashSet<int>();

        public bool IsArmed => _armed;
        public bool IsHeld => _isSelected;
        public CollectionArtifactFeedbackState FeedbackState => _feedbackState;

        void Awake()
        {
            EnsureInitialized();
            SetInteractionEnabled(false);
            SetFeedbackState(CollectionArtifactFeedbackState.Dormant, true);
        }

        void Update()
        {
            if (_foldoutDriven) return;
            if (!_initialized || _motionTheme == null || _visualRoot == null) return;
            _feedbackClock += Time.unscaledDeltaTime;
            var multiplier = FeedbackScaleMultiplier(_feedbackState, _motionTheme, _feedbackClock);
            var target = _visualBaseScale * multiplier;
            var response = 1f - Mathf.Exp(-18f * Mathf.Max(0f, Time.unscaledDeltaTime));
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, target, response);
        }

        bool _foldoutDriven;

        void EnsureInitialized()
        {
            if (_initialized) return;
            _foldoutDriven = GetComponent<FieldbookFoldout>() != null;
            _pointable = _pointableObject as IPointable;
            if (_pointable == null)
                throw new InvalidOperationException("Artifact grab relay requires an Interaction SDK IPointable.");
            if (_rigidbody == null)
                throw new InvalidOperationException("Artifact grab relay requires its authored Rigidbody.");
            if (_visualRoot == null || _visualRoot == transform || !_visualRoot.IsChildOf(transform))
                throw new InvalidOperationException(
                    "Artifact grab relay requires one authored child visual root so feedback never scales the grab collider.");
            _interactable = GetComponent<HandGrabInteractable>();
            if (_interactable == null)
                throw new InvalidOperationException(
                    "Artifact grab relay requires the authored root HandGrabInteractable.");
            if (_grabVolumes == null || _grabVolumes.Length == 0)
                throw new InvalidOperationException("Artifact grab relay requires at least one authored grab volume.");
            for (var index = 0; index < _grabVolumes.Length; index++)
            {
                var grabVolume = _grabVolumes[index];
                if (grabVolume == null ||
                    grabVolume.GetComponentInParent<Rigidbody>(true) != _rigidbody)
                    throw new InvalidOperationException(
                        "Every Artifact grab volume must belong to the authored Rigidbody.");
            }
            if (_renderers == null || _renderers.Length == 0)
                throw new InvalidOperationException("Artifact grab relay requires authored renderers for semantic feedback.");
            _propertyBlock = new MaterialPropertyBlock();
            var materialFeedback = new List<RendererMaterialFeedback>();
            for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
            {
                var renderer = _renderers[rendererIndex];
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (material == null) continue;
                    var colorProperty = material.HasProperty(BaseColorProperty)
                        ? BaseColorProperty
                        : material.HasProperty(ColorProperty)
                            ? ColorProperty
                            : -1;
                    var baseColor = colorProperty >= 0 ? material.GetColor(colorProperty) : Color.white;
                    materialFeedback.Add(new RendererMaterialFeedback(
                        renderer,
                        materialIndex,
                        colorProperty,
                        baseColor));
                }
            }
            if (materialFeedback.Count == 0)
                throw new InvalidOperationException(
                    "Artifact grab relay renderers have no authored materials for semantic feedback.");
            _materialFeedback = materialFeedback.ToArray();
            _visualBaseScale = _visualRoot.localScale;
            _initialized = true;
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();
        void OnDestroy() => Unsubscribe();

        public void Configure(
            string artifactId,
            CollectionInstanceToken instanceToken,
            CollectionArtifactMotionThemeRecord motionTheme,
            Color accentColor,
            Action<string, CollectionInstanceToken> grabStarted,
            Action grabReleased,
            IFrontendGazeSurfaceRegistry gazeInput)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
                throw new ArgumentException("A canonical ArtifactId is required.", nameof(artifactId));
            if (!instanceToken.IsValid)
                throw new ArgumentException("A valid collection instance token is required.", nameof(instanceToken));
            if (motionTheme == null)
                throw new ArgumentNullException(nameof(motionTheme));
            _gazeInput = gazeInput ?? throw new ArgumentNullException(nameof(gazeInput));
            if (!motionTheme.IsValid(out var motionError))
                throw new ArgumentException(
                    $"A valid Artifact motion theme is required: {motionError}",
                    nameof(motionTheme));
            EnsureInitialized();
            Subscribe();
            _artifactId = artifactId.Trim();
            _instanceToken = instanceToken;
            _motionTheme = motionTheme;
            _accentColor = accentColor;
            _grabStarted = grabStarted ?? throw new ArgumentNullException(nameof(grabStarted));
            _grabReleased = grabReleased ?? throw new ArgumentNullException(nameof(grabReleased));
            _isSelected = false;
            _hoverPointers.Clear();
            _visualRoot.localScale = _visualBaseScale;
            SetFeedbackState(CollectionArtifactFeedbackState.Dormant, true);
            _loggedPhysicalContact = false;
            _loggedHover = false;
            _loggedSelect = false;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            EnsureInitialized();
            var wasArmed = _armed;
            _armed = enabled;
            if (!enabled) _hoverPointers.Clear();
            _isSelected = false;
            if (enabled)
            {
                if (!wasArmed)
                {
                    _loggedPhysicalContact = false;
                    _loggedHover = false;
                    _loggedSelect = false;
                }
            }
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _interactable.enabled = enabled;
            if (!enabled)
                SetFeedbackState(CollectionArtifactFeedbackState.Dormant);
            else if (!wasArmed)
                SetFeedbackState(CollectionArtifactFeedbackState.Ready);
            if (enabled && !wasArmed)
            {
                LogVerbose(
                    $"{DiagnosticPrefix} stage=Armed artifact={_artifactId} worldPosition={transform.position}",
                    this);
                LogHandInteractorCensus();
            }
        }

        public void PresentPlacementProximity(bool? accepted)
        {
            if (!_armed || !_isSelected) return;
            SetFeedbackState(!accepted.HasValue
                ? CollectionArtifactFeedbackState.Held
                : accepted.Value
                    ? CollectionArtifactFeedbackState.PlacementValid
                    : CollectionArtifactFeedbackState.PlacementInvalid);
        }

        public void PresentPlacementResult(bool accepted)
            => SetFeedbackState(accepted
                ? CollectionArtifactFeedbackState.PlacementValid
                : CollectionArtifactFeedbackState.PlacementInvalid);

        /// <summary>
        /// Either authored HandGrabInteractor may enter the pose-free grab channel.
        /// Selection is allowed only after the scripted drop has reached the catch position.
        /// </summary>
        public bool Filter(GameObject gameObject)
        {
            if (!_armed || gameObject == null) return false;
            return gameObject.GetComponent<HandGrabInteractor>() != null ||
                   gameObject.GetComponentInParent<HandGrabInteractor>() != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("BOTANICAL_DIAGNOSTICS")]
        static void LogHandInteractorCensus()
        {
            var interactors = UnityEngine.Object.FindObjectsByType<HandGrabInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < interactors.Length; index++)
            {
                var interactor = interactors[index];
                var hand = interactor.Hand;
                Debug.Log(
                    $"{DiagnosticPrefix} interactor name={interactor.name} " +
                    $"handedness={(hand != null ? hand.Handedness.ToString() : "null")} " +
                    $"activeAndEnabled={interactor.isActiveAndEnabled} " +
                    $"connected={(hand != null && hand.IsConnected)} " +
                    $"highConfidence={(hand != null && hand.IsHighConfidence)}");
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (!_armed || _loggedPhysicalContact || other == null) return;
            _loggedPhysicalContact = true;
            LogVerbose(
                $"{DiagnosticPrefix} stage=PhysicalContact artifact={_artifactId} " +
                $"other={other.name} layer={LayerMask.LayerToName(other.gameObject.layer)}",
                this);
        }

        void Subscribe()
        {
            if (_subscribed || _pointable == null) return;
            _pointable.WhenPointerEventRaised += HandlePointerEvent;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            _inputSuspension?.Dispose();
            _inputSuspension = null;
            _hoverPointers.Clear();
            if (!_subscribed || _pointable == null) return;
            _pointable.WhenPointerEventRaised -= HandlePointerEvent;
            _subscribed = false;
        }

        void HandlePointerEvent(PointerEvent pointerEvent)
        {
            switch (pointerEvent.Type)
            {
                case PointerEventType.Hover:
                    if (_armed)
                    {
                        _hoverPointers.Add(pointerEvent.Identifier);
                        if (!_loggedHover)
                        {
                            _loggedHover = true;
                            LogVerbose($"{DiagnosticPrefix} stage=Hover artifact={_artifactId}", this);
                        }
                        if (!_isSelected)
                            SetFeedbackState(CollectionArtifactFeedbackState.Hovered);
                    }
                    break;
                case PointerEventType.Unhover:
                    _hoverPointers.Remove(pointerEvent.Identifier);
                    if (_armed && !_isSelected)
                        SetFeedbackState(CollectionArtifactFeedbackState.Ready);
                    break;
                case PointerEventType.Select:
                    if (!_armed || _isSelected || !_instanceToken.IsValid) return;
                    if (!_loggedSelect)
                    {
                        _loggedSelect = true;
                        LogVerbose($"{DiagnosticPrefix} stage=Select artifact={_artifactId}", this);
                    }
                    _isSelected = true;
                    SetFeedbackState(CollectionArtifactFeedbackState.Held);
                    _grabStarted?.Invoke(_artifactId, _instanceToken);
                    break;
                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    _hoverPointers.Remove(pointerEvent.Identifier);
                    if (_isSelected)
                    {
                        _isSelected = false;
                        _grabReleased?.Invoke();
                    }
                    if (_armed)
                        SetFeedbackState(CollectionArtifactFeedbackState.Ready);
                    break;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("BOTANICAL_DIAGNOSTICS")]
        static void LogVerbose(string message, UnityEngine.Object context = null) =>
            Debug.Log(message, context);

        void SetFeedbackState(CollectionArtifactFeedbackState state, bool forceApply = false)
        {
            var interacting = isActiveAndEnabled && _armed && (_isSelected || _hoverPointers.Count != 0);
            if (interacting && _gazeInput != null)
            {
                if (_inputSuspension == null) _inputSuspension = _gazeInput.SuspendPanelInput();
            }
            else { _inputSuspension?.Dispose(); _inputSuspension = null; }
            if (_feedbackState == state && !forceApply) return;
            _feedbackState = state;
            _feedbackClock = 0f;
            PresentEmission(FeedbackEmission(state));
            if (_visualRoot != null && _motionTheme != null)
                _visualRoot.localScale = _visualBaseScale *
                                         FeedbackScaleMultiplier(state, _motionTheme, 0f);
        }

        Color FeedbackEmission(CollectionArtifactFeedbackState state)
        {
            switch (state)
            {
                case CollectionArtifactFeedbackState.Ready:
                    return Color.Lerp(_idleEmission, _accentColor, 0.62f);
                case CollectionArtifactFeedbackState.Hovered:
                    return _hoverEmission;
                case CollectionArtifactFeedbackState.Held:
                    return Boost(_accentColor, 1.22f);
                case CollectionArtifactFeedbackState.PlacementValid:
                    return _validEmission;
                case CollectionArtifactFeedbackState.PlacementInvalid:
                    return _invalidEmission;
                default:
                    return _idleEmission;
            }
        }

        internal static float FeedbackScaleMultiplier(
            CollectionArtifactFeedbackState state,
            CollectionArtifactMotionThemeRecord theme,
            float elapsedSeconds)
        {
            if (theme == null) return 1f;
            switch (state)
            {
                case CollectionArtifactFeedbackState.Ready:
                {
                    var phase = Mathf.Max(0f, elapsedSeconds) * theme.ReadyPulseCyclesPerSecond *
                                Mathf.PI * 2f;
                    var pulse = 0.5f + (0.5f * Mathf.Sin(phase - (Mathf.PI * 0.5f)));
                    return Mathf.Lerp(1f, theme.ReadyPulseScale, pulse);
                }
                case CollectionArtifactFeedbackState.Hovered:
                    return theme.ReadyPulseScale;
                case CollectionArtifactFeedbackState.Held:
                    return theme.HeldScale;
                case CollectionArtifactFeedbackState.PlacementValid:
                    return theme.ValidScale;
                case CollectionArtifactFeedbackState.PlacementInvalid:
                    return theme.InvalidScale;
                default:
                    return 1f;
            }
        }

        static Color Boost(Color color, float multiplier)
            => new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a);

        void PresentEmission(Color color)
        {
            if (_propertyBlock == null) return;
            var tint = FeedbackTint(_feedbackState, out var tintStrength);
            for (var index = 0; index < _materialFeedback.Length; index++)
            {
                var feedback = _materialFeedback[index];
                if (feedback.Renderer == null) continue;
                _propertyBlock.Clear();
                feedback.Renderer.GetPropertyBlock(_propertyBlock, feedback.MaterialIndex);
                if (feedback.ColorProperty >= 0)
                    _propertyBlock.SetColor(
                        feedback.ColorProperty,
                        Color.Lerp(feedback.BaseColor, tint, tintStrength));
                _propertyBlock.SetColor(EmissionColorProperty, color);
                _propertyBlock.SetColor(EmissiveColorProperty, color);
                feedback.Renderer.SetPropertyBlock(_propertyBlock, feedback.MaterialIndex);
            }
        }

        Color FeedbackTint(CollectionArtifactFeedbackState state, out float strength)
        {
            switch (state)
            {
                case CollectionArtifactFeedbackState.Ready:
                    strength = 0.08f;
                    return ClampRgb(_accentColor);
                case CollectionArtifactFeedbackState.Hovered:
                    strength = 0.2f;
                    return ClampRgb(_hoverEmission);
                case CollectionArtifactFeedbackState.Held:
                    strength = 0.24f;
                    return ClampRgb(_accentColor);
                case CollectionArtifactFeedbackState.PlacementValid:
                    strength = 0.82f;
                    return ClampRgb(_validEmission);
                case CollectionArtifactFeedbackState.PlacementInvalid:
                    strength = 0.62f;
                    return ClampRgb(_invalidEmission);
                default:
                    strength = 0f;
                    return Color.white;
            }
        }

        static Color ClampRgb(Color color)
            => new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                color.a);

        readonly struct RendererMaterialFeedback
        {
            public RendererMaterialFeedback(
                Renderer renderer,
                int materialIndex,
                int colorProperty,
                Color baseColor)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                ColorProperty = colorProperty;
                BaseColor = baseColor;
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
            public int ColorProperty { get; }
            public Color BaseColor { get; }
        }
    }
}
