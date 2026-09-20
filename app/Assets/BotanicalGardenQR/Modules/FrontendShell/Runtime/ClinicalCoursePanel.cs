using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // One stable surface for every later station; only its media area changes.
    public sealed class ClinicalCoursePanel : IDisposable
    {
        readonly GameObject _root, _media;
        readonly TMP_FontAsset _font;
        readonly TMP_Text _title, _tag, _prompt, _status, _credit, _body, _tutorial;
        readonly RawImage _picture;
        readonly Button[] _choices = new Button[3], _tabs = new Button[3];
        readonly Button[] _processCards = new Button[5];
        readonly Button _advance, _submit, _skip, _mediaAction;
        readonly IFrontendGazeSurfaceRegistration _registration;
        readonly Func<SessionToken, bool> _complete;
        readonly ClinicalCourseCatalog _catalog;
        readonly ClinicalWorldSurface _worldSurface;
        ClinicalCourseSession _session;
        SessionToken _token;
        VideoPlayer _video;
        RenderTexture _videoTexture;
        GameObject _model;
        Grabbable _modelGrab;
        Material _modelMaterial;
        bool _disposed, _positioned, _completionSent, _videoFailed, _videoPlaybackRequested;
        float _tutorialTime, _prepareTime;
        int _mediaIndex = -1;
        public ClinicalCourseSession Session => _session;
        bool ModelIsHeld => _modelGrab && _modelGrab.SelectingPoints != null && _modelGrab.SelectingPointsCount > 0;

        public ClinicalCoursePanel(Transform parent, TMP_FontAsset font, IFrontendGazeSurfaceRegistry surfaces, Func<SessionToken, bool> complete)
        {
            _font = font; _complete = complete; _catalog = ClinicalCourseCatalog.Load();
            _root = new GameObject("ClinicalCoursePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            _worldSurface = _root.AddComponent<ClinicalWorldSurface>();
            var rect = (RectTransform)_root.transform; rect.SetParent(parent, false); rect.sizeDelta = new Vector2(1140, 1050);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true; canvas.sortingOrder = 230;
            Frame(rect);
            _tag = Label(rect, font, "StationModality", 0, 470, 1060, 36, 23); _tag.color = Accent;
            _title = Label(rect, font, "TaskTitle", 0, 411, 1060, 64, 35);
            _media = Rect(rect, "MediaStage", 0, 95, 1060, 540).gameObject;
            Fill((RectTransform)_media.transform, new Color(.025f, .045f, .075f, 1));
            _picture = Rect(_media.transform, "LessonMedia", 0, 0, 1060, 540).gameObject.AddComponent<RawImage>(); _picture.raycastTarget = false;
            _body = Label(_media.transform, font, "EvidenceBody", 0, 0, 984, 462, 31);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                _processCards[i] = Make(_media.transform, "ProcessCard" + i, "", i < 3 ? -350 + 350 * i : -180 + 360 * (i - 3), i < 3 ? 85 : -105, 320, 150,
                    () => { _session.ToggleSequence(index); _tutorialTime = 8; Render(); });
                _processCards[i].GetComponentInChildren<TMP_Text>().fontSize = 32;
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _tabs[i] = Make(_media.transform, "EvidenceTab" + i, "", -350 + 350 * i, 213, 332, 66, () => Read(index));
                _choices[i] = Make(rect, "CourseChoice" + i, "", -354 + 354 * i, -299, 338, 102, () => Select(index));
                _choices[i].GetComponentInChildren<TMP_Text>().fontSize = 25;
            }
            _credit = Label(rect, font, "MediaCredit", 0, -192, 1060, 30, 17); _credit.color = Muted;
            _prompt = Label(rect, font, "CourseQuestion", 0, -230, 1060, 48, 27);
            _status = Label(rect, font, "CourseStatus", 0, -385, 1060, 64, 23);
            _tutorial = Label(rect, font, "BriefFairyTip", 0, -499, 1060, 32, 22); _tutorial.color = Accent;
            _advance = Make(rect, "ContinueCourse", "开始核查", 0, -445, 570, 66, Advance, true);
            _submit = Make(rect, "SubmitCourse", "确认选择", -184, -445, 342, 66, Submit, true);
            _skip = Make(rect, "SkipCourse", "跳过本题", 184, -445, 342, 66, Skip);
            _mediaAction = Make(_media.transform, "MediaAction", "", 295, -210, 430, 66, MediaAction);
            _registration = surfaces.RegisterGazeSurface(rect, 500, "ClinicalLaterCourse");
            ClinicalNearTouch.Bind(_root.transform);
            _root.AddComponent<ClinicalCourseTicker>().Tick = Tick;
            _root.SetActive(false);
        }
        Button Make(Transform parent, string name, string copy, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var button = ClinicalPanelStyle.Button(parent, _font, name, copy, x, y, w, h, action, primary);
            EmphasizeButton(button, primary: primary); return button;
        }
        public void Present(SessionToken token, string id)
        {
            if (_session != null && token == _token && _session.Lesson.sceneId == id) return;
            ReleaseMedia(); _token = token; _session = new ClinicalCourseSession(_catalog.Find(id));
            _completionSent = false; _positioned = false; _mediaIndex = -1; _tutorialTime = 0; Render();
        }
        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            _root.SetActive(visible && _session != null);
            if (!visible)
            {
                _videoPlaybackRequested = false;
                if (_video) { _video.Pause(); MediaLabel(_video.isPrepared ? "继续播放" : "播放静音短片"); }
                _registration.Invalidate(); return;
            }
            var camera = _root.GetComponent<Canvas>().worldCamera;
            if (!camera) camera = Camera.main;
            if (!_positioned && camera)
            {
                ClinicalWorldSurface.PlaceForHands(_root.transform, camera.transform);
                _worldSurface.Pin();
                _positioned = true;
            }
            _worldSurface.Restore();
        }
        void Select(int index) { _session.Select(index); _tutorialTime = 8; Render(); }
        void Read(int index)
        {
            _session.ReadEvidence(index); _body.text = _session.Step.cards[index].body;
            foreach (var tab in _tabs) EmphasizeButton(tab, tab == _tabs[index]);
            UpdateStatus(); _registration.Invalidate();
        }
        void Submit() { _session.Submit(); Render(); }
        void Skip() { _tutorialTime = 8; _session.Skip(); Render(); }
        void Advance()
        {
            if (_session.Phase == ClinicalCoursePhase.Finished)
            {
                if (!_completionSent) { _completionSent = _complete(_token); if (!_completionSent) _status.text = "暂未完成返回，请再点一次返回房间。"; }
                return;
            }
            _session.Continue(); Render();
        }
        void Render()
        {
            var phase = _session.Phase; var task = phase == ClinicalCoursePhase.Task; var feedback = phase == ClinicalCoursePhase.Feedback;
            var step = _session.Step;
            var sequence = step.mode == "sequence";
            _registration.Invalidate();
            _tag.text = $"第 {_session.Lesson.station} 关  /  {ModeName(step.mode)}  /  {_session.Index + 1} · {_session.Lesson.steps.Length}";
            _title.text = phase == ClinicalCoursePhase.Introduction ? _session.Lesson.title : phase == ClinicalCoursePhase.Finished ? "本站核查完成" : step.title;
            _media.SetActive(true);
            _body.gameObject.SetActive(true);
            if (phase == ClinicalCoursePhase.Introduction || phase == ClinicalCoursePhase.Finished)
            {
                ReleaseMedia();
                _picture.gameObject.SetActive(false); _mediaAction.gameObject.SetActive(false);
                _body.rectTransform.anchoredPosition = Vector2.zero; _body.rectTransform.sizeDelta = new Vector2(960, 430);
                _body.text = phase == ClinicalCoursePhase.Introduction ? _session.Lesson.introduction + "\n\n" + string.Join("\n", Array.ConvertAll(_session.Lesson.steps, item => "• " + item.title)) :
                    $"答对 {_session.CorrectCount} 题\n\n跳过 {_session.SkippedCount} 题\n\n已完成本站学习流程。\n回到房间后，小精灵会主动询问是否出发。";
                _prompt.text = phase == ClinicalCoursePhase.Introduction ? "小精灵：准备好后，开始本站的小任务。" : "回到房间后，确认出发，再实际跟随小精灵。";
                _status.text = phase == ClinicalCoursePhase.Introduction ? "本关所有任务均可修改重试，也可用一个按钮跳过。" : "跳过不计答对；出发只启动带路，不移动玩家。";
                _credit.text = "";
            }
            else
            {
                if (_mediaIndex != _session.Index) { ReleaseMedia(); _mediaIndex = _session.Index; OpenMedia(step); }
                _prompt.text = step.prompt; _credit.text = step.credit;
                UpdateStatus();
            }
            for (int i = 0; i < 3; i++)
            {
                _choices[i].gameObject.SetActive(!sequence && (task || feedback)); _choices[i].interactable = task;
                _choices[i].GetComponentInChildren<TMP_Text>().text = sequence ? "" : (i == _session.Selected ? "✓ " : "") + step.options[i];
                EmphasizeButton(_choices[i], i == _session.Selected);
                _tabs[i].gameObject.SetActive((task || feedback) && step.mode == "evidence");
                _tabs[i].interactable = task;
            }
            for (int i = 0; i < _processCards.Length; i++)
            {
                var card = _processCards[i]; card.gameObject.SetActive(sequence && (task || feedback)); card.interactable = task;
                if (!sequence) continue;
                int position = -1;
                for (int j = 0; j < _session.Sequence.Count; j++) if (_session.Sequence[j] == i) position = j;
                card.GetComponentInChildren<TMP_Text>().text = (position < 0 ? "未排列" : $"第 {position + 1} 步") + "\n" + step.sequenceItems[i];
                EmphasizeButton(card, position >= 0);
            }
            _submit.gameObject.SetActive(task); _submit.interactable = _session.CanSubmit;
            _submit.GetComponentInChildren<TMP_Text>().text = sequence ? "确认工序顺序" : "确认选择";
            EmphasizeButton(_submit, primary: true);
            _skip.gameObject.SetActive(task);
            _skip.GetComponentInChildren<TMP_Text>().text = _session.Index + 1 == _session.Lesson.steps.Length ? "跳过并结束本站" : "跳过本题";
            _advance.gameObject.SetActive(!task);
            _advance.GetComponentInChildren<TMP_Text>().text = phase == ClinicalCoursePhase.Introduction ? "开始核查" : phase == ClinicalCoursePhase.Finished ? "回到房间 · 准备出发" : _session.Index + 1 == _session.Lesson.steps.Length ? "查看本站结果" : "继续下一项核查";
            _tutorial.gameObject.SetActive(task && _tutorialTime < 8);
            _tutorial.text = sequence ? "依次轻触工序卡；再碰已选卡可撤回，排列完成后确认。" : "小精灵：伸手轻触一个答案，再触碰确认；不需要拍打面板。";
            _mediaAction.interactable = task;
            if (!task && _video) { _videoPlaybackRequested = false; _video.Pause(); }
        }
        void UpdateStatus()
        {
            _status.text = _session.Phase == ClinicalCoursePhase.Feedback ? _session.Step.explanation : _session.LastIncorrect ? "再核对一下：" + _session.Step.hint :
                _session.Step.mode == "sequence" ? $"已排列 {_session.Sequence.Count} / 5 步 · 再触碰已选卡可撤回并重新编号。" :
                _session.Step.mode == "evidence" && _session.EvidenceMask != 7 ? "依次打开三个证据页，再作判断；也可以跳过。" : _session.Selected < 0 ? "请选择下方一个答案。" : "已选择，点“确认选择”提交；仍可改选。";
        }
        void OpenMedia(ClinicalCourseStep step)
        {
            _picture.gameObject.SetActive(false); _body.text = step.body ?? "";
            _body.rectTransform.anchoredPosition = new Vector2(0, step.mode == "evidence" ? -25 : 0);
            _body.rectTransform.sizeDelta = new Vector2(984, step.mode == "evidence" ? 360 : 462);
            _mediaAction.gameObject.SetActive(false); _videoFailed = false;
            if (step.mode == "sequence")
            {
                _body.rectTransform.anchoredPosition = new Vector2(0, 217); _body.rectTransform.sizeDelta = new Vector2(984, 65);
                _body.fontSize = 25;
            }
            else _body.fontSize = 31;
            if (step.mode == "image")
            {
                var texture = Resources.Load<Texture2D>(step.media);
                if (texture) { _picture.texture = texture; _picture.gameObject.SetActive(true); _body.text = ""; FitPicture(texture.width, texture.height); }
                else _body.text = "图片暂不可用。可按文字情境作答，或跳过继续。\n\n" + step.body;
            }
            else if (step.mode == "evidence")
            {
                for (int i = 0; i < 3; i++) { _tabs[i].GetComponentInChildren<TMP_Text>().text = step.cards[i].title; EmphasizeButton(_tabs[i]); }
                _body.text = step.body;
            }
            else if (step.mode == "video")
            {
                _body.rectTransform.sizeDelta = new Vector2(984, 350); _body.rectTransform.anchoredPosition = new Vector2(0, 48);
                _body.text = "静音器械扫描 · 观察外观和连接部\n\n" + step.body;
                _video = _media.AddComponent<VideoPlayer>(); _video.playOnAwake = false; _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.renderMode = VideoRenderMode.RenderTexture; _video.isLooping = false;
                _video.clip = Resources.Load<VideoClip>(step.media);
                _videoTexture = new RenderTexture(960, 540, 0); _video.targetTexture = _videoTexture;
                _video.prepareCompleted += VideoReady; _video.errorReceived += VideoError; _video.loopPointReached += VideoEnded;
                _mediaAction.gameObject.SetActive(true); MediaLabel("播放静音短片");
                if (!_video.clip) VideoError(_video, "missing clip");
            }
            else if (step.mode == "model") OpenModel(step);
        }
        void FitPicture(float w, float h)
        { float scale = Mathf.Min(1060 / w, 540 / h); _picture.rectTransform.sizeDelta = new Vector2(w * scale, h * scale); }
        void VideoReady(VideoPlayer player)
        {
            if (_disposed || !player || player != _video || _videoFailed) return;
            // Preparation may finish while hidden or after an answer. Retain the
            // prepared image, but only an active task may honour a play request.
            _prepareTime = 0; _picture.texture = _videoTexture; FitPicture(960, 540);
            if (_root.activeInHierarchy && _session.Phase == ClinicalCoursePhase.Task && _videoPlaybackRequested)
            {
                _picture.gameObject.SetActive(true); _body.text = "";
                player.Play(); MediaLabel("暂停观察");
            }
            else { _videoPlaybackRequested = false; MediaLabel("继续播放"); }
        }
        void VideoError(VideoPlayer player, string error)
        {
            if (_disposed || !player || player != _video) return;
            _videoFailed = true; _videoPlaybackRequested = false; _prepareTime = 0;
            player.Stop(); _picture.gameObject.SetActive(false);
            _body.text = "视频暂不可播放，可直接根据下方情境作答，或跳过继续。\n\n" + _session.Step.body;
            _mediaAction.gameObject.SetActive(false);
        }
        void VideoEnded(VideoPlayer player)
        {
            if (_disposed || !player || player != _video || _videoFailed) return;
            _videoPlaybackRequested = false;
            MediaLabel("重播静音短片"); _body.text = _session.Step.body; _picture.gameObject.SetActive(false);
        }
        void MediaLabel(string copy) => _mediaAction.GetComponentInChildren<TMP_Text>().text = copy;
        void MediaAction()
        {
            if (_disposed || !_root.activeInHierarchy || _session.Phase != ClinicalCoursePhase.Task) return;
            if (_model)
            {
                if (ModelIsHeld) return;
                _model.transform.localPosition = new Vector3(-280, 15, -160); _model.transform.localRotation *= Quaternion.Euler(0, 120, 0); return;
            }
            if (!_video || _videoFailed) return;
            if (_video.isPlaying) { _videoPlaybackRequested = false; _video.Pause(); MediaLabel("继续播放"); }
            else if (_video.isPrepared)
            {
                _videoPlaybackRequested = true; _prepareTime = 0; _picture.texture = _videoTexture;
                FitPicture(960, 540); _body.text = ""; _picture.gameObject.SetActive(true);
                if (_video.frame >= (long)_video.frameCount - 2) _video.time = 0;
                _video.Play(); MediaLabel("暂停观察");
            }
            else if (_prepareTime <= 0) { _videoPlaybackRequested = true; _prepareTime = .001f; _video.Prepare(); MediaLabel("正在准备…"); }
            _registration.Invalidate();
        }
        void OpenModel(ClinicalCourseStep step)
        {
            var asset = Resources.Load<GameObject>(step.media);
            if (!asset) { _body.text = "模型暂不可用。\n\n" + step.body; return; }
            _model = new GameObject("InspectableBottle"); _model.transform.SetParent(_media.transform, false);
            _model.SetActive(false); _model.transform.localPosition = new Vector3(-280, 15, -160);
            var visual = UnityEngine.Object.Instantiate(asset, _model.transform); visual.name = "OnlineSourcedBottle";
            var renderers = visual.GetComponentsInChildren<Renderer>();
            // OBJ is normalized in metres during import preparation; keep collision independent of the visual mesh.
            visual.transform.localScale = Vector3.one * 300;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _modelMaterial = new Material(shader); _modelMaterial.color = new Color(.72f, .86f, .91f);
            foreach (var renderer in renderers) renderer.sharedMaterial = _modelMaterial;
            var box = _model.AddComponent<BoxCollider>(); box.size = new Vector3(175, 300, 175);
            var body = _model.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            var grab = _model.AddComponent<Grabbable>(); grab.InjectOptionalRigidbody(body); grab.InjectOptionalThrowWhenUnselected(false);
            _modelGrab = grab;
            var hand = _model.AddComponent<HandGrabInteractable>(); hand.InjectRigidbody(body); hand.InjectOptionalPointableElement(grab); hand.HandAlignment = HandAlignType.None;
            _model.SetActive(true);
            _body.rectTransform.anchoredPosition = new Vector2(245, 20); _body.rectTransform.sizeDelta = new Vector2(475, 390);
            _body.text = step.body;
            _mediaAction.gameObject.SetActive(true); MediaLabel("翻看 / 取回瓶体");
        }
        public void Tick(float seconds)
        {
            if (_disposed || !_root.activeInHierarchy || _session == null) return;
            _worldSurface.Restore();
            if (_session.Phase == ClinicalCoursePhase.Task) _tutorialTime += seconds;
            if (_tutorialTime >= 8) _tutorial.gameObject.SetActive(false);
            if (_prepareTime > 0) { _prepareTime += seconds; if (_prepareTime > 12) { _video.Stop(); VideoError(_video, "timeout"); } }
            if (_model && !ModelIsHeld && _model.transform.localPosition.magnitude > 1250)
                _model.transform.localPosition = new Vector3(-280, 15, -160);
        }
        void ReleaseMedia()
        {
            if (_video) { _video.prepareCompleted -= VideoReady; _video.errorReceived -= VideoError; _video.loopPointReached -= VideoEnded; _video.Stop(); Destroy(_video); _video = null; }
            if (_videoTexture) { _videoTexture.Release(); Destroy(_videoTexture); _videoTexture = null; }
            if (_model) { Destroy(_model); _model = null; } _modelGrab = null;
            if (_modelMaterial) { Destroy(_modelMaterial); _modelMaterial = null; }
            _prepareTime = 0; _videoPlaybackRequested = false; _picture.texture = null;
        }
        static string ModeName(string mode) => mode == "sequence" ? "手部工序排序" : mode == "image" ? "图片找错" : mode == "video" ? "视频观察" : mode == "model" ? "模型核查" : mode == "ledger" ? "情境核查" : "证据追踪";
        static void Destroy(UnityEngine.Object obj) { if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; ReleaseMedia(); _registration.Dispose(); Destroy(_root);
        }
    }
    public sealed class ClinicalCourseTicker : MonoBehaviour
    {
        public Action<float> Tick;
        void Update() => Tick?.Invoke(Time.unscaledDeltaTime);
    }
}
