using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>Scoped reality tint, owned by the arrival scope.</summary>
    internal sealed class FairyArrivalPassage
    {
        readonly GameObject _veil;
        readonly Material _material;
        readonly Mesh _veilMesh;
        readonly Color[] _veilColors = new Color[4];

        internal FairyArrivalPassage(Transform viewer)
        {
            _material = new Material(Shader.Find("UI/Default Correct")) { name = "Cross-world light", renderQueue = 3100 };
            _veil = new GameObject("ArrivalRealityTint", typeof(MeshFilter), typeof(MeshRenderer));
            _veil.transform.SetParent(viewer, false);
            var camera = viewer.GetComponent<Camera>();
            var distance = camera != null ? Mathf.Max(.25f, camera.nearClipPlane + .08f) : .25f;
            _veil.transform.localPosition = Vector3.forward * distance;
            _veil.transform.localScale = new Vector3(distance * 6, distance * 5, 1);
            _veilMesh = new Mesh { name = "Scoped reality tint" };
            _veilMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            _veilMesh.triangles = new[] { 0,2,1,0,3,2 }; _veilMesh.RecalculateBounds();
            _veil.GetComponent<MeshFilter>().sharedMesh = _veilMesh;
            _veil.GetComponent<MeshRenderer>().sharedMaterial = _material;
            Tick(0, 24);
        }

        internal void Tick(float elapsed, float landedAt, float collision = 0, float worldShift = 0, float flash = 0)
        {
            var ending = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - landedAt) / 3f));
            var tint = (worldShift * .2f + collision * .04f + flash * .23f) * ending;
            _veil.SetActive(tint > .001f);
            for (var i = 0; i < 4; i++) _veilColors[i] = Color.Lerp(new Color(.12f,.22f,.3f,tint), new Color(.95f,.82f,.52f,tint), flash);
            _veilMesh.colors = _veilColors;
        }

        internal void Dispose()
        {
            _veil.SetActive(false);
            foreach (var value in new Object[] { _veil, _veilMesh, _material })
                if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
