using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal sealed class StaticModelDriver : IModelDriver
    {
        public bool CanPlayAnimation => false;
        public bool IsAnimationPlaying => false;
        public ModelResult Attach(GameObject instance) => ModelResult.Success();
        public ModelResult PlayAnimation() => ModelResult.Failure(ModelFailureCode.UnsupportedAnimation, "model.animation.not_configured");
        public bool Tick(float deltaTime) => false;
        public void StopAnimation() { }
        public void Dispose() { }
    }
}
