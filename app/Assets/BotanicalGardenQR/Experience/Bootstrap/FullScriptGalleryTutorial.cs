namespace BotanicalGardenQR.Bootstrap
{
    internal enum FullScriptGalleryTutorialStep
    {
        NotStarted,
        MoveToHandle,
        PinchHandle,
        DragRoom,
        ReleaseHandle,
        SelectRoom,
        Completed,
        Skipped
    }

    /// <summary>Progresses only from the matching tracked hand action; it never changes journey tasks.</summary>
    internal sealed class FullScriptGalleryTutorial
    {
        const float RequiredRotationDegrees = 5f;
        float _dragRotationDegrees;
        internal FullScriptGalleryTutorialStep Step { get; private set; }
        internal string Instruction => Step switch
        {
            FullScriptGalleryTutorialStep.MoveToHandle => "把手移到下方亮起的短把手处。",
            FullScriptGalleryTutorialStep.PinchHandle => "拇指和食指靠近，轻轻捏住短把手。",
            FullScriptGalleryTutorialStep.DragRoom => "保持捏住，向左或向右移动手。",
            FullScriptGalleryTutorialStep.ReleaseHandle => "张开拇指和食指，停稳画廊。",
            FullScriptGalleryTutorialStep.SelectRoom => "伸出食指，轻触想去的房间图片。",
            FullScriptGalleryTutorialStep.Completed => "可以继续选择房间，也可回看已到访房间。",
            FullScriptGalleryTutorialStep.Skipped => "轻触两侧换图，轻触图片进入。",
            _ => string.Empty
        };

        internal void Begin()
        {
            if (Step == FullScriptGalleryTutorialStep.NotStarted)
                Step = FullScriptGalleryTutorialStep.MoveToHandle;
        }

        internal void ObserveHandle(bool tracked, bool nearHandle, bool pinching)
        {
            if (!tracked)
            {
                OnTrackingLost();
                return;
            }

            if (Step == FullScriptGalleryTutorialStep.MoveToHandle)
            {
                if (!nearHandle) return;
                Step = FullScriptGalleryTutorialStep.PinchHandle;
                if (pinching) EnterDragStep();
                return;
            }

            if (Step != FullScriptGalleryTutorialStep.PinchHandle) return;
            if (!nearHandle) Step = FullScriptGalleryTutorialStep.MoveToHandle;
            else if (pinching) EnterDragStep();
        }

        internal void ObserveMovement(bool tracked, bool pinching, float rotationDegrees)
        {
            if (Step != FullScriptGalleryTutorialStep.DragRoom) return;
            if (!tracked)
            {
                OnTrackingLost();
                return;
            }
            if (!pinching)
            {
                ObserveRelease(tracked: true, pinching: false);
                return;
            }
            if (!float.IsNaN(rotationDegrees) && !float.IsInfinity(rotationDegrees))
                _dragRotationDegrees += System.Math.Abs(rotationDegrees);
            if (_dragRotationDegrees >= RequiredRotationDegrees)
                Step = FullScriptGalleryTutorialStep.ReleaseHandle;
        }

        internal void ObserveRelease(bool tracked, bool pinching)
        {
            if (Step == FullScriptGalleryTutorialStep.DragRoom)
            {
                if (!tracked)
                {
                    OnTrackingLost();
                    return;
                }
                if (!pinching) Step = FullScriptGalleryTutorialStep.PinchHandle;
                return;
            }
            if (Step != FullScriptGalleryTutorialStep.ReleaseHandle) return;
            if (!tracked)
            {
                OnTrackingLost();
                return;
            }
            if (!pinching) Step = FullScriptGalleryTutorialStep.SelectRoom;
        }

        internal bool ObserveSelection(bool freshPoke, bool dragging)
        {
            if (Step != FullScriptGalleryTutorialStep.SelectRoom || !freshPoke || dragging) return false;
            Step = FullScriptGalleryTutorialStep.Completed;
            return true;
        }

        internal void OnTrackingLost()
        {
            if (Step == FullScriptGalleryTutorialStep.PinchHandle ||
                Step == FullScriptGalleryTutorialStep.DragRoom ||
                Step == FullScriptGalleryTutorialStep.ReleaseHandle)
                Step = FullScriptGalleryTutorialStep.MoveToHandle;
        }

        internal void Skip()
        {
            if (Step != FullScriptGalleryTutorialStep.Completed)
                Step = FullScriptGalleryTutorialStep.Skipped;
        }

        internal void Replay() => Step = FullScriptGalleryTutorialStep.MoveToHandle;

        void EnterDragStep()
        {
            _dragRotationDegrees = 0;
            Step = FullScriptGalleryTutorialStep.DragRoom;
        }
    }
}
