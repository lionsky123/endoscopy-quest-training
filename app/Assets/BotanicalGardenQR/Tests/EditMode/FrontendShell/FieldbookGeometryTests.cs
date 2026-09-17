using System;
using System.Linq;
using BotanicalGardenQR.Collection.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class FieldbookGeometryTests
    {
        [Test]
        public void PageHasFiniteClosedGeometryWithFrontBackAndUsableUvs()
        {
            var mesh = FieldbookPageGeometry.Create(.20f, .28f, .006f, .008f);
            try
            {
                Assert.That(mesh.bounds.size.x, Is.EqualTo(.20f).Within(.0001f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(.28f).Within(.0001f));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(.006f));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.All(uv => uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1), Is.True);
                Assert.That(mesh.normals.Any(n => n.z < -.5f), Is.True);
                Assert.That(mesh.normals.Any(n => n.z > .5f), Is.True);
                var edges = new System.Collections.Generic.Dictionary<(int, int), int>();
                var triangles = mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                    for (var j = 0; j < 3; j++)
                    {
                        var a = triangles[i + j]; var b = triangles[i + (j + 1) % 3];
                        var key = (Math.Min(a, b), Math.Max(a, b));
                        edges.TryGetValue(key, out var count); edges[key] = count + 1;
                    }
                Assert.That(edges.Values, Has.All.EqualTo(2), "Every physical page edge must be closed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void InvalidGeometryIsRejectedBeforeAllocatingAnAsset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FieldbookPageGeometry.Create(float.NaN, .28f, .006f, .008f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FieldbookPageGeometry.Create(.2f, .28f, 0, .008f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FieldbookPageGeometry.Create(.2f, .28f, .006f, 1));
        }
    }
}
