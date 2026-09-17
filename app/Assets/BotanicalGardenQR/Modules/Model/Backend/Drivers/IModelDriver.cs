using System;
using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal interface IModelDriver : IDisposable
    {
        bool CanPlayAnimation { get; }
        bool IsAnimationPlaying { get; }
        ModelResult Attach(GameObject instance);
        ModelResult PlayAnimation();
        bool Tick(float deltaTime);
        void StopAnimation();
    }
}
