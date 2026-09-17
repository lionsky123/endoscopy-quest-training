using System;
using BotanicalGardenQR.Narration.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Narration.Backend
{
    public static class NarrationModuleFactory
    {
        public static INarrationController Create(Transform runtimeRoot)
        {
            if (runtimeRoot == null)
                throw new ArgumentNullException(nameof(runtimeRoot));

            return runtimeRoot.gameObject.AddComponent<NarrationController>();
        }
    }
}
