using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Model.Contracts;
using GLTFast;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal sealed class GlbModelLoader : IModelLoader
    {
        public async Task<ModelLoadResult> LoadAsync(ModelSource source, Transform parent, CancellationToken cancellationToken)
        {
            if (source.Asset != null)
            {
                if (!(source.Asset is GameObject importedAsset))
                    return ModelLoadResult.Failure(ModelFailureCode.UnsupportedSource, "model.glb.asset_type_unsupported");
                var importedInstance = UnityEngine.Object.Instantiate(importedAsset, parent, false);
                importedInstance.name = "ModelRuntime";
                return ModelLoadResult.Success(importedInstance);
            }

            if (string.IsNullOrWhiteSpace(source.StreamingAssetsPath))
                return ModelLoadResult.Failure(ModelFailureCode.InvalidDefinition, "model.glb.path_missing");

            var root = new GameObject("ModelRuntime");
            root.transform.SetParent(parent, false);
            var import = new GltfImport();
            try
            {
                var absolutePath = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + source.StreamingAssetsPath;
                var uri = ToUri(absolutePath);
                if (!await import.Load(uri, null, cancellationToken))
                {
                    import.Dispose();
                    UnityEngine.Object.Destroy(root);
                    return ModelLoadResult.Failure(ModelFailureCode.LoadFailed, "model.glb.load_failed");
                }
                if (!await import.InstantiateMainSceneAsync(root.transform, cancellationToken))
                {
                    import.Dispose();
                    UnityEngine.Object.Destroy(root);
                    return ModelLoadResult.Failure(ModelFailureCode.LoadFailed, "model.glb.instantiate_failed");
                }
                return ModelLoadResult.Success(root, import);
            }
            catch (OperationCanceledException)
            {
                import.Dispose();
                UnityEngine.Object.Destroy(root);
                throw;
            }
            catch (Exception)
            {
                import.Dispose();
                UnityEngine.Object.Destroy(root);
                return ModelLoadResult.Failure(ModelFailureCode.LoadFailed, "model.glb.exception");
            }
        }

        static Uri ToUri(string pathOrUrl)
        {
            if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absoluteUri)
                && (absoluteUri.IsFile
                    || absoluteUri.Scheme == Uri.UriSchemeHttp
                    || absoluteUri.Scheme == Uri.UriSchemeHttps
                    || absoluteUri.Scheme == "jar"))
                return absoluteUri;
            return new Uri(Path.GetFullPath(pathOrUrl));
        }
    }
}
