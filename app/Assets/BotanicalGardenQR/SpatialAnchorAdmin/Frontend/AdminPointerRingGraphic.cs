using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Authoring
{
    /// <summary>A non-interactive UI annulus rendered by the Canvas it annotates.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AdminPointerRingGraphic : Graphic
    {
        const int SegmentCount = 48;
        const float InnerRadiusRatio = 14f / 18f;

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera) => false;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = rectTransform.rect;
            var outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outerRadius <= Mathf.Epsilon) return;

            var innerRadius = outerRadius * InnerRadiusRatio;
            var center = rect.center;
            var tint = (Color32)color;
            for (var index = 0; index < SegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / SegmentCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.AddVert(center + direction * outerRadius, tint, direction * 0.5f + Vector2.one * 0.5f);
                vertices.AddVert(center + direction * innerRadius, tint, direction * 0.5f + Vector2.one * 0.5f);
            }

            for (var index = 0; index < SegmentCount; index++)
            {
                var outer = index * 2;
                var inner = outer + 1;
                var nextOuter = (index + 1) % SegmentCount * 2;
                var nextInner = nextOuter + 1;
                vertices.AddTriangle(outer, nextOuter, nextInner);
                vertices.AddTriangle(outer, nextInner, inner);
            }
        }
    }
}
