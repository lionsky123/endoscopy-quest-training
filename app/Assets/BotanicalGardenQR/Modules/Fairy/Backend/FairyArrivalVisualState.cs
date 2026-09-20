using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>
    /// Scopes all temporary rendering changes during the first crossing.
    /// Temporarily grades supported Passthrough modes; restores the exact captured controls on every exit.
    /// </summary>
    internal sealed class FairyArrivalVisualState
    {
        readonly OVRPassthroughLayer _passthrough;
        readonly Light _light;
        bool _edgeEnabled;
        Color _edgeColor, _lightColor;
        float _lightIntensity;
        bool _premultipliedAlphaEnabled;
        bool _captured;
        OVRPassthroughLayer.ColorMapEditorType _mapType;
        float _brightness, _contrast, _saturation, _posterize;
        Gradient _gradient;
        bool _canGrade;
        internal float WorldShift { get; private set; }

        internal FairyArrivalVisualState(OVRPassthroughLayer passthroughLayer, Light environmentLight)
        {
            if (environmentLight == null) throw new ArgumentNullException(nameof(environmentLight));
            _passthrough = passthroughLayer;
            _light = environmentLight;
        }

        internal void Capture()
        {
            if (_captured)
                throw new InvalidOperationException("Fairy arrival render state is already captured.");
            if (_passthrough != null)
            {
                _premultipliedAlphaEnabled = OVRManager.eyeFovPremultipliedAlphaModeEnabled;
                _edgeEnabled = _passthrough.edgeRenderingEnabled;
                _edgeColor = _passthrough.edgeColor;
                _mapType = _passthrough.colorMapEditorType;
                _brightness = _passthrough.colorMapEditorBrightness;
                _contrast = _passthrough.colorMapEditorContrast;
                _saturation = _passthrough.colorMapEditorSaturation;
                _posterize = _passthrough.colorMapEditorPosterize;
                _gradient = _passthrough.colorMapEditorGradient;
                _canGrade = _mapType == OVRPassthroughLayer.ColorMapEditorType.None ||
                    _mapType == OVRPassthroughLayer.ColorMapEditorType.ColorAdjustment ||
                    _mapType == OVRPassthroughLayer.ColorMapEditorType.Grayscale ||
                    _mapType == OVRPassthroughLayer.ColorMapEditorType.GrayscaleToColor;
            }
            _lightColor = _light.color;
            _lightIntensity = _light.intensity;
            _captured = true;
            if (_passthrough != null) OVRManager.eyeFovPremultipliedAlphaModeEnabled = false;
        }

        internal void SetWorldShift(float amount)
        {
            if (!_captured) return;
            WorldShift = Mathf.Clamp01(amount);
            if (_passthrough == null || !_canGrade) return;
            var brightness = Mathf.Clamp(_brightness - .28f * WorldShift, -1, 1);
            var contrast = Mathf.Clamp(_contrast + .18f * WorldShift, -1, 1);
            if (_mapType == OVRPassthroughLayer.ColorMapEditorType.Grayscale || _mapType == OVRPassthroughLayer.ColorMapEditorType.GrayscaleToColor)
                _passthrough.SetColorMapControls(contrast, brightness, _posterize, _gradient, _mapType);
            else
                _passthrough.SetBrightnessContrastSaturation(brightness, contrast, Mathf.Clamp(_saturation - .55f * WorldShift, -1, 1));
        }

        internal void SetPressure(float amount)
        {
            if (!_captured) return;
            amount = Mathf.Clamp01(amount);
            if (_passthrough != null)
            {
                _passthrough.edgeRenderingEnabled = amount > .01f || _edgeEnabled;
                _passthrough.edgeColor = Color.Lerp(_edgeEnabled ? _edgeColor : Color.clear,
                    new Color(.28f, .62f, .65f, .16f), amount);
            }
            if (_light != null)
            {
                _light.color = Color.Lerp(_lightColor, new Color(.48f, .76f, .83f), amount * .45f);
                _light.intensity = _lightIntensity * Mathf.Lerp(1f, .58f, Mathf.Max(amount, WorldShift));
            }
        }

        internal void Restore()
        {
            if (!_captured) return;
            if (_passthrough != null) OVRManager.eyeFovPremultipliedAlphaModeEnabled = _premultipliedAlphaEnabled;
            if (_passthrough != null)
            {
                if (_canGrade)
                {
                    if (_mapType == OVRPassthroughLayer.ColorMapEditorType.Grayscale || _mapType == OVRPassthroughLayer.ColorMapEditorType.GrayscaleToColor)
                        _passthrough.SetColorMapControls(_contrast, _brightness, _posterize, _gradient, _mapType);
                    else
                    {
                        _passthrough.SetBrightnessContrastSaturation(_brightness, _contrast, _saturation);
                        if (_mapType == OVRPassthroughLayer.ColorMapEditorType.None) _passthrough.DisableColorMap();
                    }
                }
                _passthrough.edgeRenderingEnabled = _edgeEnabled;
                _passthrough.edgeColor = _edgeColor;
            }
            if (_light != null) { _light.color = _lightColor; _light.intensity = _lightIntensity; }
            _captured = false;
            WorldShift = 0;
        }
    }
}
