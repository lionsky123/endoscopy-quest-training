using System;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    public static class ContentScenePublishCli
    {
        public static void Run()
        {
            var result = ContentScenePublisher.PublishContentLibrary();
            if (!result.Succeeded)
                throw new InvalidOperationException($"Content publish failed: {result.Validation.Format()}");

            Debug.Log(result.Status == ContentScenePublishStatus.UpToDate
                ? $"Content scene library is already up to date: {result.LibraryPath}"
                : $"Published content scene library: {result.LibraryPath}");
        }
    }
}
