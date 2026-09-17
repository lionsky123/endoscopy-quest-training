using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    public static class ModelModuleFactory
    {
        public static IModelController Create(Transform runtimeRoot, Action<DiagnosticEvent> diagnostics = null)
        {
            if (runtimeRoot == null) throw new System.ArgumentNullException(nameof(runtimeRoot));
            var controller = runtimeRoot.gameObject.AddComponent<ModelController>();
            try
            {
                controller.Initialize(new ModelImplementationSelector(), diagnostics);
                return controller;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(controller);
                else UnityEngine.Object.DestroyImmediate(controller);
                throw;
            }
        }
    }
}
