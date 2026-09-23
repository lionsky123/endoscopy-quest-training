using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    /// <summary>Imports and publishes the supplied cleaning-room model into the runtime room resource.</summary>
    public static class WashingRoomModelPublish
    {
        const string Source = "Assets/EndoscopyTheme/ImportedModels/Source/CleaningRoom_20260923/清洗室模型.FBX";
        const string SourceFolder = "Assets/EndoscopyTheme/ImportedModels/Source/CleaningRoom_20260923";
        const string TextureMapPath = SourceFolder + "/washing-room-texture-map.json";
        const string ResourceFolder = "Assets/EndoscopyTheme/Resources/EndoscopyRoom";
        const string ResourceTextureFolder = ResourceFolder + "/SourceTextures";
        const string GeometryPath = ResourceFolder + "/geometry.bytes";
        const string ManifestPath = ResourceFolder + "/manifest.json";
        const string MapPath = "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json";
        const int MaxBatchVertices = 120000;

        [Serializable] sealed class TextureBinding
        {
            public string materialName, assetPath, sourceFilename, sha256;
            public bool sharedFilenameFallback;
        }

        [Serializable] sealed class TextureMap { public TextureBinding[] materialBindings; }

        [Serializable] sealed class Surface
        {
            public string name, textureResource;
            public float[] color;
        }

        [Serializable] sealed class Obstacle
        {
            public Vector3 center, size;
        }

        [Serializable] sealed class RoomManifest
        {
            public Surface[] materials;
            public Obstacle[] obstacles;
            public Vector3 terminal;
        }

        [Serializable] sealed class BatchReport
        {
            public string meshName;
            public int vertices, triangles, materialIndex;
        }

        [Serializable] sealed class PublishReport
        {
            public string source, sourceSHA256, geometrySHA256, geometryPath;
            public int sourceMeshes, renderBatches, sourceObstacleBounds, materialCount, texturedMaterials, embeddedTextureFiles, sharedFilenameFallbacks;
            public int modeledDoorwayPortals;
            public float cleaningRoomLightIntensity;
            public long triangles, geometryBytes;
            public Vector3 boundsCenter, boundsSize;
            public string[] textureResources;
            public BatchReport[] batches;
        }

        [Serializable] sealed class MaterialRow
        {
            public string name, shader, texture, textureAssetPath;
            public float[] color;
        }

        [Serializable] sealed class MeshRow
        {
            public string name;
            public int vertices, triangles;
            public Vector3 center, size;
        }

        [Serializable] sealed class InspectionReport
        {
            public string source;
            public Vector3 boundsCenter, boundsSize;
            public int renderers, meshCount, materialSlots, missingMaterialSlots, untexturedSlots;
            public long triangles;
            public MeshRow[] meshes;
            public MaterialRow[] materials;
            public string[] importedTextureNames;
        }

        public static void Inspect()
        {
            var exitCode = 1;
            GameObject instance = null;
            try
            {
                RequireAndroidTarget();
                AssetDatabase.Refresh();
                var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
                if (!importer) throw new InvalidOperationException("ModelImporter missing: " + Source);
                if (importer.importAnimation || importer.importCameras || importer.importLights || !importer.isReadable)
                {
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                if (!model) throw new InvalidOperationException("FBX did not import as a model: " + Source);
                instance = Object.Instantiate(model);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("FBX has no MeshRenderer objects.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

                var filters = instance.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh).ToArray();
                var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).ToArray();
                var uniqueMaterials = materials.Where(material => material).Distinct().ToArray();
                var rows = uniqueMaterials.Select(material =>
                {
                    var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                    var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                    return new MaterialRow
                    {
                        name = material.name,
                        shader = material.shader ? material.shader.name : "",
                        texture = texture ? texture.name : "",
                        textureAssetPath = texture ? AssetDatabase.GetAssetPath(texture) : "",
                        color = new[] { color.r, color.g, color.b, color.a }
                    };
                }).ToArray();

                var meshRows = filters.Select(filter =>
                {
                    var meshBounds = filter.GetComponent<Renderer>().bounds;
                    var mesh = filter.sharedMesh;
                    return new MeshRow
                    {
                        name = filter.name,
                        vertices = mesh.vertexCount,
                        triangles = TriangleCount(mesh),
                        center = meshBounds.center,
                        size = meshBounds.size
                    };
                }).ToArray();

                var report = new InspectionReport
                {
                    source = Source,
                    boundsCenter = bounds.center,
                    boundsSize = bounds.size,
                    renderers = renderers.Length,
                    meshCount = filters.Length,
                    materialSlots = materials.Length,
                    missingMaterialSlots = materials.Count(material => !material),
                    untexturedSlots = materials.Count(material => !material || !GetBaseColorTexture(material)),
                    triangles = filters.Sum(filter => (long)TriangleCount(filter.sharedMesh)),
                    meshes = meshRows,
                    materials = rows,
                    importedTextureNames = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<Texture2D>().Select(texture => texture.name).OrderBy(name => name).ToArray()
                };

                var output = CaptureOutput();
                Directory.CreateDirectory(output);
                var path = Path.Combine(output, "washing-room-model-inspection.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log("Washing-room model inspection written: " + path + "; meshes=" + report.meshCount + ", triangles=" + report.triangles + ", materials=" + report.materials.Length + ", untextured slots=" + report.untexturedSlots + ", bounds=" + report.boundsSize);
                exitCode = 0;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (instance) Object.DestroyImmediate(instance);
                EditorApplication.Exit(exitCode);
            }
        }

        public static void Publish()
        {
            var exitCode = 1;
            GameObject container = null;
            var temporaryMeshes = new List<Mesh>();
            string geometryTemporary = null, manifestTemporary = null;
            try
            {
                RequireAndroidTarget();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureImporter();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                if (!model) throw new InvalidOperationException("FBX did not import as a model: " + Source);
                var textureMapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TextureMapPath);
                if (!textureMapAsset) throw new InvalidOperationException("Extracted FBX texture map is missing: " + TextureMapPath);
                var textureMap = JsonUtility.FromJson<TextureMap>(textureMapAsset.text);
                if (textureMap == null || textureMap.materialBindings == null || textureMap.materialBindings.Length == 0)
                    throw new InvalidDataException("No diffuse material bindings were extracted from the supplied FBX.");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                container = new GameObject("CleaningRoomModelPublishTemp");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (!visual) throw new InvalidOperationException("Could not instantiate the imported cleaning-room FBX.");
                visual.transform.SetParent(container.transform, false);
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("FBX has no MeshRenderer objects.");
                var originalBounds = BoundsOf(container);
                // Match the current room pivot without moving its surveyed floor level.
                visual.transform.localPosition -= new Vector3(originalBounds.center.x, 0f, originalBounds.center.z - .02953148f);
                var alignedBounds = BoundsOf(container);

                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var copiedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var binding in textureMap.materialBindings)
                {
                    if (copiedTextures.ContainsKey(binding.assetPath)) continue;
                    var sourceTexturePath = binding.assetPath.Replace('\\', '/');
                    if (!sourceTexturePath.StartsWith("Assets/", StringComparison.Ordinal))
                        throw new InvalidDataException("Texture path is not inside Assets: " + binding.assetPath);
                    var sourceTextureFile = ToAbsolutePath(projectRoot, sourceTexturePath);
                    if (!File.Exists(sourceTextureFile)) throw new FileNotFoundException("Extracted FBX texture is missing.", sourceTextureFile);
                    var fileName = Path.GetFileName(sourceTexturePath);
                    var targetTextureAsset = ResourceTextureFolder + "/" + fileName;
                    var targetTextureFile = ToAbsolutePath(projectRoot, targetTextureAsset);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetTextureFile));
                    File.Copy(sourceTextureFile, targetTextureFile, true);
                    var resourceName = "EndoscopyRoom/SourceTextures/" + Path.GetFileNameWithoutExtension(fileName);
                    copiedTextures.Add(sourceTexturePath, resourceName);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var textureResource in copiedTextures.Values.Distinct())
                    if (!Resources.Load<Texture2D>(textureResource))
                        throw new InvalidDataException("Copied diffuse texture did not import for Resources: " + textureResource);

                var bindingsByMaterial = new Dictionary<string, TextureBinding>(StringComparer.Ordinal);
                foreach (var binding in textureMap.materialBindings)
                {
                    if (bindingsByMaterial.TryGetValue(binding.materialName, out var previous)
                        && !string.Equals(previous.assetPath, binding.assetPath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Conflicting FBX diffuse textures for material " + binding.materialName);
                    bindingsByMaterial[binding.materialName] = binding;
                }

                var groups = new Dictionary<Material, List<CombineInstance>>();
                var materialIndices = new Dictionary<Material, int>();
                var surfaces = new List<Surface>();
                var obstacles = new List<Obstacle>();
                Bounds? doorwayWall = null, doorwayLeaf = null;
                var filters = visual.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.sharedMesh).ToArray();
                long sourceTriangles = 0;
                foreach (var filter in filters)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer) throw new InvalidOperationException("Mesh has no MeshRenderer: " + filter.name);
                    var bounds = renderer.bounds;
                    if (filter.name.Contains("[339871]"))
                    {
                        doorwayWall = bounds;
                    }
                    else if (filter.name.Contains("[349330]"))
                    {
                        doorwayLeaf = bounds;
                    }
                    else if (bounds.max.y > .15f && bounds.min.y < 1.8f)
                        obstacles.Add(new Obstacle { center = bounds.center, size = bounds.size });
                    var mesh = filter.sharedMesh;
                    sourceTriangles += TriangleCount(mesh);
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0) throw new InvalidOperationException("Mesh has no material slots: " + filter.name);
                    for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        var material = materials[Mathf.Min(submesh, materials.Length - 1)];
                        if (!material) throw new InvalidOperationException("Missing material on " + filter.name + " submesh " + submesh);
                        if (!groups.TryGetValue(material, out var combines))
                        {
                            groups.Add(material, combines = new List<CombineInstance>());
                            materialIndices.Add(material, surfaces.Count);
                            bindingsByMaterial.TryGetValue(material.name, out var binding);
                            var textureResource = "";
                            if (binding != null)
                                textureResource = copiedTextures[binding.assetPath.Replace('\\', '/')];
                            else
                            {
                                var texture = GetBaseColorTexture(material);
                                if (texture)
                                {
                                    var assetPath = AssetDatabase.GetAssetPath(texture).Replace('\\', '/');
                                    var resourceMarker = "/Resources/";
                                    var resourcesIndex = assetPath.LastIndexOf(resourceMarker, StringComparison.Ordinal);
                                    if (resourcesIndex < 0)
                                        throw new InvalidDataException("Material texture must be published explicitly: " + material.name + " -> " + assetPath);
                                    textureResource = assetPath.Substring(resourcesIndex + resourceMarker.Length);
                                    textureResource = textureResource.Substring(0, textureResource.LastIndexOf('.'));
                                }
                            }
                            var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                            surfaces.Add(new Surface
                            {
                                name = material.name,
                                textureResource = textureResource,
                                color = new[] { color.r, color.g, color.b }
                            });
                        }
                        combines.Add(new CombineInstance
                        {
                            mesh = mesh,
                            subMeshIndex = submesh,
                            transform = filter.transform.localToWorldMatrix
                        });
                    }
                }
                if (!doorwayWall.HasValue || !doorwayLeaf.HasValue)
                    throw new InvalidDataException("The supplied cleaning-room model no longer contains the expected doorway wall and leaf.");
                var wall = doorwayWall.Value;
                var leaf = doorwayLeaf.Value;
                var doorwayHalfWidth = leaf.size.x * .5f + .25f;
                var openingMin = leaf.center.x - doorwayHalfWidth;
                var openingMax = leaf.center.x + doorwayHalfWidth;
                if (openingMin > wall.min.x)
                {
                    var left = new Bounds(wall.center, wall.size);
                    left.max = new Vector3(openingMin, wall.max.y, wall.max.z);
                    obstacles.Add(new Obstacle { center = left.center, size = left.size });
                }
                if (openingMax < wall.max.x)
                {
                    var right = new Bounds(wall.center, wall.size);
                    right.min = new Vector3(openingMax, wall.min.y, wall.min.z);
                    obstacles.Add(new Obstacle { center = right.center, size = right.size });
                }

                var batches = new List<(Mesh mesh, int material)>();
                foreach (var group in groups)
                {
                    var chunk = new List<CombineInstance>();
                    var vertexCount = 0;
                    void Flush()
                    {
                        if (chunk.Count == 0) return;
                        var mesh = new Mesh
                        {
                            name = "CleaningRoomStatic_" + batches.Count,
                            indexFormat = IndexFormat.UInt32
                        };
                        mesh.CombineMeshes(chunk.ToArray(), true, true);
                        temporaryMeshes.Add(mesh);
                        batches.Add((mesh, materialIndices[group.Key]));
                        chunk.Clear();
                        vertexCount = 0;
                    }
                    foreach (var combine in group.Value)
                    {
                        if (vertexCount > 0 && vertexCount + combine.mesh.vertexCount > MaxBatchVertices) Flush();
                        chunk.Add(combine);
                        vertexCount += combine.mesh.vertexCount;
                    }
                    Flush();
                }
                if (batches.Count == 0 || surfaces.Count == 0) throw new InvalidOperationException("FBX produced no runtime geometry.");

                var mapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath);
                if (!mapAsset) throw new InvalidOperationException("Cleaning-room route map is missing: " + MapPath);
                var map = JsonUtility.FromJson<MapDefinition>(mapAsset.text);
                MapDefinitionValidation.Validate(map);
                var obstacleBounds = obstacles.Select(obstacle => new Bounds(obstacle.center, obstacle.size)).ToArray();
                _ = new VirtualRoomGuidePath(map, new MapFrame(new MapPosition(0, 0, 0), 0, map.scale), new MeshFilter[0], obstacleBounds);

                var geometryFile = ToAbsolutePath(projectRoot, GeometryPath);
                var manifestFile = ToAbsolutePath(projectRoot, ManifestPath);
                Directory.CreateDirectory(Path.GetDirectoryName(geometryFile));
                geometryTemporary = geometryFile + ".publish-tmp";
                manifestTemporary = manifestFile + ".publish-tmp";
                using (var writer = new BinaryWriter(File.Create(geometryTemporary)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("ECR1"));
                    writer.Write(batches.Count);
                    foreach (var batch in batches)
                    {
                        var mesh = batch.mesh;
                        var meshName = Encoding.UTF8.GetBytes(mesh.name);
                        writer.Write(meshName.Length);
                        writer.Write(meshName);
                        var vertices = mesh.vertices;
                        var normals = mesh.normals;
                        var uv = mesh.uv;
                        writer.Write(vertices.Length);
                        for (var index = 0; index < vertices.Length; index++)
                        {
                            var vertex = vertices[index];
                            var normal = normals.Length == vertices.Length ? normals[index] : Vector3.up;
                            var texCoord = uv.Length == vertices.Length ? uv[index] : Vector2.zero;
                            writer.Write(-vertex.x); writer.Write(vertex.y); writer.Write(-vertex.z);
                            writer.Write(-normal.x); writer.Write(normal.y); writer.Write(-normal.z);
                            writer.Write(texCoord.x); writer.Write(texCoord.y);
                        }
                        writer.Write(1);
                        writer.Write(batch.material);
                        var indices = mesh.triangles;
                        writer.Write(indices.Length);
                        foreach (var index in indices) writer.Write(index);
                    }
                }

                var geometryDigest = HashFile(geometryTemporary);
                var manifest = new RoomManifest { materials = surfaces.ToArray(), obstacles = obstacles.ToArray() };
                File.WriteAllText(manifestTemporary, JsonUtility.ToJson(manifest, true));
                ReplaceFile(geometryTemporary, geometryFile); geometryTemporary = null;
                ReplaceFile(manifestTemporary, manifestFile); manifestTemporary = null;
                map.modelDigest = geometryDigest;
                File.WriteAllText(ToAbsolutePath(projectRoot, MapPath), JsonUtility.ToJson(map, true));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();

                var output = CaptureOutput();
                Directory.CreateDirectory(output);
                var sourceFile = ToAbsolutePath(projectRoot, Source);
                var report = new PublishReport
                {
                    source = Source,
                    sourceSHA256 = HashFile(sourceFile),
                    geometrySHA256 = geometryDigest,
                    geometryPath = GeometryPath,
                    sourceMeshes = filters.Length,
                    renderBatches = batches.Count,
                    sourceObstacleBounds = obstacles.Count,
                    materialCount = surfaces.Count,
                    texturedMaterials = surfaces.Count(surface => !string.IsNullOrEmpty(surface.textureResource)),
                    embeddedTextureFiles = copiedTextures.Count,
                    sharedFilenameFallbacks = textureMap.materialBindings.Count(binding => binding.sharedFilenameFallback),
                    modeledDoorwayPortals = 1,
                    cleaningRoomLightIntensity = .77f,
                    triangles = sourceTriangles,
                    geometryBytes = new FileInfo(geometryFile).Length,
                    boundsCenter = alignedBounds.center,
                    boundsSize = alignedBounds.size,
                    textureResources = copiedTextures.Values.Distinct().OrderBy(value => value).ToArray(),
                    batches = batches.Select(batch => new BatchReport
                    {
                        meshName = batch.mesh.name,
                        vertices = batch.mesh.vertexCount,
                        triangles = TriangleCount(batch.mesh),
                        materialIndex = batch.material
                    }).ToArray()
                };
                var reportPath = Path.Combine(output, "washing-room-model-publication.json");
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log("Cleaning-room runtime model published: " + reportPath + "; meshes=" + report.sourceMeshes + ", batches=" + report.renderBatches + ", triangles=" + report.triangles + ", textured materials=" + report.texturedMaterials + "/" + report.materialCount + ", geometry bytes=" + report.geometryBytes);

                if (container) Object.DestroyImmediate(container);
                container = null;
                ClinicalRoomWorkspace.RefreshPublishedRoom();
                exitCode = 0;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (container) Object.DestroyImmediate(container);
                foreach (var mesh in temporaryMeshes) if (mesh) Object.DestroyImmediate(mesh);
                if (!string.IsNullOrEmpty(geometryTemporary) && File.Exists(geometryTemporary)) File.Delete(geometryTemporary);
                if (!string.IsNullOrEmpty(manifestTemporary) && File.Exists(manifestTemporary)) File.Delete(manifestTemporary);
                EditorApplication.Exit(exitCode);
            }
        }

        static Texture GetBaseColorTexture(Material material)
        {
            if (!material) return null;
            return material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
        }

        static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
            if (!importer) throw new InvalidOperationException("ModelImporter missing: " + Source);
            if (!importer.importAnimation && !importer.importCameras && !importer.importLights && importer.isReadable) return;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer found in " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static string ToAbsolutePath(string projectRoot, string assetPath)
        {
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static string HashFile(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void ReplaceFile(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        static int TriangleCount(Mesh mesh)
        {
            var count = 0;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                if (mesh.GetTopology(submesh) == MeshTopology.Triangles) count += (int)(mesh.GetIndexCount(submesh) / 3);
            return count;
        }

        static string CaptureOutput()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-bgqrCaptureOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -bgqrCaptureOutput directory.");
            return Path.GetFullPath(args[index + 1]);
        }

        static void RequireAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Cleaning-room model import requires the Android target.");
        }
    }
}
