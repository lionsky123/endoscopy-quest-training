using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    /// <summary>Lightweight ink-and-gold geometry; shares the existing gaze channel and never moves its hit area.</summary>
    public sealed class DialogueContractGraphic : MaskableGraphic
    {
        bool _choice;
        float _progress;
        Color _gold;

        public void Initialize(Color gold, bool choice)
        {
            _gold = gold;
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
            var gold = _gold;
            gold.a = _choice ? (_progress > 0f ? 1f : .55f) : .65f;
            var bottom = new Color(.018f, .044f, .037f, .98f);
            var top = _choice ? new Color(.06f, .13f, .105f, .98f) : new Color(.035f, .09f, .072f, .98f);
            if (_progress > 0f) top = Color.Lerp(top, new Color(.16f, .26f, .19f, 1f), .7f);
            Quad(vh, new Vector2(r.xMin,r.yMin), new Vector2(r.xMax,r.yMax), bottom, top);
            var inset = _choice ? 2f : 9f;
            var a = new Vector2(r.xMin+inset,r.yMin+inset);
            var b = new Vector2(r.xMax-inset,r.yMax-inset);
            Line(vh,a,new Vector2(b.x,a.y),gold,1f);
            Line(vh,new Vector2(a.x,b.y),b,gold,1f);
            Line(vh,a,new Vector2(a.x,b.y),gold,1f);
            Line(vh,new Vector2(b.x,a.y),b,gold,1f);
            if (_choice)
            {
                var center = new Vector2(r.xMin+20f,r.center.y);
                Diamond(vh,center,4f,gold);
                if (_progress > 0f)
                    Line(vh,a,new Vector2(Mathf.Lerp(a.x,b.x,_progress),a.y),_gold,3f);
            }
            else
            {
                // Sparse botanical engraving along the frame, outside the reading region.
                foreach (var side in new[] {-1f,1f})
                {
                    var x = side < 0 ? a.x+12f : b.x-12f;
                    Line(vh,new Vector2(x,a.y+22f),new Vector2(x,a.y+93f),gold,.8f);
                    for(var i=0;i<4;i++)
                    {
                        var y=a.y+35f+i*15f;
                        Line(vh,new Vector2(x,y),new Vector2(x+side*8f,y+9f),gold,1f);
                        Line(vh,new Vector2(x,y+6f),new Vector2(x-side*7f,y+14f),gold,1f);
                    }
                    Diamond(vh,new Vector2(x,b.y-12f),4f,gold);
                }
                var grain = new Color(.7f,.65f,.42f,.025f);
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
