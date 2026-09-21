using System;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    public static class ClinicalJourneyConfiguration
    {
        public static ClinicalJourneyDefinition Load()
        {
            return Resolve(Resources.Load<TextAsset>("ClinicalCourse/clinical-journey"));
        }

        public static ClinicalJourneyDefinition Resolve(TextAsset published)
        {
            if (published == null) return null;
            try
            {
                var definition = JsonUtility.FromJson<ClinicalJourneyDefinition>(published.text);
                if (definition == null) throw new ArgumentException("The clinical journey JSON is empty.");
                definition.Validate();
                return definition;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                Debug.LogWarning($"Clinical journey definition unavailable: {exception.Message}");
                return null;
            }
        }
    }
}
