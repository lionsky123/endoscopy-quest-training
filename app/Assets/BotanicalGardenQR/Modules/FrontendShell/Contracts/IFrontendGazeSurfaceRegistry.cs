using System;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public interface IFrontendGazeSurfaceRegistration : IDisposable
    {
        bool IsFocused { get; }
        void Invalidate();
    }

    public interface IFrontendGazeProgressPresenter
    {
        void PresentGazeProgress(float progress);
    }

    public interface IFrontendGazeSurfaceRegistry
    {
        IDisposable SuspendPanelInput();

        IFrontendGazeSurfaceRegistration RegisterGazeSurface(
            Transform surfaceRoot,
            int priority,
            string label);
    }
}
