using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>A fixed world-space boundary. The serialized type is retained for existing prefabs.</summary>
    public sealed class FairyArrivalFlashlightAperture : MonoBehaviour
    {
        Transform _slice;
        Mesh _mesh;
        Vector3 _center;
        Quaternion _rotation;
        bool _initialized;
        float _opening;
        internal float CurrentStrength { get; private set; }
        internal Transform Plane => _slice;
        internal float Opening => _opening;

        internal void Initialize(Transform viewer, Transform target)
        {
            if (_initialized) throw new InvalidOperationException("Arrival boundary is already initialized.");
            if (viewer == null || target == null) throw new ArgumentNullException();
            var source = transform.Find("parent/LightVolumes/LightVolumeA");
            if (source == null) throw new InvalidOperationException("Arrival boundary mesh is missing.");
            // The old beam renderers have no role in a spatial opening. Keep only one mesh anchor.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            _slice = Instantiate(source.gameObject, source.parent).transform;
            _slice.name = "LightVolumeD";
            _mesh = new Mesh { name = "Spatial boundary plane" };
            _mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            _mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _mesh.triangles = new[] { 0,2,1,0,3,2 };
            _mesh.RecalculateBounds();
            _slice.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _slice.GetComponent<Renderer>().enabled = false;
            var direction = target.position - viewer.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .0001f) direction = Vector3.forward;
            _rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _center = target.position + Vector3.up * .8f;
            _initialized = true;
            SetOpeningProgress(0);
            Align();
        }

        internal float SetFlickerTime(float progress)
        {
            CurrentStrength = Mathf.Clamp01(progress);
            return CurrentStrength;
        }

        internal void SetOpeningProgress(float progress)
        {
            _opening = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            Align();
        }

        internal void SetInvitationWindow(Vector3 origin, Vector3 destination, float travel)
        {
            // Invitation motion belongs to its leaf current. The actual door never follows the viewer or guide.
            _center = destination;
            Align();
        }

        internal void SetClosingProgress(float progress)
        {
            CurrentStrength = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
        }

        void LateUpdate() => Align();
        internal void RefreshPose() => Align();

        void Align()
        {
            if (!_initialized || _slice == null) return;
            _slice.SetPositionAndRotation(_center, _rotation);
            var parentScale = _slice.parent.lossyScale;
            // Geometry stays full-size. The shared signed boundary controls opening and body clipping.
            _slice.localScale = new Vector3(1.7f / parentScale.x, 1.8f / parentScale.y, 1f / parentScale.z);
        }

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
    }
}
