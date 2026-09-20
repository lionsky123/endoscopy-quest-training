using System;
using System.Collections.Generic;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Panorama.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Panorama.Frontend
{
    // One reading surface; the temporary locator shares the panorama's world orientation.
    internal sealed class ClinicalEvidenceControls : IDisposable
    {
        readonly GameObject _root, _reading, _locator, _shade;
        readonly ClinicalReferenceDialogue _dialogue;
        readonly ClinicalWorldSurface _surface, _locatorSurface;
        Vector3 _entryPosition;
        readonly Transform _viewer;
        readonly TMP_FontAsset _font;
        readonly RawImage _image;
        readonly RectTransform _annotations;
        readonly TMP_Text _title, _copy, _status, _credit, _methodRibbon;
        readonly Button _advance, _yes, _no, _submit, _skip, _openObservation;
        readonly CanvasGroup _group;
        readonly IFrontendGazeSurfaceRegistration _registration, _locatorRegistration;
        readonly List<Button> _regions = new List<Button>();
        readonly List<GameObject> _caseLabels = new List<GameObject>();
        readonly Texture _panorama;
        readonly Texture[] _cases;
        readonly Material _perspective;
        readonly Action _complete;
        readonly float _yaw;
        readonly ClinicalEvidenceSession _session;
        bool _disposed, _completionSent, _answerTutorialDismissed, _returning;
        float _answerTutorialTime;
        const float TutorialSeconds = 8;
        float _feedbackTime;
        float _transition = -1;
        Vector3 _fromPosition, _toPosition;
        Quaternion _fromRotation, _toRotation;
        const float Scale = .00055f, Distance = .50f, TransitionSeconds = .38f;
        internal ClinicalEvidenceSession Session => _session;

        public ClinicalEvidenceControls(Transform parent, TMP_FontAsset font,
            IFrontendGazeSurfaceRegistry surfaces, Action complete, Texture panorama, Transform viewer, float panoramaYaw = 0)
        {
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _font = font; _complete = complete; _panorama = panorama; _yaw = panoramaYaw;
            var data = Resources.Load<TextAsset>("ClinicalEvidence/lesson");
            if (data == null) throw new InvalidOperationException("Missing clinical evidence lesson.");
            _session = new ClinicalEvidenceSession(JsonUtility.FromJson<ClinicalEvidenceLesson>(data.text));
            _cases = new Texture[_session.Definition.topics.Length];
            for (int i = 0; i < _cases.Length; i++)
            {
                _cases[i] = Resources.Load<Texture>(_session.Definition.topics[i].image);
                if (_cases[i] == null) throw new InvalidOperationException("Missing unlabeled clinical case image.");
            }
            var shader = Resources.Load<Shader>("ClinicalEvidencePerspective");
            if (shader == null) throw new InvalidOperationException("Missing panorama evidence projection shader.");
            _perspective = new Material(shader) { name = "ClinicalEvidencePerspective" };
            _root = CanvasRoot(parent, "ClinicalPanoramaControls", 1200, 1200);
            _surface = _root.AddComponent<ClinicalWorldSurface>();
            _dialogue = new ClinicalReferenceDialogue(parent, viewer, font, surfaces);
            _group = _root.GetComponent<CanvasGroup>();
            var shade = Rect(_root.transform, "PanoramaReadingShade", 0, 0, 10000, 10000);
            shade.localPosition += Vector3.forward * 100;
            var dim = shade.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .24f); dim.raycastTarget = false;
            _shade = shade.gameObject;
            _reading = Rect(_root.transform, "EvidenceReadingSurface", 0, -65, 1160, 1040).gameObject;
            Frame((RectTransform)_reading.transform);
            var board = _reading.transform;
            _title = Label(board, font, "EvidenceTitle", 0, 478, 1080, 42, 29); _title.color = Accent;
            _image = Rect(board, "CurrentTeachingImage", 0, 112, 1080, 608).gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _annotations = Rect(_image.transform, "EvidenceAnnotations", 0, 0, 1080, 608);
            var ribbon = Rect(_image.transform, "MethodReadingGuide", 0, 250, 1040, 62);
            Fill(ribbon, new Color(.025f, .045f, .05f, .9f), 8);
            _methodRibbon = Label(ribbon, font, "MethodGuideCopy", 0, 0, 1000, 54, 27);
            _methodRibbon.alignment = TextAlignmentOptions.Center;
            Fill(Rect(board, "QuestionBacking", 0, -270, 1080, 96), new Color(.025f, .08f, .13f, .97f), 10);
            _copy = Label(board, font, "CurrentInstruction", 0, -270, 1044, 92, 26);
            _status = Label(board, font, "EvidenceStatus", 0, -367, 1080, 100, 22);
            _advance = MakeButton(board, "ContinueEvidence", "开始核查", 0, -458, 420, Advance, true);
            _yes = MakeButton(board, "VerdictConforms", "符合条件", -405, -458, 250, () => SelectVerdict(true));
            _no = MakeButton(board, "VerdictNeedsCorrection", "需要纠正", -135, -458, 250, () => SelectVerdict(false));
            _submit = MakeButton(board, "SubmitEvidence", "确认判断", 135, -458, 250, Submit, true);
            _skip = MakeButton(board, "SkipEvidence", "跳过本题", 405, -458, 250, Skip);
            _credit = Label(board, font, "TeachingImageCredit", 0, -208, 1080, 26, 17);
            _credit.color = Muted;
            _locator = CanvasRoot(parent, "PanoramaObservationLocator", 320, 320);
            _locatorSurface = _locator.AddComponent<ClinicalWorldSurface>();
            _openObservation = ClinicalEntryHalo.Create(_locator.transform, font, "OpenObservation", Locate);
            BuildTopic(); // Register the stable region pool with the shared gaze controller.
            _registration = surfaces.RegisterGazeSurface(_root.transform, 500, "ClinicalEvidenceLesson");
            _locatorRegistration = surfaces.RegisterGazeSurface(_locator.transform, 500, "ClinicalEvidenceLocator");
            ClinicalNearTouch.Bind(_reading.transform, () => CanAct);
            ClinicalNearTouch.Bind(_locator.transform, () => CanAct && _session.Phase == ClinicalEvidencePhase.Seek);
            Reset();
        }
        Button MakeButton(Transform parent, string name, string copy, float x, float y, float width, Action action, bool primary = false, float height = 70)
        {
            var button = ClinicalPanelStyle.Button(parent, _font, name, copy, x, y, width, height, action, primary);
            ClinicalPanelStyle.EmphasizeButton(button, primary: primary);
            return button;
        }
        static GameObject CanvasRoot(Transform parent, string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * Scale;
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; canvas.sortingOrder = 500; return go;
        }
        bool CanAct => !_disposed && _root.activeInHierarchy && _transition < 0 && _group.interactable;
        void Invalidate() { _registration.Invalidate(); _locatorRegistration.Invalidate(); }
        public void Reset()
        {
            Invalidate(); _session.Reset(); _completionSent = _answerTutorialDismissed = false;
            _answerTutorialTime = 0; _entryPosition = _viewer.position;
            _transition = -1; _returning = false; _feedbackTime = 0; _group.alpha = 1; _group.interactable = _group.blocksRaycasts = true;
            _root.transform.localScale = Vector3.one * Scale;
            PlaceForReading(); BuildTopic(); Render(); UpdateLocator(); ShowDialogue();
        }
        void PlaceForReading()
        {
            ClinicalWorldSurface.PlaceForHands(_root.transform, _viewer, Scale); _surface.Pin();
        }
        Vector3 ObservationDirection => ClinicalEvidenceSession.PanoramaDirection(_session.Topic.panoramaUv, _yaw);
        void UpdateLocator()
        {
            var direction = Vector3.ProjectOnPlane(ObservationDirection, Vector3.up).normalized;
            var position = _entryPosition + direction * Distance - Vector3.up * .18f;
            _locator.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _entryPosition, Vector3.up));
            _locatorSurface.Pin();
        }
        void ShowDialogue() { _dialogue.Show(_session.TopicIndex, _session.Topic.title, _locator.transform.position); _locator.SetActive(false); }
        void Locate()
        {
            if (!CanAct || !_session.Locate()) return;
            Invalidate(); _dialogue.Hide(); PlaceForReading(); Render();
            _toPosition = _root.transform.position; _toRotation = _root.transform.rotation;
            _fromPosition = _locator.transform.position;
            _fromRotation = _locator.transform.rotation;
            if (Application.isPlaying) { _returning = false; _transition = 0; _group.interactable = _group.blocksRaycasts = false; Tick(0); }
        }
        void Advance()
        {
            if (!CanAct) return;
            Invalidate();
            if (_session.Phase == ClinicalEvidencePhase.Method) _session.Ask();
            else if (_session.Phase == ClinicalEvidencePhase.Feedback)
            {
                if (Application.isPlaying)
                {
                    _fromPosition = _root.transform.position; _fromRotation = _root.transform.rotation;
                    _toPosition = _locator.transform.position;
                    _toRotation = _locator.transform.rotation;
                    _returning = true; _transition = 0; _group.interactable = _group.blocksRaycasts = false;
                    return;
                }
                FinishTopic(); return;
            }
            Render();
        }
        void FinishTopic()
        {
            if (_session.Continue())
            {
                Render();
                if (!_completionSent) { _completionSent = true; _complete?.Invoke(); }
                return;
            }
            PlaceForReading(); BuildTopic(); Render(); UpdateLocator(); ShowDialogue();
        }
        void SelectRegion(int index) { if (!CanAct) return; Invalidate(); _answerTutorialDismissed = true; _session.SelectRegion(index); Render(); }
        void SelectVerdict(bool conforms) { if (!CanAct) return; Invalidate(); _answerTutorialDismissed = true; _session.SelectVerdict(conforms); Render(); }
        void Submit() { if (!CanAct || !_session.CanSubmit) return; Invalidate(); _session.Submit(); _feedbackTime = 0; Render(); }
        void Skip()
        {
            if (!CanAct || !_session.Skip()) return;
            _answerTutorialDismissed = true;
            Advance(); // Same return animation and single completion callback as an answered topic.
        }

        void BuildTopic()
        {
            foreach (var label in _caseLabels) { label.SetActive(false); Destroy(label); }
            _caseLabels.Clear();
            var topic = _session.Topic;
            if (_regions.Count == 0) for (int i = 0; i < 8; i++)
            {
                var rect = Rect(_annotations, "EvidenceRegion_" + i, 0, 0, 10, 10);
                var hit = rect.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .005f);
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
                button.navigation = new Navigation { mode = Navigation.Mode.None }; button.transition = Selectable.Transition.None;
                int regionIndex = i; button.onClick.AddListener(() => SelectRegion(regionIndex));
                var outline = ClinicalPanelStyle.Outline(Rect(rect, "SelectionOutline", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y), Color.white);
                var badge = Rect(rect, "RegionBadge", 0, 0, 110, 40);
                var badgeFill = Fill(badge, new Color(.03f,.12f,.22f,1), 6);
                var number = Label(badge, _font, "RegionNumber", 0, 0, 100, 36, 19);
                number.alignment = TextAlignmentOptions.Center;
                rect.gameObject.AddComponent<ClinicalEvidenceRegionFeedback>().Bind(outline, hit, badgeFill, number, i + 1);
                _regions.Add(button);
            }
            for (int i = 0; i < _regions.Count; i++)
            {
                _regions[i].gameObject.SetActive(false);
                if (i >= topic.regions.Length) continue;
                var r = topic.regions[i].bounds.ToRect();
                var rect = (RectTransform)_regions[i].transform;
                rect.anchoredPosition = new Vector2((r.center.x - .5f) * 1080, (r.center.y - .5f) * 608);
                rect.sizeDelta = new Vector2(r.width * 1080, r.height * 608);
                var outline = (RectTransform)rect.Find("SelectionOutline");
                outline.sizeDelta = rect.sizeDelta;
                ClinicalPanelStyle.ResizeOutline(outline.GetComponent<Image>(), rect.sizeDelta);
                var badge = (RectTransform)rect.Find("RegionBadge");
                badge.sizeDelta = new Vector2(Mathf.Min(110, rect.sizeDelta.x - 8), 40);
                badge.anchoredPosition = new Vector2(-rect.sizeDelta.x * .5f + badge.sizeDelta.x * .5f + 4, -rect.sizeDelta.y * .5f + 25);
                ((RectTransform)badge.Find("RegionNumber")).sizeDelta = new Vector2(badge.sizeDelta.x - 4, 36);
            }
            foreach (var label in topic.labels ?? Array.Empty<ClinicalEvidenceLabel>())
            {
                float x = (label.position.x - .5f) * 1080, y = (label.position.y - .5f) * 608;
                float w = topic.labels.Length > 2 ? 150 : 420;
                var backing = Rect(_annotations, "CaseLabelBacking", x, y, w, 44);
                Fill(backing, new Color(.025f, .045f, .05f, .94f), 6);
                var text = Label(_annotations, _font, "CaseLabel", x, y, w, 44, 22);
                text.text = label.text; text.alignment = TextAlignmentOptions.Center; text.fontStyle = FontStyles.Bold;
                _caseLabels.Add(backing.gameObject); _caseLabels.Add(text.gameObject);
            }
        }
        void Render()
        {
            var phase = _session.Phase;
            var seek = phase == ClinicalEvidencePhase.Seek;
            var question = phase == ClinicalEvidencePhase.Question;
            var feedback = phase == ClinicalEvidencePhase.Feedback;
            _reading.SetActive(!seek && phase != ClinicalEvidencePhase.Finished);
            _shade.SetActive(_reading.activeSelf);
            _locator.SetActive(seek);
            _advance.gameObject.SetActive(phase == ClinicalEvidencePhase.Method || feedback);
            foreach (var button in new[] { _yes, _no, _submit, _skip }) button.gameObject.SetActive(question);
            _submit.interactable = _session.CanSubmit;
            ClinicalPanelStyle.EmphasizeButton(_submit, primary: true);
            ClinicalPanelStyle.EmphasizeButton(_yes, _session.Verdict == 1);
            ClinicalPanelStyle.EmphasizeButton(_no, _session.Verdict == 0);
            var topic = _session.Topic;
            var skipLabel = _session.TopicIndex == _session.Definition.topics.Length - 1 ? "跳过并结束" : "跳过本题";
            _skip.GetComponentInChildren<TMP_Text>().text = skipLabel;
            _title.text = $"{(question ? "图片题" : "观察")} {_session.TopicIndex + 1:00} / 03   ·   {topic.title}   ·   {(question ? "请作答" : feedback ? "依据解析" : "观察方法")}";
            _copy.text = feedback ? topic.explanation : question ? "<color=#FFD775>本题问题：</color>" + topic.question : topic.method;
            _status.text = question
                ? SelectionCopy() + "\n" + (_session.LastSubmissionIncorrect ? $"尚未答对，选择已保留；可修改重试，或点「{skipLabel}」。\n" : "") +
                    (_session.HintLevel > 0 ? "观察提示：" + topic.hints[_session.HintLevel - 1] : "")
                : feedback ? _session.ResolvedCount == 3 ? $"本次答对 {_session.CompletedCount} 题，跳过 {_session.SkippedCount} 题。\n点「结束学习」返回房间，再点「出发」跟随小精灵前往下一站。"
                    : $"本题已完成。看完解析，点下方继续，寻找观察点 {_session.TopicIndex + 2:00}。"
                    : "先看上图的观察方法，看完后点「开始答题」。";
            if (question && !_answerTutorialDismissed)
                _status.text = "小精灵：轻触图片编号区域，可多选，再碰取消。\n再轻触一个判断和「提交判断」。卡住时可用「跳过本题」。";
            _advance.GetComponentInChildren<TMP_Text>().text = feedback
                ? _session.ResolvedCount == 3 ? "结束学习 · 准备出发" : $"继续 · 观察点 {_session.TopicIndex + 2:00}" : "开始答题";
            _yes.GetComponentInChildren<TMP_Text>().text = (_session.Verdict == 1 ? "已选 · " : "") + "符合条件";
            _no.GetComponentInChildren<TMP_Text>().text = (_session.Verdict == 0 ? "已选 · " : "") + "需要纠正";
            _submit.GetComponentInChildren<TMP_Text>().text = _session.SelectedMask == 0 ? "先点图片选区"
                : _session.Verdict < 0 ? "再选下方判断" : _session.LastSubmissionIncorrect ? "重新提交" : "提交判断";
            _image.texture = question || feedback ? _cases[_session.TopicIndex] : _panorama;
            _credit.text = question || feedback ? "教学情境示意 · 案例图由 AI 生成 · 标签为本题设定" : "全景观察特写 · 方法示范 · 不作为现场配置证明";
            _methodRibbon.transform.parent.gameObject.SetActive(phase == ClinicalEvidencePhase.Method);
            _methodRibbon.text = _session.TopicIndex == 0 ? "观察顺序：墙体连接 → 门框 → 门的当前状态"
                : _session.TopicIndex == 1 ? "两组分别核对：用途 → 槽 → 机器"
                : "清洗 → 漂洗 → 消毒 → 终末漂洗 → 干燥";
            _image.uvRect = new Rect(0, 0, 1, 1); _image.material = question || feedback ? null : _perspective;
            _perspective.SetVector("_ViewCenter", new Vector4(topic.panoramaUv.x, topic.panoramaUv.y, 0, 0));
            _perspective.SetFloat("_Aspect", 1080f / 608f);
            _perspective.SetFloat("_TanHalfFov", Mathf.Tan(38 * Mathf.Deg2Rad * .5f));
            for (int i = 0; i < _regions.Count; i++)
            {
                _regions[i].gameObject.SetActive(i < topic.regions.Length && (question || (feedback && (topic.requiredMask & (1 << i)) != 0)));
                _regions[i].interactable = question;
                _regions[i].GetComponent<ClinicalEvidenceRegionFeedback>().SetState((_session.SelectedMask & (1 << i)) != 0, feedback);
            }
            foreach (var label in _caseLabels) label.SetActive(question || feedback);
            UpdateEvidenceReveal();
        }
        void UpdateEvidenceReveal()
        {
            if (_session.Phase != ClinicalEvidencePhase.Feedback) return;
            var order = 0;
            for (int i = 0; i < _regions.Count; i++)
                if ((_session.Topic.requiredMask & (1 << i)) != 0)
                    _regions[i].gameObject.SetActive(!Application.isPlaying || _feedbackTime >= order++ * .5f);
        }
        string SelectionCopy()
        {
            var selected = new List<string>();
            for (int i = 0; i < _session.Topic.regions.Length; i++) if ((_session.SelectedMask & (1 << i)) != 0) selected.Add((i + 1).ToString());
            return "已选依据：" + (selected.Count == 0 ? "未选择" : string.Join("、", selected)) +
                "　|　判断：" + (_session.Verdict < 0 ? "未选择" : _session.Verdict == 1 ? "符合本题条件" : "需要纠正");
        }
        public void InvalidateInput() => Invalidate();
        public void Tick(float deltaTime)
        {
            if (_disposed || !_root.activeInHierarchy) return;
            _dialogue.Tick(deltaTime); _locatorSurface.Restore();
            _locator.SetActive(_session.Phase == ClinicalEvidencePhase.Seek && !_dialogue.IsVisible);
            if (_transition < 0) _surface.Restore();
            if (_session.Phase == ClinicalEvidencePhase.Question && !_answerTutorialDismissed && CanAct)
            {
                _answerTutorialTime += Mathf.Max(0, deltaTime);
                if (_answerTutorialTime >= TutorialSeconds) { _answerTutorialDismissed = true; Render(); }
            }
            if (_session.Phase == ClinicalEvidencePhase.Feedback) { _feedbackTime += Mathf.Max(0, deltaTime); UpdateEvidenceReveal(); }
            if (_transition < 0) return;
            _transition += Mathf.Max(0, deltaTime);
            var t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_transition / TransitionSeconds));
            _root.transform.SetPositionAndRotation(Vector3.Lerp(_fromPosition, _toPosition, t), Quaternion.Slerp(_fromRotation, _toRotation, t));
            var size = _returning ? 1 - t : t;
            _root.transform.localScale = Vector3.one * Scale * Mathf.Lerp(.35f, 1f, size); _group.alpha = Mathf.Lerp(.1f, 1f, size);
            _surface.Pin();
            if (_transition < TransitionSeconds) return;
            _transition = -1; _group.interactable = _group.blocksRaycasts = true; Invalidate();
            if (_returning)
            {
                _returning = false; _root.transform.localScale = Vector3.one * Scale; _group.alpha = 1;
                FinishTopic();
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _registration.Dispose(); _locatorRegistration.Dispose();
            _dialogue.Dispose();
            Destroy(_root); Destroy(_locator); Destroy(_perspective);
        }
        static void Destroy(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }

    internal sealed class ClinicalEvidenceRegionFeedback : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        Image _outline, _fill, _badge; TMP_Text _label; int _number; bool _selected, _answer;
        public void Bind(Image outline, Image fill, Image badge, TMP_Text label, int number)
        { _outline = outline; _fill = fill; _badge = badge; _label = label; _number = number; PresentGazeProgress(0); }
        public void SetState(bool selected, bool answer) { _selected = selected; _answer = answer; PresentGazeProgress(0); }
        public void PresentGazeProgress(float progress)
        {
            if (_outline == null) return;
            var color = _answer ? new Color(.3f, .9f, .7f) : new Color(.43f, .74f, 1f);
            color.a = _selected || _answer ? 1f : Mathf.Lerp(.65f, 1f, progress); _outline.color = color;
            _fill.color = new Color(.25f,.58f,.92f,_selected || _answer ? .15f : .025f);
            _badge.color = _selected || _answer ? new Color(.06f,.31f,.55f,1) : new Color(.03f,.12f,.22f,1);
            _label.text = _number + (_answer ? " 依据" : _selected ? " 已选" : " 未选");
        }
    }
}
