using System;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using UnityEngine;

namespace BotanicalGardenQR.Effect.Backend
{
    public static class EffectModuleFactory
    {
        public static IEffectController Create(Transform runtimeRoot, Action<DiagnosticEvent> diagnostics = null)
        {
            if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
            var moduleRoot = new GameObject("EffectModuleRuntime");
            moduleRoot.transform.SetParent(runtimeRoot, false);
            var controller = moduleRoot.AddComponent<EffectController>();
            controller.Initialize(diagnostics);
            return controller;
        }

        public static IDisposable BindToFlow(
            IExperienceFlow flow,
            IEffectDefinitionSource definitions,
            IEffectController controller)
            => new EffectSessionBinding(flow, definitions, controller);
    }
}
