using System;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal interface IModelLoader
    {
        Task<ModelLoadResult> LoadAsync(ModelSource source, Transform parent, CancellationToken cancellationToken);
    }

    internal sealed class ModelLoadResult : IDisposable
    {
        readonly IDisposable _resource;

        ModelLoadResult(GameObject instance, IDisposable resource, ModelFailureCode failureCode, string diagnosticTag)
        {
            Instance = instance;
            _resource = resource;
            FailureCode = failureCode;
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public GameObject Instance { get; private set; }
        public ModelFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }
        public bool Succeeded => Instance != null && FailureCode == ModelFailureCode.None;

        public static ModelLoadResult Success(GameObject instance, IDisposable resource = null)
            => new ModelLoadResult(instance, resource, ModelFailureCode.None, string.Empty);

        public static ModelLoadResult Failure(ModelFailureCode code, string diagnosticTag)
            => new ModelLoadResult(null, null, code, diagnosticTag);

        public void Dispose()
        {
            _resource?.Dispose();
            if (Instance != null)
                UnityEngine.Object.Destroy(Instance);
            Instance = null;
        }
    }
}
