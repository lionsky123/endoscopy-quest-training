using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    public static class VisitorMapConfiguration
    {
        public static MapDefinition Resolve(TextAsset published)
        {
            if (published == null)
                return null;
            try
            {
                var definition = JsonUtility.FromJson<MapDefinition>(published.text);
                MapDefinitionValidation.Validate(definition);
                return definition;
            }
            catch (System.Exception exception)when (exception is System.ArgumentException)
            {
                Debug.LogWarning($"Map guidance definition unavailable: {exception.Message}");
                return null;
            }
        }
    }
}
