using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Grab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // A sourced hand tool. The lesson evaluates actual observation through its lens;
    // the optional release callback is retained for callers outside that lesson.
    public sealed class ClinicalObservationToken : MonoBehaviour
    {
        const float SizeMetres = .22f;
        public const float Magnification = 4;
        public Func<Pose> RestPose { private get; set; }
        bool _parkPending;
        Grabbable _grabbable;
        Material _material, _glass, _magnification;
        Transform _lensTransform;
        Vector3 _lensLocalCenter, _lensLocalNormal;
        Action _released;
        Pose _home, _grabStart;
        int? _pointer;
        bool _ready, _grabCanComplete;
        float _cooldown;
        public bool IsHeld => _pointer.HasValue && isActiveAndEnabled;
        public Vector3 LensCenter => _lensTransform.TransformPoint(_lensLocalCenter);
        public Vector3 LensNormal => _lensTransform.TransformDirection(_lensLocalNormal).normalized;

        public static ClinicalObservationToken Create(Transform parent, Action released = null)
        {
            var asset = Resources.Load<GameObject>("ClinicalEvidence/InspectionMagnifier");
            var material = Resources.Load<Material>("ClinicalEvidence/InspectionMagnifierSurface");
            var diffuse = Resources.Load<Texture2D>("ClinicalEvidence/InspectionMagnifierDiffuse");
            if (!asset || !material || !diffuse) throw new InvalidOperationException("Missing sourced observation token.");
            var root = new GameObject("ObservationToken"); root.SetActive(false);
            var token = root.AddComponent<ClinicalObservationToken>(); token._released = released;
            var visual = Instantiate(asset, root.transform); visual.name = "SourcedInspectionMagnifier";
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Observation token has no geometry.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var scale = SizeMetres / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            visual.transform.localScale *= scale;
            visual.transform.localPosition -= bounds.center * scale;
            var lensFilter = visual.GetComponentInChildren<MeshFilter>();
            if (!lensFilter || lensFilter.sharedMesh.subMeshCount < 2) throw new InvalidOperationException("Magnifier must contain a separate lens submesh.");
            var lensBounds = lensFilter.sharedMesh.GetSubMesh(0).bounds;
            token._lensTransform = lensFilter.transform;
            token._lensLocalCenter = lensBounds.center;
            var size = lensBounds.size;
            token._lensLocalNormal = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            token._material = new Material(material) { name = "InspectionMagnifier_Frame" };
            // Resolve the explicit texture resource rather than an imported FBX texture reference.
            token._material.SetTexture("_BaseMap", diffuse);
            token._material.SetTexture("_EmissionMap", diffuse);
            token._glass = new Material(material) { name = "InspectionMagnifier_Lens" };
            foreach (var slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" }) token._glass.SetTexture(slot, null);
            foreach (var keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_EMISSION" }) token._glass.DisableKeyword(keyword);
            token._glass.SetColor("_BaseColor", new Color(.65f, .90f, 1, .12f));
            token._glass.SetFloat("_Surface", 1); token._glass.SetFloat("_ZWrite", 0);
            token._glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            token._glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            token._glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); token._glass.renderQueue = 3000;
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    // This sourced FBX has lens then frame slots. Unity can rename
                    // both imported materials to the filename, so names are not stable.
                    slots[i] = i == 0 ? token._glass : token._material;
                }
                renderer.sharedMaterials = slots;
            }
            // Grip the handle below the lens instead of the empty viewing area.
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, -.046f, 0) * (SizeMetres / .18f);
            collider.size = new Vector3(.045f, .085f, .04f) * (SizeMetres / .18f);
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            token._grabbable = root.AddComponent<Grabbable>();
            token._grabbable.InjectOptionalRigidbody(body); token._grabbable.InjectOptionalThrowWhenUnselected(false);
            var hand = root.AddComponent<HandGrabInteractable>(); hand.InjectRigidbody(body);
            // Only the index/thumb pinch controls this tool: other curled fingers
            // must not retain it when the user opens the pinch to poke a panel.
            hand.InjectSupportedGrabTypes(GrabTypeFlags.Pinch);
            var pinch = new GrabbingRule();
            pinch[HandFinger.Index] = FingerRequirement.Required;
            hand.InjectPinchGrabRules(pinch);
            hand.InjectOptionalPointableElement(token._grabbable); hand.HandAlignment = HandAlignType.None;
            token._grabbable.WhenPointerEventRaised += token.OnPointer;
            root.transform.SetParent(parent, true); // Retain metre scale even under a canvas host.
            root.SetActive(true);
            return token;
        }

        public void ConfigurePanoramaLens(Texture panorama, Shader shader, float yaw, Vector3 origin, float radius)
        {
            if (_magnification) throw new InvalidOperationException("Magnifier lens is already configured.");
            if (!panorama || !shader) throw new ArgumentException("Panorama lens needs an image and shader.");
            _magnification = new Material(shader) { name = "InspectionMagnifier_LivePanorama" };
            _magnification.SetTexture("_MainTex", panorama);
            _magnification.SetFloat("_YawRadians", yaw * Mathf.Deg2Rad);
            _magnification.SetFloat("_Magnification", Magnification);
            _magnification.SetVector("_LensCenterOS", _lensLocalCenter);
            _magnification.SetVector("_PanoramaOrigin", origin);
            _magnification.SetFloat("_PanoramaRadius", radius);
            var renderer = _lensTransform.GetComponent<Renderer>();
            var slots = renderer.sharedMaterials; slots[0] = _magnification; renderer.sharedMaterials = slots;
            SetLensActive(false);
        }
        public void SetLensActive(bool active)
        {
            if (!_magnification) return;
            _magnification.SetFloat("_Active", active && IsHeld && isActiveAndEnabled ? 1 : 0);
        }
        public bool TryRecall(Pose pose)
        {
            if (IsHeld) return false;
            Place(pose); return true;
        }
        public void Dismiss()
        {
            if (_pointer.HasValue)
                _grabbable.ProcessPointerEvent(new PointerEvent(_pointer.Value, PointerEventType.Cancel,
                    new Pose(transform.position, transform.rotation)));
            _pointer = null; SetLensActive(false);
            gameObject.SetActive(false);
        }

        public void Place(Pose pose, bool ready = true)
        {
            _home = pose; _ready = ready; _pointer = null; _cooldown = .6f; _parkPending = false;
            var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1 / parentScale.x, 1 / parentScale.y, 1 / parentScale.z);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
        public void SetReady(bool ready) { _ready = ready; if (!ready) _pointer = null; }
        void OnPointer(PointerEvent evt)
        {
            if (!_ready || !isActiveAndEnabled) return;
            if (evt.Type == PointerEventType.Select && !_pointer.HasValue)
            {
                // The SDK can still grab during the progress cooldown. Track that
                // hold so recovery never pulls the object out of the user's hand.
                _grabCanComplete = _cooldown <= 0;
                _parkPending = false;
                _pointer = evt.Identifier; _grabStart = new Pose(transform.position, transform.rotation);
            }
            else if (_pointer == evt.Identifier && (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel))
            {
                _pointer = null;
                _parkPending = RestPose != null;
                SetLensActive(false);
                var moved = Vector3.Distance(transform.position, _grabStart.position) >= .025f || Quaternion.Angle(transform.rotation, _grabStart.rotation) >= 20;
                _cooldown = .6f;
                if (_grabCanComplete && evt.Type == PointerEventType.Unselect && moved) _released?.Invoke();
            }
        }
        void Update() => Tick(Time.unscaledDeltaTime);
        public void Tick(float seconds)
        {
            _cooldown = Mathf.Max(0, _cooldown - Mathf.Max(0, seconds));
            // Wait until the SDK has finished releasing its transformer before parking.
            if (_parkPending && !IsHeld) { Place(RestPose()); return; }
            if (!IsHeld && Vector3.Distance(transform.position, _home.position) > .30f)
                transform.SetPositionAndRotation(_home.position, _home.rotation);
        }
        void OnDisable() { _pointer = null; _parkPending = false; _cooldown = .6f; SetLensActive(false); }
        void OnApplicationPause(bool paused) { if (paused) { _pointer = null; _cooldown = .6f; SetLensActive(false); } }
        void OnDestroy()
        {
            if (_grabbable) _grabbable.WhenPointerEventRaised -= OnPointer;
            foreach (var material in new[] { _material, _glass, _magnification })
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
