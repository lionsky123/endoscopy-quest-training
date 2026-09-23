using System;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [Serializable]
    public sealed class VisitorPrologueCopyRecord
    {
        [SerializeField] string _invitationTitle;
        [SerializeField, TextArea] string _invitationDetail;
        [SerializeField] string _gazeInvitationAction;
        [SerializeField] string _arrivalTitle;
        [SerializeField, TextArea] string _arrivalDetail;
        [SerializeField] string _fairySpeakerName;
        [SerializeField, TextArea] string[] _encounterPages = Array.Empty<string>();
        [SerializeField] string _curiousReplyLabel;
        [SerializeField] string _companionReplyLabel;
        [SerializeField, TextArea] string _curiousResponse;
        [SerializeField, TextArea] string _companionResponse;
        [SerializeField] string _retryAction;
        public string InvitationTitle => _invitationTitle;
        public string InvitationDetail => _invitationDetail;
        public string GazeInvitationAction => _gazeInvitationAction;
        public string ArrivalTitle => _arrivalTitle;
        public string ArrivalDetail => _arrivalDetail;
        public string FairySpeakerName => _fairySpeakerName;
        public string CuriousReplyLabel => _curiousReplyLabel;
        public string CompanionReplyLabel => _companionReplyLabel;
        public string CuriousResponse => _curiousResponse;
        public string CompanionResponse => _companionResponse;
        [SerializeField] string _firstContinueAction = "凝视回应  ›";
        public string FirstContinueAction => _firstContinueAction;
        public string RetryAction => _retryAction;
        public int EncounterPageCount => _encounterPages?.Length ?? 0;
        internal void UseStationaryCopy()
        {
            _encounterPages[3]="请留在原位，用真实手部近触操作。检查点和房间都由你主动选择，画面淡出后切换，无需跟随或走到门口。现在选择学习模式。";
            _invitationDetail="请坐稳或站稳，留出双手活动空间。将手掌靠近学习册封面印记，稍停片刻。";
        }
        public bool TryGetEncounterPage(int index, out string text)
        {
            text = _encounterPages != null && index >= 0 && index < _encounterPages.Length ? _encounterPages[index] : string.Empty;
            return !string.IsNullOrWhiteSpace(text);
        }
        public bool IsValid(out string error)
        {
            foreach (var text in new[] { _invitationTitle, _invitationDetail, _gazeInvitationAction,
                _arrivalTitle, _arrivalDetail, _fairySpeakerName, _curiousReplyLabel, _companionReplyLabel,
                _curiousResponse, _companionResponse, _retryAction, _firstContinueAction })
                if (string.IsNullOrWhiteSpace(text)) { error = "Encounter copy is incomplete."; return false; }
            if (EncounterPageCount != 4) { error = "Encounter requires four authored pages with one reply page."; return false; }
            foreach (var page in _encounterPages)
                if (string.IsNullOrWhiteSpace(page)) { error = "Encounter page is empty."; return false; }
            error = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Botanical Garden QR/Visitor Prologue Theme", fileName = "VisitorPrologueTheme")]
    public sealed class VisitorPrologueThemeAsset : ScriptableObject
    {
        [SerializeField] GameObject _presentationPrefab;
        [SerializeField, Range(.45f, .65f)] float _viewerDistance = .45f;
        [SerializeField] float _verticalOffset = -.08f;
        [SerializeField] float _minimumStandingPanelCenterHeight = 1.35f;
        [SerializeField] float _initialPlacementDelaySeconds = .18f;
        [SerializeField] float _gazeFallbackDelaySeconds = 5f;
        [SerializeField] float _palmHoldSeconds = 1.1f;
        [SerializeField] float _palmContactRadius = .13f;
        [SerializeField] float _minimumPalmAlignment = .45f;
        [SerializeField] float _bookOpenSeconds = .85f;
        [SerializeField] AudioClip _invitationAudio;
        [SerializeField] AudioClip _bookAppearAudio;
        [SerializeField] AudioClip _sealContactAudio;
        public AudioClip BookAppearAudio => _bookAppearAudio;
        public AudioClip SealContactAudio => _sealContactAudio;
        [SerializeField] VisitorPrologueCopyRecord _copy = new VisitorPrologueCopyRecord();
        public GameObject PresentationPrefab => _presentationPrefab;
        public float ViewerDistance => _viewerDistance;
        public float VerticalOffset => _verticalOffset;
        public float MinimumStandingPanelCenterHeight => _minimumStandingPanelCenterHeight;
        public float InitialPlacementDelaySeconds => _initialPlacementDelaySeconds;
        public float GazeFallbackDelaySeconds => _gazeFallbackDelaySeconds;
        public float PalmHoldSeconds => _palmHoldSeconds;
        public float PalmContactRadius => _palmContactRadius;
        public float MinimumPalmAlignment => _minimumPalmAlignment;
        public float BookOpenSeconds => _bookOpenSeconds;
        public AudioClip InvitationAudio => _invitationAudio;
        public VisitorPrologueCopyRecord Copy => _copy;
        public VisitorPrologueThemeAsset CreateStationaryVariant()
        {
            var copy=Instantiate(this);
            copy._copy=JsonUtility.FromJson<VisitorPrologueCopyRecord>(JsonUtility.ToJson(_copy));
            copy._copy.UseStationaryCopy();
            copy._minimumStandingPanelCenterHeight=.2f;
            return copy;
        }
        public bool IsValid(out string error)
        {
            if (_presentationPrefab == null || _invitationAudio == null)
            { error = "Invitation requires its presentation and local feedback audio."; return false; }
            if (!Positive(_viewerDistance) || _viewerDistance < .45f || _viewerDistance > .65f ||
                !Positive(_initialPlacementDelaySeconds) || !Positive(_gazeFallbackDelaySeconds) ||
                !Positive(_minimumStandingPanelCenterHeight) || !Positive(_palmHoldSeconds) ||
                !Positive(_palmContactRadius) || _palmContactRadius > .2f ||
                !Positive(_minimumPalmAlignment) || _minimumPalmAlignment > 1f || !Positive(_bookOpenSeconds) ||
                float.IsNaN(_verticalOffset) || float.IsInfinity(_verticalOffset))
            { error = "Invitation placement or timing is invalid."; return false; }
            if (_copy == null) { error = "Invitation copy is missing."; return false; }
            return _copy.IsValid(out error);
        }
        static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
