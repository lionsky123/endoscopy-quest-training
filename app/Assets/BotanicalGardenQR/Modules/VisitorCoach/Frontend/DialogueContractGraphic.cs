using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    /// <summary>Opaque reading surface with distinct tinted choices; hit geometry stays fixed.</summary>
    public sealed class DialogueContractGraphic : MaskableGraphic
    {
        bool _choice;
        bool _primaryAction;
        float _progress;
        Color _surface;
        Color _accent;
        VisitorDialogueButtonVisualState _interactionState;

        internal float CurrentProgress => _progress;

        public void Initialize(Color surface, Color accent, bool choice, bool primaryAction = false)
        {
            _surface = surface;
            _accent = accent;
            _choice = choice;
            _primaryAction = choice && primaryAction;
            raycastTarget = choice;
            SetVerticesDirty();
        }

        public void PresentGazeProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (Mathf.Approximately(_progress, progress)) return;
            _progress = progress;
            SetVerticesDirty();
        }

        internal void PresentInteractionState(VisitorDialogueButtonVisualState state)
        {
            if (_interactionState == state) return;
            _interactionState = state;
            _progress = state switch
            {
                VisitorDialogueButtonVisualState.Approaching => .5f,
                VisitorDialogueButtonVisualState.Pressed => .78f,
                VisitorDialogueButtonVisualState.Triggered => 1f,
                _ => 0f
            };
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            _progress = 0f;
            _interactionState = VisitorDialogueButtonVisualState.Resting;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var visualOffset = _interactionState == VisitorDialogueButtonVisualState.Pressed ? -2f :
                _interactionState == VisitorDialogueButtonVisualState.Triggered ? -1f : 0f;
            r.y += visualOffset;
            var baseSurface = _primaryAction ? _accent : _choice ? Color.Lerp(_surface, _accent, .12f) : _surface;
            var radius = _choice ? 12f : 20f;
            var shadowRect = new Rect(r.x + 2f, r.y - 7f, r.width - 4f, r.height);
            RoundedPanel(vh, shadowRect, radius, new Color(.06f,.04f,.12f,.20f), new Color(.06f,.04f,.12f,.08f));
            RoundedPanel(vh,new Rect(r.x-1.5f,r.y-1.5f,r.width+3f,r.height+3f),radius+1.5f,
                new Color(.46f,.42f,.58f,.72f),new Color(.65f,.60f,.73f,.60f));
            var bottom = Color.Lerp(baseSurface, Color.black, _choice ? .085f : .025f);
            var top = Color.Lerp(baseSurface, Color.white, _choice ? .065f : .045f);
            if (_progress > 0f) top = Color.Lerp(top, Color.white, _progress * (_primaryAction ? .13f : .06f));
            bottom.a = top.a = .995f;
            RoundedPanel(vh, r, radius, bottom, top);
            var accent = _primaryAction ? Color.Lerp(_accent, Color.white, .55f) : _accent;
            accent.a = _choice ? .56f : .38f;
            var a = new Vector2(r.xMin + radius, r.yMin + 2f);
            var b = new Vector2(r.xMax - radius, r.yMax - 2f);
            Line(vh, new Vector2(a.x,b.y), new Vector2(b.x,b.y),
                new Color(1f,1f,1f,_choice ? .28f : .72f), _choice ? 1.5f : 2f);
            Line(vh, new Vector2(a.x,r.yMin+2f), new Vector2(b.x,r.yMin+2f),accent,1.5f);
            if (_choice)
            {
                if (_progress > 0f)
                    Line(vh,new Vector2(a.x,r.yMin+3f),new Vector2(Mathf.Lerp(a.x,b.x,_progress),r.yMin+3f),
                        _primaryAction ? Color.white : _accent,3f);
            }
        }

        static void RoundedPanel(VertexHelper vh, Rect rect, float radius, Color bottom, Color top)
        {
            radius = Mathf.Min(radius, rect.width * .5f, rect.height * .5f);
            const int segments = 6;
            var corners = new[]
            {
                new Vector2(rect.xMin + radius, rect.yMax - radius),
                new Vector2(rect.xMax - radius, rect.yMax - radius),
                new Vector2(rect.xMax - radius, rect.yMin + radius),
                new Vector2(rect.xMin + radius, rect.yMin + radius)
            };
            var first = vh.currentVertCount;
            vh.AddVert(rect.center, Color.Lerp(bottom, top, .5f), Vector2.zero);
            for (var corner = 0; corner < 4; corner++)
                for (var step = 0; step <= segments; step++)
                {
                    var angle = (180f - corner * 90f - step * 90f / segments) * Mathf.Deg2Rad;
                    var point = corners[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    var tint = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                    vh.AddVert(point, tint, Vector2.zero);
                }
            var count = 4 * (segments + 1);
            for (var i = 0; i < count; i++)
            {
                vh.AddTriangle(first, first + i + 1, first + (i + 1) % count + 1);
            }
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            var direction=(b-a).normalized;
            var normal=new Vector2(-direction.y,direction.x)*width*.5f;
            int n=vh.currentVertCount;
            vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero);
            vh.AddVert(b+normal,color,Vector2.zero); vh.AddVert(b-normal,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
