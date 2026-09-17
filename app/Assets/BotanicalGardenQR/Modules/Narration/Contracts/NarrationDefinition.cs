using System;
using UnityEngine;
namespace BotanicalGardenQR.Narration.Contracts
{
    public sealed class NarrationDefinition
    {
        public NarrationDefinition(AudioClip clip,NarrationStartPolicy startPolicy=NarrationStartPolicy.OnVisitorCommand) { Clip=clip!=null?clip:throw new ArgumentNullException(nameof(clip)); StartPolicy=startPolicy; }
        public AudioClip Clip { get; }
        public NarrationStartPolicy StartPolicy { get; }
    }
}
