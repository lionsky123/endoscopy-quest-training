using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Fairy.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    public static class FairyModuleFactory
    {
        public static IFairyController Create(
            Transform runtimeRoot,
            Transform viewer,
            Transform groundReference,
            OVRPassthroughLayer arrivalPassthroughLayer,
            Light arrivalEnvironmentLight,
            Action<DiagnosticEvent> diagnostics = null,
            Func<Vector3, Vector3?> arrivalHandPosition = null,
            IFairyWalkSpace walkSpace = null)
        {
            if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (groundReference == null) throw new ArgumentNullException(nameof(groundReference));
            // A virtual room has no passthrough layer; arrival still animates its light.
            if (arrivalEnvironmentLight == null) throw new ArgumentNullException(nameof(arrivalEnvironmentLight));

            var moduleRoot = new GameObject("FairyModuleRuntime");
            moduleRoot.transform.SetParent(runtimeRoot, false);
            var controller = moduleRoot.AddComponent<FairyController>();
            controller.Initialize(
                viewer,
                groundReference,
                arrivalPassthroughLayer,
                arrivalEnvironmentLight,
                diagnostics,
                arrivalHandPosition, walkSpace);
            return controller;
        }

        public static FairyCompanionBinding BindAsCompanion(
            IFairyController controller,
            FairyDefinition definition,
            bool initiallyVisible = true)
            => new FairyCompanionBinding(controller, definition, initiallyVisible);
    }
}
