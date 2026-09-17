using BotanicalGardenQR.Video.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Video.Backend
{
    public static class VideoModuleFactory
    {
        public static IVideoController Create(Transform runtimeRoot, VideoRuntimeOptions options)
        {
            if (runtimeRoot == null) throw new System.ArgumentNullException(nameof(runtimeRoot));
            if (options == null) throw new System.ArgumentNullException(nameof(options));
            var controller = runtimeRoot.gameObject.AddComponent<VideoController>();
            controller.Initialize(options);
            return controller;
        }
    }
}
