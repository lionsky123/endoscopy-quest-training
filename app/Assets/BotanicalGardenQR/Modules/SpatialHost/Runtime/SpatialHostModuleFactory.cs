using System;
using System.Threading;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    public static class SpatialHostModuleFactory
    {
        public static ISpatialDisplayHost Create(Transform viewer, Transform displayRoot)
        {
            if (viewer == null)
                throw new ArgumentNullException(nameof(viewer));
            if (displayRoot == null)
                throw new ArgumentNullException(nameof(displayRoot));

            return new SpatialDisplayHost(
                viewer,
                displayRoot,
                Thread.CurrentThread.ManagedThreadId);
        }

    }
}
