using System;
using System.IO;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEditor;
using UnityEngine;

namespace EndoscopyTheme.Editor
{
    // Explicit editor-only diagnostic. Uses the shipped renderer and never changes an XR camera.
    public static class ClinicalPanoramaProjectionCheck
    {
        public static void Capture()
        {
            var output = Environment.GetEnvironmentVariable("ENDOSCOPY_FIRST_LESSON_PREVIEW");
            Directory.CreateDirectory(output);
            var root = new GameObject("ProjectionCheck");
            var viewer = new GameObject("Viewer");
            var cameraObject = new GameObject("ProjectionCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 70;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            var resolver = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
            ((IPanoramaDefinitionSource)resolver).TryGet(new SceneId("giant_saguaro"), out var definition);
            var controller = PanoramaModuleFactory.Create(root.transform, viewer.transform);
            var session = SessionToken.CreateNew();
            controller.Open(session, definition, new PanoramaSurfaceLease(root.transform, Vector2.one, true, () => { }));
            try
            {
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                float angularError = 0;
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    var lat = Mathf.Asin(mesh.vertices[i].normalized.y) * Mathf.Rad2Deg;
                    angularError = Mathf.Max(angularError, Mathf.Abs(lat - (mesh.uv[i].y - .5f) * 180));
                }
                var left = Render(camera, new Vector3(-.032f, 0, 0), 0, output, "panorama-eye-left.png");
                var right = Render(camera, new Vector3(.032f, 0, 0), 0, output, "panorama-eye-right.png");
                double difference = 0;
                for (int i = 0; i < left.Length; i++)
                    difference += (Math.Abs(left[i].r - right[i].r) + Math.Abs(left[i].g - right[i].g) + Math.Abs(left[i].b - right[i].b)) / 765d;
                difference /= left.Length;
                int missing = 0, min = 255, max = 0;
                foreach (var pixel in left)
                {
                    if (pixel.r > 245 && pixel.g < 10 && pixel.b > 245) missing++;
                    min = Math.Min(min, pixel.g);
                    max = Math.Max(max, pixel.g);
                }
                for (int i = 0; i < 4; i++) Render(camera, Vector3.zero, i * 90, output, "panorama-view-" + i + ".png");
                bool passed = angularError < .01f && difference < .001 && missing == 0 && max - min > 20;
                string report = $"Projection check: {(passed ? "PASS" : "FAIL")}; maximum latitude error={angularError:F5} degrees; parallel-eye pixel difference={difference:F6}; expected <0.01 degrees and <0.001; missing pixels={missing}; green-channel range={max - min}.\n";
                File.WriteAllText(Path.Combine(output, "projection-check.txt"), report);
                Debug.Log(report);
                if (!passed) throw new InvalidOperationException(report);
            }
            finally
            {
                // DestroyImmediate is required in edit mode; controller's play-mode lifecycle is not invoked.
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer.sharedMaterial);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
        static Color32[] Render(Camera camera, Vector3 position, float yaw, string output, string name)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var target = new RenderTexture(1200, 900, 24);
            var pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
