using BotanicalGardenQR.ApplicationMode.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.ApplicationMode.Adapters
{
    public sealed class MetaLeftMenuHoldAdapter : MonoBehaviour
    {
        [SerializeField] MonoBehaviour _controllerSource;

        IApplicationModeController _controller;

        void Awake()
        {
            _controller = _controllerSource as IApplicationModeController;
            if (_controller == null)
                Debug.LogError(
                    "[ApplicationMode] MODE_INPUT_CONTROLLER_MISSING",
                    this);
        }

        void Update()
        {
            if (_controller == null) return;
            var pressed = OVRInput.Get(
                OVRInput.Button.Start,
                OVRInput.Controller.LTouch);
            _controller.Advance(Time.unscaledDeltaTime, pressed);
        }

        void OnDisable()
        {
            _controller?.Advance(0f, false);
        }
    }
}
