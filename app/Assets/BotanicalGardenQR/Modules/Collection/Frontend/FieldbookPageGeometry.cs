using System;
using UnityEngine;

namespace BotanicalGardenQR.Collection.Frontend
{
    /// <summary>Shared authoring geometry for a thick, gently curved collectible page.</summary>
    public static class FieldbookPageGeometry
    {
        public static Mesh Create(float width, float height, float thickness, float curl, int segments = 12)
        {
            if (!Positive(width) || !Positive(height) || !Positive(thickness) ||
                !Finite(curl) || Mathf.Abs(curl) > width * .25f || segments < 2 || segments > 64)
                throw new ArgumentOutOfRangeException(nameof(width), "Invalid fieldbook page geometry.");
            var vertices = new Vector3[(segments + 1) * 4];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 24 + 12];
            for (var i = 0; i <= segments; i++)
            {
                var u = (float)i / segments;
                var x = (u - .5f) * width;
                var z = curl * (2f * u - 1f) * (2f * u - 1f);
                var k = i * 4;
                vertices[k] = new Vector3(x, -height / 2, z - thickness / 2);
                vertices[k + 1] = new Vector3(x, height / 2, z - thickness / 2);
                vertices[k + 2] = new Vector3(x, -height / 2, z + thickness / 2);
                vertices[k + 3] = new Vector3(x, height / 2, z + thickness / 2);
                uv[k] = uv[k + 2] = new Vector2(u, 0);
                uv[k + 1] = uv[k + 3] = new Vector2(u, 1);
            }
            var at = 0;
            void Quad(int a, int b, int c, int d)
            {
                triangles[at++] = a; triangles[at++] = b; triangles[at++] = c;
                triangles[at++] = a; triangles[at++] = c; triangles[at++] = d;
            }
            for (var i = 0; i < segments; i++)
            {
                var k = i * 4;
                Quad(k, k + 1, k + 5, k + 4);
                Quad(k + 2, k + 6, k + 7, k + 3);
                Quad(k, k + 4, k + 6, k + 2);
                Quad(k + 1, k + 3, k + 7, k + 5);
            }
            Quad(0, 2, 3, 1);
            var last = segments * 4;
            Quad(last, last + 1, last + 3, last + 2);
            var mesh = new Mesh { name = "FieldbookPage", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool Positive(float x) => Finite(x) && x > 0;
    }
}
