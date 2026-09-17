using System;
using BotanicalGardenQR.ApplicationMode.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BotanicalGardenQR.ApplicationMode.Adapters
{
    sealed class UnitySceneTransitionAdapter : IApplicationModeSceneLoader
    {
        readonly ApplicationModeOptionsAsset _options;

        public UnitySceneTransitionAdapter(ApplicationModeOptionsAsset options)
        {
            _options = options ? options : throw new ArgumentNullException(nameof(options));
        }

        public void Load(
            ApplicationModeRole targetRole,
            Action<ApplicationModeLoadResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (!_options.TryGetBuildIndex(targetRole, out var buildIndex) ||
                buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError("[ApplicationMode] MODE_SCENE_INDEX_INVALID");
                completed(ApplicationModeLoadResult.Failed);
                return;
            }

            try
            {
                var operation = SceneManager.LoadSceneAsync(
                    buildIndex,
                    LoadSceneMode.Single);
                if (operation == null)
                {
                    Debug.LogError("[ApplicationMode] MODE_SCENE_LOAD_NOT_STARTED");
                    completed(ApplicationModeLoadResult.Failed);
                    return;
                }

                operation.completed += _ => completed(ApplicationModeLoadResult.Success);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ApplicationMode] MODE_SCENE_LOAD_FAILED {exception.GetType().Name}");
                completed(ApplicationModeLoadResult.Failed);
            }
        }
    }
}
