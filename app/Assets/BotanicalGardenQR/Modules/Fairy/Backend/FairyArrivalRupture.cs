using UnityEngine;
using UnityEngine.Rendering;

namespace BotanicalGardenQR.Fairy.Backend
{
    // Presentation adapter for the attributed Spektr and Mirza shader sources.
    // Owns only transient geometry/materials; the arrival controller owns progression.
    internal sealed class FairyArrivalRupture
    {
        readonly GameObject _root, _overlay;
        readonly Material[] _bolts = new Material[5];
        readonly Material _lines;
        readonly Mesh _ribbon;
        readonly Transform _viewer;
        readonly Vector3 _door, _right;

        internal FairyArrivalRupture(Transform owner, Transform viewer, Vector3 door, Shader boltShader, Shader linesShader)
        {
            _viewer = viewer; _door = door;
            _right = Vector3.Cross(Vector3.up, (door - viewer.position).normalized).normalized;
            _root = new GameObject("ArrivalSpatialDischarge");
            _root.transform.SetParent(owner, false);
            _root.transform.position = Vector3.zero; _root.transform.rotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            // Spektr's line coordinates, widened into a ribbon for readable mobile rendering.
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                vertices[i * 2] = vertices[i * 2 + 1] = new Vector3((float)i / segments, .37f, 0);
                uv[i * 2] = new Vector2(0, -1); uv[i * 2 + 1] = new Vector2(0, 1);
                if (i == segments) continue;
                var t = i * 6; var v = i * 2;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v + 1; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
            }
            _ribbon = new Mesh { name = "Spektr ribbon topology" };
            _ribbon.vertices = vertices; _ribbon.uv = uv; _ribbon.triangles = indices;
            _ribbon.bounds = new Bounds(door, Vector3.one * 12);
            for (var i = 0; i < _bolts.Length; i++)
            {
                var go = new GameObject("SpatialBolt", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(_root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = _ribbon;
                _bolts[i] = new Material(boltShader);
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _bolts[i];
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            _overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _overlay.name = "ArrivalPressureSpeedLines";
            var collider = _overlay.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            _overlay.transform.SetParent(viewer, false);
            var camera = viewer.GetComponent<Camera>();
            var depth = camera != null ? Mathf.Max(.4f, camera.nearClipPlane + .1f) : .4f;
            _overlay.transform.localPosition = Vector3.forward * depth;
            _overlay.transform.localScale = new Vector3(depth * 6, depth * 5, 1);
            _lines = new Material(linesShader);
            _lines.SetFloat("_SpeedLinesTiling", 95); _lines.SetFloat("_SpeedLinesRadialScale", .8f);
            _lines.SetFloat("_SpeedLinesPower", 1); _lines.SetFloat("_SpeedLinesRemap", .55f);
            _lines.SetFloat("_SpeedLinesAnimation", 4); _lines.SetFloat("_MaskScale", 1.1f);
            _lines.SetFloat("_MaskHardness", .1f); _lines.SetFloat("_MaskPower", 2);
            _overlay.GetComponent<MeshRenderer>().sharedMaterial = _lines;
            _root.SetActive(false); _overlay.SetActive(false);
        }

        internal void Tick(float seconds, Vector3 artifact)
        {
            var returnWave = FairyController.ArrivalPulse(seconds, .2f, .55f, 1.15f);
            var rupture = FairyController.ArrivalPulse(seconds, 1.35f, 1.65f, 2.65f);
            var strength = Mathf.Max(returnWave * .65f, rupture);
            _root.SetActive(strength > .01f); _overlay.SetActive(strength > .01f);
            _root.transform.position = Vector3.zero;
            _root.transform.rotation = Quaternion.identity;
            _lines.SetFloat("_ArrivalTime", seconds);
            _lines.SetColor("_Colour", new Color(.9f, .82f, .6f, strength * .85f));
            for (var i = 0; i < _bolts.Length; i++)
            {
                var from = i == 0 ? artifact : _door + _right * ((i % 2 == 0 ? -1 : 1) * 2f) + Vector3.up * (i < 3 ? .8f : -.65f);
                var to = _door + Vector3.up * ((i - 2) * .27f);
                var direction = (to - from).normalized;
                var side = Vector3.Cross(direction, (_viewer.position - from).normalized).normalized;
                var mat = _bolts[i];
                mat.SetVector("_Point0", from); mat.SetVector("_Point1", to);
                mat.SetVector("_Axis0", direction); mat.SetVector("_Axis1", side);
                mat.SetVector("_Axis2", Vector3.Cross(direction, side));
                mat.SetFloat("_Distance", Vector3.Distance(from, to));
                mat.SetFloat("_ArrivalTime", seconds); mat.SetFloat("_Seed", i * 17);
                mat.SetFloat("_Throttle", 1); mat.SetVector("_Interval", new Vector2(.16f, .27f));
                mat.SetVector("_Length", Vector2.one);
                mat.SetVector("_NoiseAmplitude", new Vector2(.13f, .035f));
                mat.SetVector("_NoiseFrequency", new Vector2(2, 17));
                mat.SetVector("_NoiseMotion", new Vector2(3, 8));
                mat.SetColor("_Color", new Color(2.4f, 1.6f, .7f, i == 0 ? strength : rupture * .8f));
            }
        }

        internal void Dispose()
        {
            if (_root != null) _root.SetActive(false);
            if (_overlay != null) _overlay.SetActive(false);
            Destroy(_root); Destroy(_overlay); Destroy(_ribbon); Destroy(_lines);
            foreach (var bolt in _bolts) Destroy(bolt);
        }
        static void Destroy(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
