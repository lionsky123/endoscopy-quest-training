using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Panorama.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Panorama.Frontend
{
    // Practice and the three observations share a physical tool, but never share progress.
    internal sealed class ClinicalGuidedObservationControls : IDisposable
    {
        enum Phase { Tutorial, Practice, Ready, Seeking, Observed, Explanation, Finished }
        const float ObserveSeconds = 1.2f;
        const float PracticeJitterGraceSeconds = .20f;
        const float HaloRadius = .16f;
        readonly GameObject _root;
        readonly Transform _viewer;
        readonly ClinicalReferenceDialogue _dialogue;
        readonly ClinicalObservationTeachingPanel _teaching;
        readonly ClinicalObservationToken _token;
        readonly ClinicalWorldSurface _cueSurface, _targetSurface;
        readonly ClinicalHaloGraphic _halo;
        readonly RectTransform _progress;
        readonly ClinicalEvidenceLesson _lesson;
        readonly Action _complete;
        readonly ClinicalObservationProgress _saved;
        readonly Func<bool> _readOnly;
        readonly Action _closeReview;
        bool _reviewing;
        readonly TMP_Text _cue, _targetLabel, _progressStatus;
        readonly RawImage _liveView;
        readonly Material _liveProjection;
        readonly TMP_Text _liveStatus;
        bool _hasLiveFrame;
        readonly TMP_Text[] _directions = new TMP_Text[8];
        readonly ClinicalWorldSurface[] _directionSurfaces = new ClinicalWorldSurface[8];
        readonly Button _skip, _help, _recall, _advance;
        readonly IFrontendGazeSurfaceRegistration _registration;
        Vector3 _entry, _practiceDirection;
        readonly float _yaw;
        int _index;
        Phase _phase;
        bool _disposed;
        float _stableSeconds;
        float _practiceMissSeconds;
        bool ExplainingNow => _phase == Phase.Explanation;
        bool Formal => _phase == Phase.Seeking || _phase == Phase.Observed || ExplainingNow;
        bool Seeking => _phase == Phase.Practice || _phase == Phase.Seeking;
        bool Usable => !_disposed && _phase != Phase.Finished && _root.activeInHierarchy;
        public int CompletedCount { get; private set; }
        public int SkippedCount { get; private set; }
        internal int TopicIndex => _index;
        internal bool Explaining => ExplainingNow;
        internal string CurrentPhase => _phase.ToString();
        internal float ObservationProgress => _stableSeconds / ObserveSeconds;

        public ClinicalGuidedObservationControls(Transform parent, TMP_FontAsset font, IFrontendGazeSurfaceRegistry surfaces,
            Action complete, Texture panorama, Transform viewer, float yaw,
            ClinicalObservationProgress saved = null, Func<bool> readOnly = null, Action closeReview = null)
        {
            _viewer = viewer; _yaw = yaw; _complete = complete;
            _saved=saved;_readOnly=readOnly;_closeReview=closeReview;
            _lesson = JsonUtility.FromJson<ClinicalEvidenceLesson>(Resources.Load<TextAsset>("ClinicalEvidence/lesson").text);
            _root = new GameObject("ClinicalGuidedObservation"); _root.transform.SetParent(parent, false);
            _dialogue = new ClinicalReferenceDialogue(_root.transform, viewer, font, surfaces);
            _teaching = new ClinicalObservationTeachingPanel(_root.transform, viewer, font, surfaces, panorama, Continue, LookBack);
            _token = ClinicalObservationToken.Create(_root.transform);
            _token.RestPose = RestPose;
            _token.ConfigurePanoramaLens(panorama, Resources.Load<Shader>("ClinicalPanoramaMagnifier"), yaw, viewer.position, PanoramaDefinition.RadiusMetres);
            var caption = new GameObject("ObservationActionCue", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            caption.transform.SetParent(_root.transform, false);
            caption.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _cueSurface = caption.AddComponent<ClinicalWorldSurface>();
            Fill(Rect(caption.transform, "CueContrast", 0, 0, 600, 132), Surface);
            _cue = Label(caption.transform, font, "ActionCue", 0, 0, 580, 124, 25);
            _cue.alignment = TextAlignmentOptions.Center;
            _skip = ClinicalPanelStyle.Button(caption.transform, font, "SkipObservation", "跳过此处", 200, -108, 185, 64, Skip);
            _help = ClinicalPanelStyle.Button(caption.transform, font, "ObservationHelp", "操作说明", -200, -108, 185, 64, ShowHelp);
            _recall = ClinicalPanelStyle.Button(caption.transform, font, "RecallMagnifier", "取回放大镜", 0, -108, 185, 64, Recall);
            _advance = ClinicalPanelStyle.Button(caption.transform, font, "ContinueObservation", "开始练习", 0, -186, 600, 64, Continue, true);
            foreach (var button in new[] { _skip, _help, _recall, _advance })
            {
                var contrast = Rect(button.transform, "ContrastBase", 0, 0, button == _advance ? 600 : 185, 64);
                Fill(contrast, Surface); contrast.SetAsFirstSibling();
            }
            ClinicalNearTouch.Bind(caption.transform, () => Usable);
            _registration = surfaces.RegisterGazeSurface(caption.transform, 480, "ObservationFallback");
            var target = CreateGuideSurface("ObservationTarget");
            _targetSurface = target.GetComponent<ClinicalWorldSurface>();
            // One transparent-centred circle, 32 cm across at 1.4 m. Its visible inner
            // boundary is also the hit boundary, including after the viewer moves.
            _halo = Rect(target, "ObservationHalo", 0, 0, HaloRadius * 2000, HaloRadius * 2000)
                .gameObject.AddComponent<ClinicalHaloGraphic>();
            _halo.TransparentCenter = true; _halo.raycastTarget = false;
            Fill(Rect(target, "TargetCaption", 0, 750, 760, 104), Surface);
            _targetLabel = Label(target, font, "ObservationTargetLabel", 0, 750, 740, 96, 26);
            _targetLabel.alignment = TextAlignmentOptions.Center;
            Fill(Rect(target, "LiveViewBorder", 0, 465, 772, 412), Surface);
            _liveView = Rect(target, "LiveObservationView", 0, 465, 760, 400).gameObject.AddComponent<RawImage>();
            _liveView.texture = panorama; _liveView.raycastTarget = false;
            _liveProjection = new Material(Resources.Load<Shader>("ClinicalEvidencePerspective")) { name = "LiveObservationProjection" };
            _liveProjection.SetFloat("_Aspect", 760f / 400);
            // A broad contextual view, independent of the tiny physical 4x lens aperture.
            // 14 degrees vertically covers the entire 13-degree target circle, while
            // avoiding a room-wide thumbnail that makes the actual subject smaller.
            _liveProjection.SetFloat("_TanHalfFov", Mathf.Tan(7 * Mathf.Deg2Rad));
            _liveView.material = _liveProjection;
            Fill(Rect(target, "LiveViewStatusContrast", 0, 632, 740, 56), Surface);
            _liveStatus = Label(target, font, "LiveObservationStatus", 0, 632, 720, 48, 25);
            _liveStatus.alignment = TextAlignmentOptions.Center;
            Fill(Rect(target, "ObservationProgressBorder", 530, 30, 412, 34), Color.white);
            Fill(Rect(target, "ObservationProgressTrack", 530, 30, 400, 24), Surface);
            _progress = Rect(target, "ObservationProgress", 330, 30, 0, 24);
            _progress.pivot = new Vector2(0, .5f); Fill(_progress, new Color(1, .82f, .22f, 1));
            Fill(Rect(target, "ProgressStatusContrast", 530, -60, 590, 136), Surface);
            _progressStatus = Label(target, font, "ObservationProgressStatus", 530, -60, 570, 128, 27);
            _progressStatus.alignment = TextAlignmentOptions.Center;
            for (int i = 0; i < _directions.Length; i++)
            {
                var guide = CreateGuideSurface("ObservationDirection" + i);
                _directionSurfaces[i] = guide.GetComponent<ClinicalWorldSurface>();
                Fill(Rect(guide, "DirectionContrast", 0, 0, 540, 104), Surface);
                _directions[i] = Label(guide, font, "DirectionCue", 0, 0, 520, 96, 27);
                _directions[i].alignment = TextAlignmentOptions.Center;
            }
            Reset();
        }

        Transform CreateGuideSurface(string name)
        {
            var guide = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(ClinicalWorldSurface));
            guide.transform.SetParent(_root.transform, false);
            guide.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            return guide.transform;
        }

        public void Reset()
        {
            _index = CompletedCount = SkippedCount = 0; _phase = Phase.Tutorial; _stableSeconds = 0;
            _entry = _viewer.position;
            _practiceDirection = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            for (int i = 0; i < _directions.Length; i++)
            {
                var direction = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                var surface = _directionSurfaces[i];
                var position = _entry + direction * .9f + Vector3.up * .18f;
                surface.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _entry));
                surface.transform.localScale = Vector3.one * .00065f; surface.Pin();
            }
            _token.gameObject.SetActive(true); PlaceToken();
            _targetSurface.gameObject.SetActive(false);
            PlaceCue();
            PresentHelp(); UpdateDirections();
            if(_saved!=null)
            {
                CompletedCount=_saved.CompletedCount;SkippedCount=_saved.SkippedCount;
                _reviewing=_saved.NextTopic==3 || _readOnly?.Invoke()==true;
                if(_reviewing)
                {
                    _index=0;_token.Dismiss();ShowExplanation();
                }
                else if(_saved.Started)
                {
                    _index=_saved.NextTopic;_phase=Phase.Ready;
                    _dialogue.ShowObservation(_index,"继续本次观察",
                        $"已学习 {CompletedCount} 处，主动跳过 {SkippedCount} 处。\n工具重新放置；继续第 {_index+1} 处，不恢复旧抓取或画面。",
                        Continue,float.PositiveInfinity,"继续未完成观察",true,"本次进度保留");
                    RefreshActions();
                }
            }
        }
        void PlaceTarget(Vector3 look)
        {
            _hasLiveFrame = false;
            SetLiveCenter(look); _liveView.gameObject.SetActive(true);
            _liveStatus.text = "目标区域预览 · 捏起手柄开始取景";
            _targetSurface.transform.SetPositionAndRotation(_entry + look * 1.4f, Quaternion.LookRotation(look));
            _targetSurface.transform.localScale = Vector3.one * .001f; _targetSurface.Pin();
            _targetSurface.gameObject.SetActive(true);
            _halo.gameObject.SetActive(true);
            _progress.sizeDelta = new Vector2(0, 24);
            _halo.color = Formal ? Color.white : new Color(1, .8f, .35f, 1);
        }
        void BeginPractice()
        {
            _phase = Phase.Practice; _stableSeconds = _practiceMissSeconds = 0; _dialogue.Hide();
            PlaceTarget(_practiceDirection);
            _targetLabel.text = "操作练习 · 不计入进度\n小光圈定位，上方大窗看清内容";
            PlaceCue(); UpdateDirections();
        }
        void ReadyForObservation()
        {
            _phase = Phase.Ready; _stableSeconds = 0;
            _targetSurface.gameObject.SetActive(false); UpdateDirections(); PlaceCue();
            _dialogue.ShowObservation(0, "教学结束", "接下来开始正式观察，共三处。\n每次只亮一个光圈，看完再去下一处。\n松开捏取，镜子归侧，再伸指点按钮。",
                Continue, float.PositiveInfinity, "开始正式观察", true, "教学结束 · 准备开始");
            RefreshActions();
        }
        void ShowSeek()
        {
            _teaching.Hide();
            _phase = Phase.Seeking; _stableSeconds = 0;
            var topic = _lesson.topics[_index];
            PlaceTarget(ClinicalEvidenceSession.PanoramaDirection(topic.panoramaUv, _yaw));
            _targetLabel.text = $"步骤 1 · 现场观察 {_index + 1}/3 · {topic.title}\n镜片对准小光圈，上方大窗看细节";
            _dialogue.Hide(); PlaceCue(); UpdateDirections();
        }
        string SeekInstructions => _phase == Phase.Tutorial
            ? "拇指食指捏住手柄，自动开始练习。\n镜片对准小光圈，上方大窗看细节。\n松手保留画面；也可轻触完成教程。"
            : "镜片对准小光圈，上方大窗看细节。\n稳住等进度满，再轻触「查看图解」。\n松手镜子归侧，取景画面仍会保留。";
        void ShowHelp()
        {
            if (!Usable) return;
            PresentHelp();
        }
        void PresentHelp()
        {
            if (ExplainingNow)
            {
                _teaching.Show(_index, _lesson.topics[_index], ContinueLabel);
                RefreshActions(); return;
            }
            string title = Formal ? _lesson.topics[_index].title : "放大镜怎么用";
            string body = _phase == Phase.Observed
                ? "观察已完成，可继续移动镜片看细节。\n松手保留画面，镜子停回侧边。\n准备好后轻触「查看图解」学习方法。"
                : _phase == Phase.Practice
                ? "镜片对准小光圈，上方大窗看细节。\n稳住等进度满，轻微偏移不用重来。\n也可轻触「完成教程」，直接结束练习。"
                : SeekInstructions;
            Action action = _phase == Phase.Ready || _phase == Phase.Practice || _phase == Phase.Observed ? Continue : BeginOrResume;
            var label = _phase == Phase.Observed ? "查看图解" : _phase == Phase.Practice ? "完成教程" : _phase == Phase.Ready ? "开始正式观察" : _phase == Phase.Tutorial ? "开始练习" : "继续观察";
            _dialogue.ShowObservation(_index, title, body, action, float.PositiveInfinity, label, true,
                Formal ? $"正式观察 {_index + 1:00} / 03 · {title}" : "操作教学 · 先学会，再开始");
            _registration.Invalidate(); RefreshActions();
        }
        void BeginOrResume()
        {
            if (!Usable) return;
            if (_phase == Phase.Tutorial) BeginPractice();
            else { _dialogue.Hide(); _registration.Invalidate(); RefreshActions(); }
        }
        void UpdateDirections()
        {
            var look = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up);
            var target = Vector3.ProjectOnPlane(_targetSurface.transform.position - _viewer.position, Vector3.up);
            var angle = Vector3.SignedAngle(look, target, Vector3.up);
            int nearest = 0; float best = float.NegativeInfinity;
            for (int i = 0; i < _directions.Length; i++)
            {
                var toGuide = Vector3.ProjectOnPlane(_directionSurfaces[i].transform.position - _viewer.position, Vector3.up).normalized;
                var score = Vector3.Dot(look.normalized, toGuide);
                if (score > best) { best = score; nearest = i; }
            }
            for (int i = 0; i < _directions.Length; i++)
            {
                bool visible = Seeking && !_dialogue.IsVisible && Mathf.Abs(angle) > 30 && i == nearest;
                _directionSurfaces[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var turn = angle < 0 ? "向左转" : "向右转";
                _directions[i].text = $"{StageLabel}\n{turn}，寻找唯一的小光圈";
                _directionSurfaces[i].Restore();
            }
        }
        string StageLabel => Formal ? $"正式观察 {_index + 1}/3 · {_lesson.topics[_index].title}" : "操作练习 · 不计入进度";
        string ContinueLabel => _index == 2 ? (_reviewing?"结束回看 · 返回房间":"完成观察 · 自动收起") : "下一处：" + _lesson.topics[_index + 1].title;
        void Recall()
        {
            if (!Usable || _token.IsHeld) return;
            PlaceToken();
        }
        void PlaceToken()
        {
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            _token.TryRecall(new Pose(_viewer.position + forward * .43f - Vector3.up * .18f, Quaternion.LookRotation(forward)));
        }
        Pose RestPose()
        {
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            return new Pose(_viewer.position + forward * .38f - right * .30f - Vector3.up * .30f, Quaternion.LookRotation(forward));
        }
        void ShowExplanation()
        {
            _phase = Phase.Explanation; UpdateDirections();
            _targetSurface.gameObject.SetActive(false);
            var topic = _lesson.topics[_index];
            _dialogue.Hide();
            _teaching.Show(_index, topic, ContinueLabel);
            PlaceCue(); RefreshActions();
        }
        void MarkObserved()
        {
            _phase = Phase.Observed;
            _targetLabel.text = $"现场观察 {_index + 1}/3 · {_lesson.topics[_index].title}\n已对准完成，可继续查看细节";
            _progressStatus.text = "观察进度 100%\n松手保留画面，不急着翻页\n轻触「查看图解」进入教学";
            PlaceCue(); UpdateDirections();
        }
        void LookBack()
        {
            if (!Usable || !ExplainingNow) return;
            _teaching.Hide(); RefreshActions();
        }
        void PlaceCue()
        {
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            var position = _viewer.position + forward * .55f + Vector3.Cross(Vector3.up, forward) * .26f - Vector3.up * .24f;
            _cueSurface.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _viewer.position, Vector3.up));
            _cueSurface.transform.localScale = Vector3.one * .00045f; _cueSurface.Pin();
            _cue.text = _phase == Phase.Observed ? "步骤 1 完成 · 可继续看细节\n松手保留画面，镜子自动归侧\n准备好后，轻触「查看图解」"
                : ExplainingNow ? $"{StageLabel} · 已观察\n可继续取景，或重看图解\n读完后轻触下方继续"
                : _phase == Phase.Ready ? "教学结束 · 准备开始\n正式观察共三处，逐个点亮\n松开捏取，再轻触「开始正式观察」"
                : "操作教学 · 放大镜\n捏住手柄 → 对准光圈 → 大窗取景\n松手保留画面，再伸指点按钮";
            _registration.Invalidate(); RefreshActions();
        }
        void RefreshActions()
        {
            bool visible = !_dialogue.IsVisible && !_teaching.IsVisible;
            _skip.gameObject.SetActive(visible && _phase == Phase.Seeking);
            _skip.GetComponentInChildren<TMP_Text>().text = Formal ? "跳过此处" : "跳过练习";
            _help.gameObject.SetActive(visible);
            _help.GetComponentInChildren<TMP_Text>().text = ExplainingNow ? "重看图解" : "操作说明";
            _recall.gameObject.SetActive(visible && !_token.IsHeld);
            _advance.gameObject.SetActive(visible && _phase != Phase.Seeking);
            _advance.GetComponentInChildren<TMP_Text>().text = _phase == Phase.Observed ? "查看图解" : ExplainingNow ? ContinueLabel : _phase == Phase.Practice ? "完成教程" : _phase == Phase.Ready ? "开始正式观察" : "开始练习";
            _cueSurface.gameObject.SetActive(visible);
        }
        void Continue()
        {
            if (!Usable) return;
            if (_phase == Phase.Tutorial) { BeginPractice(); return; }
            if (_phase == Phase.Practice) { _registration.Invalidate(); ReadyForObservation(); return; }
            if (_phase == Phase.Ready) { _saved?.Begin();_registration.Invalidate(); ShowSeek(); return; }
            if (_phase == Phase.Observed) { _registration.Invalidate(); ShowExplanation(); return; }
            if (!ExplainingNow) return;
            if(_reviewing)
            {
                if(_index==2){Finish();return;}
                _index++;ShowExplanation();return;
            }
            if(_readOnly?.Invoke()==true)return;
            _saved?.Record(_index,false);CompletedCount++; Next();
        }
        void Skip()
        {
            if (!Usable || !Seeking) return;
            if(_readOnly?.Invoke()==true || _reviewing)return;
            if (_phase == Phase.Practice) { ReadyForObservation(); return; }
            _saved?.Record(_index,true);SkippedCount++; Next();
        }
        void Next()
        {
            _registration.Invalidate(); _dialogue.Hide();
            if (_index == 2) { Finish(); return; }
            _index++; ShowSeek();
        }
        void Finish()
        {
            _phase = Phase.Finished;
            _teaching.Hide();
            // Explicit finish also dismisses the tool. Cancel the SDK grasp before
            // hiding it; never interpret this cleanup as a user release/observation.
            _token.Dismiss();
            _cueSurface.gameObject.SetActive(false); _targetSurface.gameObject.SetActive(false); UpdateDirections();
            if(_reviewing)_closeReview?.Invoke();else _complete?.Invoke();
        }
        bool LensInsideHalo(Vector3 ray)
        {
            var target = _targetSurface.transform;
            var denominator = Vector3.Dot(ray, target.forward);
            if (denominator <= .001f) return false;
            var distance = Vector3.Dot(target.position - _viewer.position, target.forward) / denominator;
            if (distance <= 0) return false;
            var point = _viewer.position + ray * distance;
            return Vector3.Distance(point, target.position) <= HaloRadius * .81f;
        }
        void SetLiveCenter(Vector3 direction)
        {
            direction = Quaternion.Euler(0, -_yaw, 0) * direction.normalized;
            _liveProjection.SetVector("_ViewCenter", new Vector4(Mathf.Atan2(direction.x, direction.z) / (2 * Mathf.PI) + .5f,
                Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) / Mathf.PI + .5f, 0, 0));
        }
        void UpdateLiveView(bool viewable, Vector3 ray)
        {
            bool visible = (Seeking || _phase == Phase.Observed || ExplainingNow)
                && !_dialogue.IsVisible && !_teaching.IsVisible;
            _targetSurface.gameObject.SetActive(visible);
            _halo.gameObject.SetActive(Seeking);
            if (!visible) return;
            if (viewable)
            {
                // Match the physical lens centre's intersection with the fixed panorama sphere.
                var offset = _viewer.position - _entry;
                var b = Vector3.Dot(offset, ray);
                var radius = PanoramaDefinition.RadiusMetres;
                var distance = -b + Mathf.Sqrt(Mathf.Max(0, b * b - offset.sqrMagnitude + radius * radius));
                SetLiveCenter(offset + ray * distance);
                _hasLiveFrame = true;
            }
            _liveView.gameObject.SetActive(true);
            _liveStatus.text = viewable ? "实时取景 · 移动镜片查看周边" : _hasLiveFrame
                ? "画面已暂停 · 举起镜片继续取景" : "目标区域预览 · 捏起手柄开始取景";
        }
        public void Tick(float seconds)
        {
            if (!Usable) return;
            // Taking the offered physical tool is itself an explicit start action.
            // Never leave a held magnifier behind a modal "start" prerequisite.
            if (_phase == Phase.Tutorial && _token.IsHeld) BeginPractice();
            _dialogue.Tick(seconds); _teaching.Tick(); _cueSurface.Restore(); _targetSurface.Restore();
            var delta = _token.LensCenter - _viewer.position;
            var ray = delta.normalized;
            var viewable = _token.IsHeld && delta.magnitude >= .18f && delta.magnitude <= .70f
                && Vector3.Dot(_viewer.forward, ray) >= Mathf.Cos(25 * Mathf.Deg2Rad)
                && Mathf.Abs(Vector3.Dot(_token.LensNormal, ray)) >= Mathf.Cos(40 * Mathf.Deg2Rad);
            _token.SetLensActive(viewable);
            if (Seeking)
            {
                var onTarget = viewable && LensInsideHalo(ray) && !_dialogue.IsVisible;
                var deltaSeconds = Mathf.Clamp(seconds, 0, .05f);
                if (onTarget)
                {
                    _practiceMissSeconds = 0;
                    _stableSeconds = Mathf.Min(ObserveSeconds, _stableSeconds + deltaSeconds);
                }
                else if (_phase == Phase.Practice && viewable && !_dialogue.IsVisible)
                {
                    // Pause rather than erase progress for a brief physical hand tremor.
                    // Time outside the small circle never counts towards completion.
                    _practiceMissSeconds += deltaSeconds;
                    if (_practiceMissSeconds > PracticeJitterGraceSeconds) _stableSeconds = 0;
                }
                else { _stableSeconds = _practiceMissSeconds = 0; }
                _progress.sizeDelta = new Vector2(400 * ObservationProgress, 24);
                string hint = !_token.IsHeld ? "拇指食指捏住手柄，拿起放大镜"
                    : !viewable ? "举到眼前，镜面朝向眼睛"
                    : !onTarget ? "让小光圈落在镜片中央"
                    : "已对准，稳住片刻 · " + Mathf.FloorToInt(ObservationProgress * 100) + "%";
                var resultHint = _phase == Phase.Practice ? "可随时轻触下方「完成教程」" : "进度满后，轻触「查看图解」";
                var alignmentHint = _phase == Phase.Practice
                    ? "对准后保持约1秒，轻微偏移暂留进度"
                    : "对准后保持约1秒，移开需重新保持";
                _progressStatus.text = !onTarget
                    ? $"{hint}\n{alignmentHint}\n{resultHint}"
                    : $"保持不动 · {Mathf.FloorToInt(ObservationProgress * 100)}%\n黄色进度条正在填满\n{resultHint}";
                _cue.text = $"{StageLabel}\n{hint}\n" + (_phase == Phase.Practice
                    ? "也可轻触「完成教程」，无需放下"
                    : "上方大窗看细节；松手保留画面");
                if (_stableSeconds >= ObserveSeconds)
                {
                    if (_phase == Phase.Practice) ReadyForObservation(); else MarkObserved();
                }
            }
            UpdateLiveView(viewable, ray); UpdateDirections(); RefreshActions();
        }
        public void InvalidateInput() { _registration.Invalidate(); _stableSeconds = _practiceMissSeconds = 0; _token.SetLensActive(false); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _token.Dismiss(); _registration.Dispose(); _dialogue.Dispose(); _teaching.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(_liveProjection);
            else UnityEngine.Object.DestroyImmediate(_liveProjection);
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
