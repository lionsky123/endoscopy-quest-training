using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal sealed class PrefabModelLoader : IModelLoader
    {
        public Task<ModelLoadResult> LoadAsync(ModelSource source, Transform parent, CancellationToken cancellationToken)
        {
            if (!(source.Asset is GameObject prefab))
                return Task.FromResult(ModelLoadResult.Failure(ModelFailureCode.UnsupportedSource, "model.prefab.asset_invalid"));
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<ModelLoadResult>(cancellationToken);

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "ModelRuntime";
            return Task.FromResult(ModelLoadResult.Success(instance));
        }
    }
}
