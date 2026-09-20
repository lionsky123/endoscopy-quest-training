using System;
using System.Collections.Generic;
using BotanicalGardenQR.VisitorCoach.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [Serializable]
    public sealed class VisitorCoachCueRecord
    {
        [SerializeField] string _cueKey;
        [SerializeField] bool _globalOverlay;
        [SerializeField, TextArea(1, 3)] string[] _dialoguePages = Array.Empty<string>();
        [SerializeField] AudioClip[] _dialogueAudio = Array.Empty<AudioClip>();
        [SerializeField, TextArea(1, 3)] string _initial;
        [SerializeField, TextArea(1, 3)] string _direct;
        [SerializeField, TextArea(1, 3)] string _demonstration;
        [SerializeField, TextArea(1, 3)] string _recovery;

        public string CueKey => _cueKey?.Trim() ?? string.Empty;
        public bool GlobalOverlay => _globalOverlay;
        public int DialoguePageCount => _dialoguePages?.Length ?? 0;
        public bool HasExplicitRecovery => !string.IsNullOrWhiteSpace(_recovery);

        public bool TryGetDialoguePage(int pageIndex, out string text)
        {
            text = string.Empty;
            if (_dialoguePages == null || pageIndex < 0 || pageIndex >= _dialoguePages.Length)
                return false;
            text = _dialoguePages[pageIndex]?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(text);
        }

        public bool TryGetOptionalDialogueAudio(int pageIndex, out AudioClip clip)
        {
            clip = null;
            if (_dialogueAudio == null || pageIndex < 0 || pageIndex >= _dialogueAudio.Length)
                return false;
            clip = _dialogueAudio[pageIndex];
            return clip != null;
        }

        public bool TryResolve(VisitorCoachHintLevel level, out string copy)
        {
            copy = level switch
            {
                VisitorCoachHintLevel.Initial => FirstNonEmpty(_initial),
                VisitorCoachHintLevel.Direct => FirstNonEmpty(_direct, _initial),
                VisitorCoachHintLevel.Demonstration => FirstNonEmpty(_demonstration, _direct, _initial),
                VisitorCoachHintLevel.Recovery => FirstNonEmpty(_recovery, _demonstration, _direct, _initial),
                _ => string.Empty
            };
            return !string.IsNullOrWhiteSpace(copy);
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(CueKey))
            {
                error = "Visitor Coach cue key is empty.";
                return false;
            }
            if (!TryResolve(VisitorCoachHintLevel.Initial, out _))
            {
                error = $"Visitor Coach cue '{CueKey}' has no initial copy.";
                return false;
            }
            if (_dialoguePages == null || _dialoguePages.Length == 0 || _dialoguePages.Length > 3)
            {
                error = $"Visitor Coach cue '{CueKey}' requires one to three authored dialogue pages.";
                return false;
            }
            for (var index = 0; index < _dialoguePages.Length; index++)
            {
                var page = _dialoguePages[index];
                if (string.IsNullOrWhiteSpace(page) || !string.Equals(page, page.Trim(), StringComparison.Ordinal))
                {
                    error = $"Visitor Coach cue '{CueKey}' page {index + 1} is empty or not trimmed.";
                    return false;
                }
                if (LineCount(page) > 3)
                {
                    error = $"Visitor Coach cue '{CueKey}' page {index + 1} exceeds three authored lines.";
                    return false;
                }
            }
            if (_dialogueAudio != null && _dialogueAudio.Length != 0 &&
                _dialogueAudio.Length != _dialoguePages.Length)
            {
                error = $"Visitor Coach cue '{CueKey}' optional page-audio slots must match its page count.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        static int LineCount(string value)
        {
            var count = 1;
            for (var index = 0; index < value.Length; index++)
                if (value[index] == '\n') count++;
            return count;
        }

        static string FirstNonEmpty(params string[] values)
        {
            for (var index = 0; index < values.Length; index++)
                if (!string.IsNullOrWhiteSpace(values[index]))
                    return values[index].Trim();
            return string.Empty;
        }
    }

    [CreateAssetMenu(menuName = "Botanical Garden QR/Visitor Coach Theme", fileName = "VisitorCoachTheme")]
    public sealed class VisitorCoachThemeAsset : ScriptableObject
    {
        [SerializeField] AudioClip _confirmSound;
        public AudioClip ConfirmSound => _confirmSound;
        [Header("Map guidance")]
        [SerializeField] string _guidanceChapter = "地图引航";
        [SerializeField] string _guidanceDepartureCopy = "本次观察已完成。下一点位：{0}。选择出发后，靠近小精灵，它会继续带路。";
        [SerializeField] string _guidanceDepartureLabel = "前往下一站";
        public string GuidanceDepartureCopy => _guidanceDepartureCopy;
        public string GuidanceDepartureLabel => _guidanceDepartureLabel;
        public string GuidanceChapter => _guidanceChapter;
        [SerializeField] string _guidanceUnavailableCopy = "这一段暂时无法带路。你可以继续使用二维码探索。";
        public string GuidanceUnavailableCopy => _guidanceUnavailableCopy;
        [SerializeField] string _guidanceEndLabel = "继续扫码探索";
        public string GuidanceEndLabel => _guidanceEndLabel;

        [SerializeField] GameObject _presentationPrefab;
        [SerializeField] Sprite _listeningPortrait;
        [SerializeField] Sprite _welcomePortrait;
        [SerializeField] Sprite _wonderPortrait;
        public Sprite ListeningPortrait => _listeningPortrait;
        public Sprite WelcomePortrait => _welcomePortrait;
        public Sprite WonderPortrait => _wonderPortrait;

        [Header("Escalation")]
        [SerializeField, Min(0.1f)] float _directSeconds = 4f;
        [SerializeField, Min(0.2f)] float _demonstrationSeconds = 8f;
        [SerializeField, Min(0.3f)] float _recoverySeconds = 12f;

        [Header("Dialogue presentation")]
        [SerializeField, Min(0.2f)] float _viewerDistance = 0.45f;
        [SerializeField] Vector2 _panelPixels = new Vector2(760f, 132f);
        [SerializeField, Min(0.0001f)] float _canvasScale = 0.001f;
        [SerializeField, Min(8f)] float _detailFontSize = 19f;
        [SerializeField, Min(8f)] float _speakerFontSize = 22f;
        [SerializeField, Min(8f)] float _dialogueFontSize = 28f;
        [SerializeField, Min(0.01f)] float _fadeSeconds = 0.16f;
        [SerializeField, Range(0.05f, 0.5f)] float _confirmDebounceSeconds = 0.18f;
        [SerializeField, Min(0.1f)] float _fairyAttentionSeconds = 3f;
        [SerializeField] float _dialogueVerticalOffset = 0f;
        [SerializeField] Vector2 _fairyDialogueHorizontalOffset = new Vector2(-0.35f, 0.3f);
        [SerializeField] string _fairySpeakerName = "小精灵";
        [SerializeField] Color _panelColor = new Color(0.035f, 0.09f, 0.085f, 0.92f);
        [SerializeField] Color _textColor = new Color(0.92f, 1f, 0.96f, 1f);
        [SerializeField] Color _detailTextColor = new Color(0.72f, 0.86f, 0.8f, 1f);
        [SerializeField] Color _accentColor = new Color(0.2f, 0.88f, 0.62f, 1f);
        [SerializeField] Color _speakerColor = new Color(1f, 0.82f, 0.38f, 1f);

        [Header("Cue copy")]
        [SerializeField] string _toolPreparationReadyCopy = "靠近我，一起去第一站吧。离得远了，我会等你。";
        [SerializeField] string _toolPreparationIntroduction = "见闻册记录发现，卷轴为我们指路。把一只手放在胸前，掌心朝上，稍停片刻——试试看。";
        public string ToolPreparationIntroduction => _toolPreparationIntroduction;
        [SerializeField] string _toolPreparationPracticeCopy = "我在这里等你。\n一只手放在胸前，掌心朝上，稍停片刻。";
        [SerializeField] string _toolPreparationSummonedCopy = "看，它们回应你了！\n先用“收起”送它们回去，我们就可以出发。";
        [SerializeField] string _toolPreparationSuccessCopy = "它们认得你了，以后就这样呼唤它们。";
        [SerializeField] string _toolPreparationDeferredCopy = "没关系，方便时我们再试。";
        public string ToolPreparationPracticeCopy => _toolPreparationPracticeCopy;
        public string ToolPreparationSummonedCopy => _toolPreparationSummonedCopy;
        public string ToolPreparationSuccessCopy => _toolPreparationSuccessCopy;
        public string ToolPreparationDeferredCopy => _toolPreparationDeferredCopy;
        [SerializeField] string _toolPreparationStartLabel = "开始探索";
        [SerializeField] VisitorCoachCueRecord[] _cues = Array.Empty<VisitorCoachCueRecord>();
        public string ToolPreparationReadyCopy => _toolPreparationReadyCopy;
        public string ToolPreparationStartLabel => _toolPreparationStartLabel;

        public GameObject PresentationPrefab => _presentationPrefab;
        public float ViewerDistance => _viewerDistance;
        public Vector2 PanelPixels => _panelPixels;
        public float CanvasScale => _canvasScale;
        public float DetailFontSize => _detailFontSize;
        public float SpeakerFontSize => _speakerFontSize;
        public float DialogueFontSize => _dialogueFontSize;
        public float FadeSeconds => _fadeSeconds;
        public float ConfirmDebounceSeconds => _confirmDebounceSeconds;
        public float FairyAttentionSeconds => _fairyAttentionSeconds;
        public float DialogueVerticalOffset => _dialogueVerticalOffset;
        public Vector2 FairyDialogueHorizontalOffset => _fairyDialogueHorizontalOffset;
        public string FairySpeakerName => _fairySpeakerName?.Trim() ?? string.Empty;
        public Color PanelColor => _panelColor;
        public Color TextColor => _textColor;
        public Color DetailTextColor => _detailTextColor;
        public Color AccentColor => _accentColor;
        public Color SpeakerColor => _speakerColor;

        public VisitorCoachTiming Timing =>
            new VisitorCoachTiming(_directSeconds, _demonstrationSeconds, _recoverySeconds);

        public bool TryResolveCopy(
            string cueKey,
            VisitorCoachHintLevel level,
            out string copy)
            => TryResolve(cueKey, level, requireGlobalOverlay: false, out copy);

        public bool TryResolveGlobalCopy(
            string cueKey,
            VisitorCoachHintLevel level,
            out string copy)
            => TryResolve(cueKey, level, requireGlobalOverlay: true, out copy);

        public int GetDialoguePageCount(string cueKey)
        {
            if (string.IsNullOrWhiteSpace(cueKey) || _cues == null) return 0;
            var normalized = cueKey.Trim();
            for (var index = 0; index < _cues.Length; index++)
            {
                var cue = _cues[index];
                if (cue == null || !string.Equals(cue.CueKey, normalized, StringComparison.Ordinal)) continue;
                return cue.DialoguePageCount;
            }
            return 0;
        }

        public bool TryResolveDialoguePage(string cueKey, int pageIndex, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(cueKey) || _cues == null) return false;
            var normalized = cueKey.Trim();
            for (var index = 0; index < _cues.Length; index++)
            {
                var cue = _cues[index];
                if (cue == null || !string.Equals(cue.CueKey, normalized, StringComparison.Ordinal)) continue;
                return cue.TryGetDialoguePage(pageIndex, out text);
            }
            return false;
        }

        public bool TryGetOptionalDialogueAudio(string cueKey, int pageIndex, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(cueKey) || _cues == null) return false;
            var normalized = cueKey.Trim();
            for (var index = 0; index < _cues.Length; index++)
            {
                var cue = _cues[index];
                if (cue == null || !string.Equals(cue.CueKey, normalized, StringComparison.Ordinal)) continue;
                return cue.TryGetOptionalDialogueAudio(pageIndex, out clip);
            }
            return false;
        }

        public bool IsValid(out string error)
        {
            if (_presentationPrefab == null)
            {
                error = "Visitor Coach theme has no presentation prefab.";
                return false;
            }
            try { _ = Timing; }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            if (!IsPositiveFinite(_viewerDistance) ||
                _panelPixels.x <= 0f || _panelPixels.y <= 0f ||
                !IsPositiveFinite(_canvasScale) ||
                !IsPositiveFinite(_detailFontSize) || !IsPositiveFinite(_speakerFontSize) ||
                !IsPositiveFinite(_dialogueFontSize) || !IsPositiveFinite(_fadeSeconds) ||
                !IsPositiveFinite(_confirmDebounceSeconds) ||
                !IsPositiveFinite(_fairyAttentionSeconds) || !IsFinite(_dialogueVerticalOffset) ||
                !IsFinite(_fairyDialogueHorizontalOffset.x) || !IsFinite(_fairyDialogueHorizontalOffset.y) ||
                string.IsNullOrWhiteSpace(FairySpeakerName) ||
                string.IsNullOrWhiteSpace(ToolPreparationIntroduction) || string.IsNullOrWhiteSpace(ToolPreparationReadyCopy) || string.IsNullOrWhiteSpace(ToolPreparationStartLabel) ||
                string.IsNullOrWhiteSpace(ToolPreparationPracticeCopy) || string.IsNullOrWhiteSpace(ToolPreparationSummonedCopy) ||
                string.IsNullOrWhiteSpace(ToolPreparationSuccessCopy) || string.IsNullOrWhiteSpace(ToolPreparationDeferredCopy) ||
                string.IsNullOrWhiteSpace(GuidanceChapter) || string.IsNullOrWhiteSpace(GuidanceUnavailableCopy) ||
                string.IsNullOrWhiteSpace(GuidanceEndLabel) || string.IsNullOrWhiteSpace(GuidanceDepartureCopy) || string.IsNullOrWhiteSpace(GuidanceDepartureLabel))
            {
                error = "Visitor Coach global cue layout contains invalid values.";
                return false;
            }


            if (_cues == null || _cues.Length == 0)
            {
                error = "Visitor Coach theme has no cue records.";
                return false;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            VisitorCoachCueRecord firstQrCue = null;
            for (var index = 0; index < _cues.Length; index++)
            {
                var cue = _cues[index];
                if (cue == null)
                {
                    error = $"Visitor Coach cue record {index} is missing.";
                    return false;
                }
                if (!cue.IsValid(out error)) return false;
                if (!keys.Add(cue.CueKey))
                {
                    error = $"Visitor Coach theme repeats cue key '{cue.CueKey}'.";
                    return false;
                }
                if (string.Equals(cue.CueKey, VisitorCoachCueKeys.QrConfirm, StringComparison.Ordinal))
                    firstQrCue = cue;
            }

            if (firstQrCue == null || !firstQrCue.GlobalOverlay || !firstQrCue.HasExplicitRecovery)
            {
                error = "Visitor Coach theme requires one global first-QR cue with explicit recovery copy.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        bool TryResolve(
            string cueKey,
            VisitorCoachHintLevel level,
            bool requireGlobalOverlay,
            out string copy)
        {
            copy = string.Empty;
            if (string.IsNullOrWhiteSpace(cueKey) || _cues == null) return false;
            var normalized = cueKey.Trim();
            for (var index = 0; index < _cues.Length; index++)
            {
                var cue = _cues[index];
                if (cue == null || !string.Equals(cue.CueKey, normalized, StringComparison.Ordinal)) continue;
                if (requireGlobalOverlay && !cue.GlobalOverlay) return false;
                return cue.TryResolve(level, out copy);
            }
            return false;
        }

        static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
