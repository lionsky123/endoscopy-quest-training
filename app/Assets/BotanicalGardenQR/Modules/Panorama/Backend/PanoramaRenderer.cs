using System;
using System.Collections;
using System.IO;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace BotanicalGardenQR.Panorama.Backend
{
    internal enum PanoramaRenderFailure { None, UnsupportedResource, LoadFailed, RenderFailed }

    internal readonly struct PanoramaRenderResult
    {
        PanoramaRenderResult(bool succeeded, PanoramaRenderFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public PanoramaRenderFailure Failure { get; }
        public static PanoramaRenderResult Success() => new PanoramaRenderResult(true, PanoramaRenderFailure.None);
        public static PanoramaRenderResult Fail(PanoramaRenderFailure failure) => new PanoramaRenderResult(false, failure);
    }

    internal sealed class PanoramaRenderer : MonoBehaviour
    {
        const int LongitudeSegments = 64;
        const int LatitudeSegments = 32;
        const float Radius = 2.8f;

        Mesh _mesh;
        Material _material;
        MeshRenderer _meshRenderer;
        Texture2D _dynamicTexture;
        UnityWebRequest _request;
        Coroutine _load;
        Action<PanoramaRenderResult> _completed;
        Transform _viewer;
        PanoramaSource _source;
        string _shaderName = "<not-resolved>";
        int _generation;
        bool _released;

        public bool IsReady { get; private set; }

        public void Begin(PanoramaSource source, float yawDegrees, Transform viewer, Action<PanoramaRenderResult> completed)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (_completed != null || _released) throw new InvalidOperationException("The panorama renderer has already been used.");

            _completed = completed;
            _viewer = viewer;
            _source = source;
            transform.position = _viewer.position;
            SetYaw(yawDegrees);
            bool surfaceBuilt;
            try
            {
                surfaceBuilt = TryBuildSurface(out _shaderName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ReportFailure(PanoramaRenderFailure.RenderFailed, exception.Message);
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.RenderFailed));
                return;
            }

            if (!surfaceBuilt)
            {
                ReportFailure(PanoramaRenderFailure.RenderFailed, "No compatible unlit shader was found.");
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.RenderFailed));
                return;
            }

            switch (source.Kind)
            {
                case PanoramaSourceKind.Texture when source.Texture != null:
                    try
                    {
                        ApplyTexture(source.Texture);
                        Complete(PanoramaRenderResult.Success());
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                        ReportFailure(PanoramaRenderFailure.RenderFailed, exception.Message);
                        Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.RenderFailed));
                    }
                    break;
                case PanoramaSourceKind.StreamingAssetsPath when !string.IsNullOrWhiteSpace(source.StreamingAssetsPath):
                    var generation = ++_generation;
                    _load = StartCoroutine(LoadStreamingTexture(source.StreamingAssetsPath, generation));
                    break;
                default:
                    ReportFailure(PanoramaRenderFailure.UnsupportedResource, "The configured panorama source is unavailable.");
                    Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.UnsupportedResource));
                    break;
            }
        }

        public void SetYaw(float yawDegrees) => transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        void LateUpdate()
        {
            if (!_released && _viewer != null) transform.position = _viewer.position;
        }

        public void Release()
        {
            if (_released) return;
            _released = true;
            ++_generation;
            _completed = null;
            _viewer = null;
            if (_load != null)
            {
                StopCoroutine(_load);
                _load = null;
            }
            if (_request != null)
            {
                if (!_request.isDone) _request.Abort();
                _request.Dispose();
                _request = null;
            }
            if (_material != null)
            {
                _material.mainTexture = null;
                Destroy(_material);
                _material = null;
            }
            if (_dynamicTexture != null)
            {
                Destroy(_dynamicTexture);
                _dynamicTexture = null;
            }
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
            _meshRenderer = null;
            _source = null;
            _shaderName = "<released>";
            IsReady = false;
        }

        void OnDestroy() => Release();

        IEnumerator LoadStreamingTexture(string relativePath, int generation)
        {
            UnityWebRequestAsyncOperation operation;
            try
            {
                var sourceUrl = BuildStreamingUrl(relativePath);
                _request = UnityWebRequestTexture.GetTexture(sourceUrl, true);
                operation = _request.SendWebRequest();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ReportFailure(PanoramaRenderFailure.LoadFailed, exception.Message);
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.LoadFailed));
                yield break;
            }

            yield return operation;

            if (_released || generation != _generation) yield break;
            if (_request.result != UnityWebRequest.Result.Success)
            {
                ReportFailure(PanoramaRenderFailure.LoadFailed, _request.error ?? "UnityWebRequest failed.");
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.LoadFailed));
                DisposeRequest();
                yield break;
            }

            try
            {
                _dynamicTexture = DownloadHandlerTexture.GetContent(_request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ReportFailure(PanoramaRenderFailure.LoadFailed, exception.Message);
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.LoadFailed));
                DisposeRequest();
                yield break;
            }
            DisposeRequest();
            if (_dynamicTexture == null)
            {
                ReportFailure(PanoramaRenderFailure.LoadFailed, "Texture download completed without a texture.");
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.LoadFailed));
                yield break;
            }

            try
            {
                ApplyTexture(_dynamicTexture);
                Complete(PanoramaRenderResult.Success());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ReportFailure(PanoramaRenderFailure.RenderFailed, exception.Message);
                Complete(PanoramaRenderResult.Fail(PanoramaRenderFailure.RenderFailed));
            }
        }

        bool TryBuildSurface(out string shaderName)
        {
            // Resources keeps this shader in Quest builds. UVs are sampled from each eye's
            // view direction, so a mono panorama has no artificial 2.8 m spherical depth.
            var shader = Resources.Load<Shader>("PanoramaEquirectangular");
            shaderName = shader != null ? shader.name : "<missing>";
            if (shader == null) return false;

            _mesh = CreateInsideSphere(Radius);
            _material = new Material(shader) { name = "PanoramaRuntimeMaterial" };
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.enabled = false;
            return true;
        }

        void ApplyTexture(Texture texture)
        {
            _material.mainTexture = texture;
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", texture);
            if (_meshRenderer != null) _meshRenderer.enabled = true;
        }

        void Complete(PanoramaRenderResult result)
        {
            IsReady = result.Succeeded;
            var callback = _completed;
            _completed = null;
            callback?.Invoke(result);
        }

        void DisposeRequest()
        {
            _request?.Dispose();
            _request = null;
            _load = null;
        }

        void ReportFailure(PanoramaRenderFailure failure, string detail)
        {
            var source = _source;
            var sourceKind = source != null ? source.Kind.ToString() : "<missing>";
            var texture = source?.Texture != null ? source.Texture.name : "<none>";
            var streamingPath = source?.StreamingAssetsPath ?? "<none>";
            Debug.LogError(
                $"PanoramaRenderer failure={failure}; sourceKind={sourceKind}; texture={texture}; " +
                $"streamingAssetsPath={streamingPath}; shader={_shaderName}; detail={detail}",
                this);
        }

        static string BuildStreamingUrl(string relativePath)
        {
            var combined = Path.Combine(Application.streamingAssetsPath, relativePath).Replace('\\', '/');
            if (combined.Contains("://") || combined.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
                return combined;
            return new Uri(Path.GetFullPath(combined)).AbsoluteUri;
        }

        static Mesh CreateInsideSphere(float radius)
        {
            var vertices = new Vector3[(LongitudeSegments + 1) * (LatitudeSegments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[LongitudeSegments * LatitudeSegments * 6];
            for (var y = 0; y <= LatitudeSegments; y++)
            {
                var v = y / (float)LatitudeSegments;
                var theta = Mathf.PI * v;
                for (var x = 0; x <= LongitudeSegments; x++)
                {
                    var u = x / (float)LongitudeSegments;
                    var phi = Mathf.Lerp(-Mathf.PI, Mathf.PI, u);
                    var index = y * (LongitudeSegments + 1) + x;
                    vertices[index] = new Vector3(
                        Mathf.Sin(theta) * Mathf.Sin(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Cos(phi)) * radius;
                    uv[index] = new Vector2(u, 1f - v);
                }
            }

            var triangleIndex = 0;
            for (var y = 0; y < LatitudeSegments; y++)
            {
                for (var x = 0; x < LongitudeSegments; x++)
                {
                    var index = y * (LongitudeSegments + 1) + x;
                    triangles[triangleIndex++] = index + 1;
                    triangles[triangleIndex++] = index + LongitudeSegments + 1;
                    triangles[triangleIndex++] = index;
                    triangles[triangleIndex++] = index + LongitudeSegments + 2;
                    triangles[triangleIndex++] = index + LongitudeSegments + 1;
                    triangles[triangleIndex++] = index + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "PanoramaRuntimeMesh",
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
