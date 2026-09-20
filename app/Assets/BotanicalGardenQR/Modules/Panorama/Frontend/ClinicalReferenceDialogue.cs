using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.VisitorCoach.Frontend;
using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Frontend
{
    // Reuse the reference Visitor dialogue prefab, layout, portraits and real poke targets.
    // A scoped instance prevents the global navigation dialogue from overlapping a lesson.
    internal sealed class ClinicalReferenceDialogue : IDisposable
    {
        readonly VisitorCoachPresenter _presenter;
        readonly ClinicalWorldSurface _surface;
        readonly Transform _viewer;
        VisitorDialogueContextId _context;
        float _elapsed, _visibleSeconds = 8;
        Action _action;
        public bool IsVisible => _presenter.gameObject.activeSelf;
        public ClinicalReferenceDialogue(Transform parent, Transform viewer, TMP_FontAsset font, IFrontendGazeSurfaceRegistry surfaces)
        {
            _viewer = viewer;
            var theme = Resources.Load<VisitorCoachThemeAsset>("ClinicalEvidence/ReferenceDialogueTheme");
            var prefab = theme ? theme.PresentationPrefab : null;
            if (!theme || !prefab) throw new InvalidOperationException("Missing reference fairy dialogue resources.");
            _presenter = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<VisitorCoachPresenter>();
            _presenter.name = "ClinicalReferenceFairyDialogue";
            _presenter.Configure(viewer, theme, font, surfaces);
            _presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
            _presenter.IntentRequested += OnIntent;
            _surface = _presenter.gameObject.AddComponent<ClinicalWorldSurface>();
        }
        public void Show(int index, string title, Vector3 target)
        {
            _action = null;
            _elapsed = 0; _visibleSeconds = 8; _context = new VisitorDialogueContextId("clinical-observation-" + index);
            var direction = target - _viewer.position;
            var angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(_viewer.forward, Vector3.up), Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
            var turn = Mathf.Abs(angle) < 20 ? "光圈就在前方。" : angle < 0 ? "请向左转身，寻找发光圆环。" : "请向右转身，寻找发光圆环。";
            _presenter.Present(new VisitorDialogueSurfaceState(index, _context, VisitorDialogueOwner.Coach,
                VisitorDialogueSurfaceMode.Dialogue, $"观察点 {index + 1:00} / 03", "小精灵",
                $"一起观察「{title}」。\n{turn}\n伸手轻触光圈，就能打开图片任务。", 0, 1,
                primaryActionLabel: "知道了 · 找光圈", allowRestart: false));
            ClinicalWorldSurface.PlaceForHands(_presenter.transform, _viewer, .00065f); _surface.Pin();
        }
        public void ShowObservation(int index, string title, string body, Action action, float visibleSeconds = 14,
            string actionLabel = "跳过当前观察", bool besideTool = false, string stageLabel = null)
        {
            _elapsed = 0; _visibleSeconds = visibleSeconds; _action = action;
            _context = new VisitorDialogueContextId("clinical-guided-" + index + "-" + body.GetHashCode());
            _presenter.Present(new VisitorDialogueSurfaceState(index, _context, VisitorDialogueOwner.Coach,
                VisitorDialogueSurfaceMode.Dialogue, stageLabel ?? $"房间观察 {index + 1:00} / 03 · {title}", "小精灵",
                body, 0, 1, primaryActionLabel: actionLabel, allowRestart: false));
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            var position = _viewer.position + forward * (besideTool ? .65f : .55f) - Vector3.up * .25f;
            if (besideTool) position += Vector3.Cross(Vector3.up, forward) * .24f;
            _presenter.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _viewer.position, Vector3.up));
            _presenter.transform.localScale = Vector3.one * .00065f; _surface.Pin();
        }
        void OnIntent(VisitorDialogueIntent intent) { if (intent.Context == _context) { if (_action != null) _action(); else Hide(); } }
        public void Hide() { _presenter.Hide(_context); _presenter.Tick(1); }
        public void Tick(float delta)
        {
            _elapsed += Mathf.Max(0, delta);
            if (_elapsed >= _visibleSeconds) Hide();
            _presenter.Tick(delta); _surface.Restore();
        }
        public void Dispose()
        {
            _presenter.IntentRequested -= OnIntent; _presenter.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(_presenter.gameObject); else UnityEngine.Object.DestroyImmediate(_presenter.gameObject);
        }
    }
}
