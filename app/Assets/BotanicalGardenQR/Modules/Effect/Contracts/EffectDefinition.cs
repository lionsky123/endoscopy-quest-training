using System;
using UnityEngine;
namespace BotanicalGardenQR.Effect.Contracts
{
    public enum EffectTriggerPolicy { OnSessionOpen, OnCommand }
    public sealed class EffectDefinition
    {
        public EffectDefinition(GameObject prefab,EffectTriggerPolicy triggerPolicy,float scale=1f)
        { Prefab=prefab!=null?prefab:throw new ArgumentNullException(nameof(prefab)); if(!(scale>0)||float.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale)); TriggerPolicy=triggerPolicy; Scale=scale; }
        public GameObject Prefab { get; }
        public EffectTriggerPolicy TriggerPolicy { get; }
        public float Scale { get; }
    }
}
