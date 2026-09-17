using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public enum ShellFlowAction
    {
        EnterFeature = 1,
        BackToMain = 2,
        Close = 3,
        CompleteObservation = 4,
        FeaturePageAction = 5,
        FeaturePageActionExitFocus = 6
    }

    public sealed class ShellFlowActionTarget : MonoBehaviour
    {
        [SerializeField] ShellFlowAction _action;
        [SerializeField] FeaturePageId _feature;
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _label;
        [SerializeField] Text _legacyLabel;

        System.Action<ShellFlowActionTarget> _selected;

        public ShellFlowAction Action => _action;
        public FeaturePageId Feature => _feature;
        internal RectTransform HitRect => _button != null ? _button.transform as RectTransform : null;
        internal bool IsInteractable => _button != null && _button.IsActive() && _button.IsInteractable();
        internal bool HasFeaturePagePresentation =>
            _button != null && (_label != null ^ _legacyLabel != null);

        internal void ApplyFeaturePageAction(bool interactable, string label)
        {
            if (_action != ShellFlowAction.FeaturePageAction &&
                _action != ShellFlowAction.FeaturePageActionExitFocus)
                throw new System.InvalidOperationException(
                    "Only feature-page semantic action targets accept dynamic state.");
            if (!HasFeaturePagePresentation)
                throw new System.InvalidOperationException("The feature-page action target requires a Button and label.");
            _button.interactable = interactable;
            if (_label != null)
                _label.text = label ?? string.Empty;
            else
                _legacyLabel.text = label ?? string.Empty;
        }

        internal void Bind(System.Action<ShellFlowActionTarget> selected)
        {
            Unbind();
            _selected = selected ?? throw new System.ArgumentNullException(nameof(selected));
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        internal void Unbind()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
            _selected = null;
        }

        public FlowIntent CreateIntent(SessionToken session)
        {
            switch (_action)
            {
                case ShellFlowAction.EnterFeature: return FlowIntent.EnterFeature(session, _feature);
                case ShellFlowAction.BackToMain: return FlowIntent.BackToMain(session);
                case ShellFlowAction.Close: return FlowIntent.Close(session);
                case ShellFlowAction.CompleteObservation:
                    throw new System.InvalidOperationException("Observation completion is a semantic request, not a Flow intent.");
                case ShellFlowAction.FeaturePageAction:
                    throw new System.InvalidOperationException("The feature-page action is a semantic request, not a Flow intent.");
                case ShellFlowAction.FeaturePageActionExitFocus:
                    throw new System.InvalidOperationException(
                        "The feature-page focus exit is a semantic request, not a Flow intent.");
                default: throw new System.InvalidOperationException("Unsupported shell flow action.");
            }
        }

        void HandleClick() => _selected?.Invoke(this);
        void OnDestroy() => Unbind();
    }
}
