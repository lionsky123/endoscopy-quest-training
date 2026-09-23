using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    /// <summary>Light, low-glare dialogue surface with a restrained cyan accent; hit geometry stays fixed.</summary>
    public sealed class DialogueContractGraphic : MaskableGraphic
    {
        bool _choice;
        float _progress;
        Color _surface;
        Color _accent;

        public void Initialize(Color surface, Color accent, bool choice)
        {
            _surface = surface;
            _accent = accent;
            _choice = choice;
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

        protected override void OnDisable() { _progress = 0f; base.OnDisable(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var accent = _accent;
            accent.a = _choice ? (_progress > 0f ? .95f : .62f) : .38f;
            var bottom = Color.Lerp(_surface, Color.black, .015f);
            var top = Color.Lerp(_surface, Color.white, .025f);
            bottom.a = top.a = .99f;
            if (_progress > 0f) top = Color.Lerp(top, Color.Lerp(_surface, _accent, .12f), .72f);
            Quad(vh, new Vector2(r.xMin,r.yMin), new Vector2(r.xMax,r.yMax), bottom, top);
            var inset = _choice ? 2f : 9f;
            var a = new Vector2(r.xMin+inset,r.yMin+inset);
            var b = new Vector2(r.xMax-inset,r.yMax-inset);
            Line(vh,a,new Vector2(b.x,a.y),accent,1f);
            Line(vh,new Vector2(a.x,b.y),b,accent,1f);
            Line(vh,a,new Vector2(a.x,b.y),accent,1f);
            Line(vh,new Vector2(b.x,a.y),b,accent,1f);
            if (_choice)
            {
                var center = new Vector2(r.xMin+20f,r.center.y);
                Diamond(vh,center,4f,accent);
                if (_progress > 0f)
                    Line(vh,a,new Vector2(Mathf.Lerp(a.x,b.x,_progress),a.y),_accent,3f);
            }
            else
            {
                // Sparse botanical lines stay outside the reading region.
                foreach (var side in new[] {-1f,1f})
                {
                    var x = side < 0 ? a.x+12f : b.x-12f;
                    Line(vh,new Vector2(x,a.y+22f),new Vector2(x,a.y+93f),accent,.8f);
                    for(var i=0;i<4;i++)
                    {
                        var y=a.y+35f+i*15f;
                        Line(vh,new Vector2(x,y),new Vector2(x+side*8f,y+9f),accent,1f);
                        Line(vh,new Vector2(x,y+6f),new Vector2(x-side*7f,y+14f),accent,1f);
                    }
                    Diamond(vh,new Vector2(x,b.y-12f),4f,accent);
                }
                var grain = new Color(_accent.r,_accent.g,_accent.b,.045f);
                for(var i=0;i<22;i++)
                {
                    var y=Mathf.Lerp(a.y+3f,b.y-3f,i/21f);
                    Line(vh,new Vector2(a.x+25f,y),new Vector2(a.x+95f+(i%3)*11f,y+1f),grain,.7f);
                }
            }
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color bottom, Color top)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector3(a.x,a.y),bottom,Vector2.zero);
            vh.AddVert(new Vector3(a.x,b.y),top,Vector2.zero);
            vh.AddVert(new Vector3(b.x,b.y),top,Vector2.zero);
            vh.AddVert(new Vector3(b.x,a.y),bottom,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }

        static void Diamond(VertexHelper vh, Vector2 p, float radius, Color color)
        {
            Line(vh,p+Vector2.left*radius,p+Vector2.up*radius,color,1.5f);
            Line(vh,p+Vector2.up*radius,p+Vector2.right*radius,color,1.5f);
            Line(vh,p+Vector2.right*radius,p+Vector2.down*radius,color,1.5f);
            Line(vh,p+Vector2.down*radius,p+Vector2.left*radius,color,1.5f);
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
