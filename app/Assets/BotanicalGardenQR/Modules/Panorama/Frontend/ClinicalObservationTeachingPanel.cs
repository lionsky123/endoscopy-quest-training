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
    // A readable photograph of the actual observed scene, with a fixed projection.
    // It shares neither the live magnifier material nor the old quiz/case state.
    internal sealed class ClinicalObservationTeachingPanel : IDisposable
    {
        readonly GameObject _root;
        readonly ClinicalWorldSurface _surface;
        readonly Transform _viewer;
        readonly Material _projection;
        readonly TMP_Text _title, _copy, _guide;
        readonly Button _continue;
        readonly IFrontendGazeSurfaceRegistration _registration;
        int _topic = -1;
        bool _disposed;
        public bool IsVisible => _root.activeSelf;

        public ClinicalObservationTeachingPanel(Transform parent, Transform viewer, TMP_FontAsset font,
            IFrontendGazeSurfaceRegistry registry, Texture panorama, Action next, Action lookBack)
        {
            _viewer = viewer;
            _root = new GameObject("ObservationPictureTeaching", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _surface = _root.AddComponent<ClinicalWorldSurface>();
            var board = (RectTransform)_root.transform; board.sizeDelta = new Vector2(760, 740);
            Fill(board, Surface);
            _title = Label(board, font, "PictureTeachingTitle", 0, 334, 710, 40, 28); _title.color = Accent;
            _guide = Label(board, font, "PictureTeachingGuide", 0, 290, 710, 30, 21);
            var photo = Rect(board, "ObservedScenePhoto", 0, 65, 710, 400).gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false; photo.texture = panorama;
            var shader = Resources.Load<Shader>("ClinicalEvidencePerspective");
            if (!shader) throw new InvalidOperationException("Missing observation photo projection.");
            _projection = new Material(shader) { name = "ObservedSceneTeachingPhoto" };
            _projection.SetFloat("_Aspect", 710f / 400);
            _projection.SetFloat("_TanHalfFov", Mathf.Tan(19 * Mathf.Deg2Rad));
            photo.material = _projection;
            var marker = Rect(photo.transform, "ObservedSpotMarker", 0, 0, 112, 112).gameObject.AddComponent<ClinicalHaloGraphic>();
            marker.TransparentCenter = true; marker.raycastTarget = false;
            _copy = Label(board, font, "PictureTeachingBody", 0, -214, 710, 128, 27);
            _continue = ClinicalPanelStyle.Button(board, font, "ContinuePictureTeaching", "下一处", 150, -320, 390, 68,
                () => { if (!_disposed && _root.activeInHierarchy) next(); }, true);
            ClinicalPanelStyle.Button(board, font, "LookBackAtScene", "回看现场", -226, -320, 280, 68,
                () => { if (!_disposed && _root.activeInHierarchy) lookBack(); });
            ClinicalNearTouch.Bind(board, () => !_disposed && _root.activeInHierarchy);
            _registration = registry.RegisterGazeSurface(board, 500, "ObservationPictureTeaching");
            _root.SetActive(false);
        }
        public void Show(int index, ClinicalEvidenceTopic topic, string continueLabel)
        {
            if (_disposed) return;
            _registration.Invalidate();
            _title.text = $"步骤 2 · 图片教学 {index + 1}/3 · {topic.title}";
            _guide.text = index == 2
                ? "圈内是刚才观察的位置 · 读完后轻触完成观察"
                : "圈内是刚才观察的位置 · 读完图解后轻触下一处";
            _copy.text = topic.method;
            _projection.SetVector("_ViewCenter", new Vector4(topic.panoramaUv.x, topic.panoramaUv.y, 0, 0));
            _continue.GetComponentInChildren<TMP_Text>().text = continueLabel;
            if (_topic != index)
            {
                var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
                var position = _viewer.position + forward * .65f + Vector3.Cross(Vector3.up, forward) * .29f - Vector3.up * .12f;
                _root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _viewer.position));
                _root.transform.localScale = Vector3.one * .00065f;
                _surface.Pin(); _topic = index;
            }
            _root.SetActive(true); _surface.Restore();
        }
        public void Hide() { _registration.Invalidate(); _root.SetActive(false); }
        public void Tick() { if (IsVisible) _surface.Restore(); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _registration.Dispose();
            if (Application.isPlaying) { UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_projection); }
            else { UnityEngine.Object.DestroyImmediate(_root); UnityEngine.Object.DestroyImmediate(_projection); }
        }
    }
}
