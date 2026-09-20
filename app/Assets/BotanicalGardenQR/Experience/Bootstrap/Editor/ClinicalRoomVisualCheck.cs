using System;
using System.IO;
using System.Linq;
using System.Text;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Same published scene lighting and production room loader; editor rendering only.
    public static class ClinicalRoomVisualCheck
    {
        public static void Capture()
        {
            var code = 1;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var output = Path.GetFullPath(args[Array.IndexOf(args, "-bgqrCaptureOutput") + 1]);
                Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity", OpenSceneMode.Single);
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
                var rig = new GameObject("Room validation rig");
                var view = new GameObject("Room validation camera", typeof(Camera));
                view.transform.SetParent(rig.transform, false);
                var cam = view.GetComponent<Camera>(); cam.fieldOfView = 75; cam.nearClipPlane = .3f; cam.farClipPlane = 60;
                var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
                using (var room = VirtualRoomEnvironment.Create(rig, null, definition))
                {
                    var root = GameObject.Find("VirtualWashingRoom");
                    var log = new StringBuilder("Android target; editor rendering; published Visitor scene lights; no player build.\n");
                    log.AppendLine($"Camera near={cam.nearClipPlane}; ambient={RenderSettings.ambientLight}");
                    var textured = root.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().Where(m => m.mainTexture).ToArray();
                    var tints = textured.Select(m => m.color).ToArray();
                    for (var p = 0; p < definition.points.Length; p++)
                    {
                        var point = definition.points[p].position;
                        view.transform.position = root.transform.TransformPoint(new Vector3(point.x, 1.6f, point.z));
                        view.transform.rotation = root.transform.rotation * Quaternion.LookRotation(new Vector3(-1, -.15f, p % 2 == 0 ? .25f : -.25f));
                        Render(cam, output, $"P0{p + 1}-room", log);
                        // Isolate the old tint bug under exactly the same lights and geometry.
                        for (var m = 0; m < textured.Length; m++) textured[m].color = Color.white;
                        Render(cam, output, $"P0{p + 1}-legacy-tint", log);
                        for (var m = 0; m < textured.Length; m++) textured[m].color = tints[m];
                    }
                    // Take an original large horizontal triangle, keeping its vertex normals, UV and material.
                    MeshFilter selected = null; int submesh = 0, offset = 0; float largest = 0;
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh = filter.sharedMesh; var vertices = mesh.vertices;
                        for (var sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var triangles = mesh.GetTriangles(sub);
                            for (var i = 0; i < triangles.Length; i += 3)
                            {
                                var cross = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                                if (Mathf.Abs(cross.normalized.y) < .95f || cross.magnitude <= largest) continue;
                                largest = cross.magnitude; selected = filter; submesh = sub; offset = i;
                            }
                        }
                    }
                    if (!selected) throw new InvalidOperationException("No room floor/ceiling triangle found.");
                    var source = selected.sharedMesh; var ids = source.GetTriangles(submesh).Skip(offset).Take(3).ToArray();
                    var positions = ids.Select(i => selected.transform.TransformPoint(source.vertices[i])).ToArray();
                    var probeMesh = new Mesh { vertices = positions, normals = ids.Select(i => selected.transform.TransformDirection(source.normals[i])).ToArray(), uv = ids.Select(i => source.uv[i]).ToArray(), triangles = new[] { 0, 1, 2 } };
                    probeMesh.RecalculateBounds();
                    var probe = new GameObject("Original room face", typeof(MeshFilter), typeof(MeshRenderer));
                    probe.GetComponent<MeshFilter>().sharedMesh = probeMesh;
                    probe.GetComponent<MeshRenderer>().sharedMaterial = selected.GetComponent<MeshRenderer>().sharedMaterials[submesh];
                    log.AppendLine($"Face source={selected.name}; area={largest / 2}; material={probe.GetComponent<MeshRenderer>().sharedMaterial.name}");
                    root.SetActive(false);
                    // Isolate the room sample from authored decorative objects while retaining real scene lights.
                    foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (renderer.gameObject != probe) renderer.enabled = false;
                    var center = (positions[0] + positions[1] + positions[2]) / 3;
                    var normal = Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0]).normalized;
                    cam.backgroundColor = Color.magenta; cam.orthographic = true;
                    cam.orthographicSize = probeMesh.bounds.size.magnitude * .6f;
                    float[] coverage = new float[2];
                    for (var side = 0; side < 2; side++)
                    {
                        view.transform.position = center + normal * (side == 0 ? 10 : -10);
                        view.transform.LookAt(center, Vector3.forward);
                        coverage[side] = Render(cam, output, side == 0 ? "original-face-front" : "original-face-back", log, true);
                    }
                    Object.DestroyImmediate(probe); Object.DestroyImmediate(probeMesh);
                    code = coverage.All(value => value > .02f) ? 0 : 2;
                    log.AppendLine(code == 0 ? "PASS: original face visible from both sides." : "FAIL: original face disappears from one side.");
                    File.WriteAllText(Path.Combine(output, "render-check.txt"), log.ToString());
                }
                Object.DestroyImmediate(rig);
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.Exit(code);
        }

        static float Render(Camera camera, string output, string name, StringBuilder log, bool checkCoverage = false)
        {
            const int width = 1200, height = 900;
            var target = new RenderTexture(width, height, 24); target.Create();
            var old = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                // Discard the first readback: newly loaded Android textures may still be
                // uploading on the editor's first render of the material set.
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                var pixels = image.GetPixels32(); int covered = 0, clipped = 0; double luminance = 0;
                foreach (var pixel in pixels)
                {
                    if (!(pixel.r > 245 && pixel.g < 10 && pixel.b > 245)) covered++;
                    if (pixel.r >= 250 && pixel.g >= 250 && pixel.b >= 250) clipped++;
                    luminance += (.2126 * pixel.r + .7152 * pixel.g + .0722 * pixel.b) / 255;
                }
                log.AppendLine($"{name}: mean_display_luminance={luminance / pixels.Length:F4}; clipped_white={(double)clipped / pixels.Length:F4}" + (checkCoverage ? $"; coverage={(float)covered / pixels.Length:F4}" : ""));
                return (float)covered / pixels.Length;
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; if (image) Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
