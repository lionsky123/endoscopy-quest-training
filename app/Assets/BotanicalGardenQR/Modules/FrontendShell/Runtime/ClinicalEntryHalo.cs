using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public static class ClinicalEntryHalo
    {
        public static Button Create(Transform parent, TMP_FontAsset font, string name, Action enter)
        {
            var rect = Rect(parent, name, 0, 0, 314, 314);
            var halo = rect.gameObject.AddComponent<ClinicalHaloGraphic>(); halo.color = new Color(.82f, .95f, .9f, 1);
            halo.raycastTarget = false;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = halo;
            button.onClick.AddListener(() => enter());
            var label = Label(rect, font, "TouchHint", 0, 0, 200, 80, 36); label.text = "轻触"; label.alignment = TextAlignmentOptions.Center;
            ClinicalNearTouch.Bind(button.transform);
            return button;
        }
    }
    // A circular light surface; rounded-rectangle sprites clamp large radii into a square.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ClinicalHaloGraphic : MaskableGraphic
    {
        public bool TransparentCenter { get; set; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = rectTransform.rect; float radius = Mathf.Min(rect.width, rect.height) * .5f;
            if (!TransparentCenter) Ring(mesh, rect.center, 0, radius * .76f, new Color(.025f, .19f, .17f, .8f) * color);
            Ring(mesh, rect.center, radius * .81f, radius * .87f, new Color(.35f, 1f, .83f, 1) * color);
            Ring(mesh, rect.center, radius * .97f, radius, new Color(.35f, 1f, .83f, .4f) * color);
        }
        static void Ring(VertexHelper mesh, Vector2 center, float inner, float outer, Color tint)
        {
            const int segments = 96;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                var start = mesh.currentVertCount;
                var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                mesh.AddVert(center + from * inner, tint, Vector2.zero); mesh.AddVert(center + from * outer, tint, Vector2.zero);
                mesh.AddVert(center + to * outer, tint, Vector2.zero); mesh.AddVert(center + to * inner, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
