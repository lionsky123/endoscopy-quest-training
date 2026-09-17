using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using BotanicalGardenQR.VisitorAtlasHub.Frontend;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    /// <summary>Invoked by the existing content publisher in its isolated authoring mirror.</summary>
    public static class FieldbookAssetPublisher
    {
        public const string Root = "Assets/BotanicalGardenQR/Content/Shared/Fieldbook";
        const string Presentation = "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/";
        const string CatalogPath = "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset";
        static readonly List<string> Outputs = new List<string>();

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-fieldbookManifest");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("External manifest path required.");
            var manifest = Path.GetFullPath(args[at + 1]);
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (manifest.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Authoring evidence must be external.");
            Outputs.Clear();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/BotanicalGardenQR/Content/Shared", "Fieldbook");
            Outputs.Add(Root + ".meta");
            var pageMesh = SaveMesh("Page.asset", FieldbookPageGeometry.Create(.20f, .28f, .006f, .008f));
            var paper = Material("Paper.mat", new Color(.78f, .71f, .55f));
            var cover = Material("Cover.mat", new Color(.14f, .105f, .065f));
            var brass = Material("Brass.mat", new Color(.48f, .33f, .14f));
            var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
            var page = PublishPage(pageMesh, paper, cover, brass, font);
            PublishWorld(pageMesh, paper, cover, brass);
            PublishTools();
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var artifacts = serialized.FindProperty("_artifacts");
            for (var i = 0; i < artifacts.arraySize; i++)
                artifacts.GetArrayElementAtIndex(i).FindPropertyRelative("_presentationPrefab").objectReferenceValue = page;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            Track(CatalogPath);
            AssetDatabase.SaveAssets();
            var contentResult = BotanicalGardenQR.Configuration.Editor.ContentScenePublisher.PublishContentLibrary();
            if (!contentResult.Succeeded) throw new InvalidOperationException(contentResult.Validation.Format());
            Track("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset");
            Track("Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset");
            File.WriteAllLines(manifest, Outputs.Distinct());
            Debug.Log("FIELD BOOK PUBLISH PASSED");
            if (Array.IndexOf(args, "-bgqrCaptureOutput") >= 0) VisitorGamePresentationCapture.RunInterfaceOnly();
        }

        static GameObject PublishPage(Mesh mesh, Material paper, Material cover, Material brass, TMP_FontAsset font)
        {
            // Preserve the already-tested SDK hand/rigidbody/pointable dependency closure.
            var path = Root + "/FieldbookPage.prefab";
            var source = path;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null) throw new InvalidOperationException("The authored FieldbookPage SDK closure is required.");
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = "FieldbookPage";
                var relay = root.GetComponent<CollectionArtifactGrabRelay>();
                var data = new SerializedObject(relay);
                var visual = (Transform)data.FindProperty("_visualRoot").objectReferenceValue;
                Clear(visual);
                for (var child = root.transform.childCount - 1; child >= 0; child--)
                    if (root.transform.GetChild(child) != visual) Object.DestroyImmediate(root.transform.GetChild(child).gameObject);
                visual.localScale = Vector3.one;
                var foldout = root.GetComponent<FieldbookFoldout>() ?? root.AddComponent<FieldbookFoldout>();
                var folded = new SerializedObject(foldout);
                var paperMesh = SaveMesh("FoldoutPaper.asset", FieldbookPageGeometry.Create(.14f, .21f, .004f, .002f));
                var photoMaterial = Material("Picture.mat", Color.white);
                photoMaterial.shader = Shader.Find("Universal Render Pipeline/Unlit");
                EditorUtility.SetDirty(photoMaterial);
                var gripMaterial = Material("Grip.mat", new Color(.95f, .72f, .28f));
                gripMaterial.shader = Shader.Find("Universal Render Pipeline/Unlit");
                EditorUtility.SetDirty(gripMaterial);
                var pages = new Transform[2]; var pictures = new Renderer[2]; var grips = new Transform[2];
                for (var side = 0; side < 2; side++)
                {
                    pages[side] = Surface(side == 0 ? "LeftPage" : "RightPage", visual, paperMesh, paper, Vector3.zero).transform;
                    pictures[side] = Surface("Picture", pages[side], SaveMesh("PictureHalf" + side + ".asset", PictureMesh(.126f, .17f, side * .5f, (side + 1) * .5f)), photoMaterial, new Vector3(0, 0, -.005f));
                    Box("UpperEdge", pages[side], new Vector3(0, .098f, -.004f), new Vector3(.13f, .002f, .001f), brass);
                    Box("LowerEdge", pages[side], new Vector3(0, -.098f, -.004f), new Vector3(.13f, .002f, .001f), brass);
                    // Colliders keep unit transforms; only their decorative children are scaled.
                    grips[side] = new GameObject(side == 0 ? "LeftGrip" : "RightGrip").transform;
                    grips[side].SetParent(visual, false);
                    Box("GripLight", grips[side], Vector3.zero, new Vector3(.012f, .072f, .012f), gripMaterial);
                    var edge = grips[side].gameObject.AddComponent<BoxCollider>(); edge.size = new Vector3(.035f, .11f, .035f);
                }
                var detail = new GameObject("PopUpDetail").transform; detail.SetParent(visual, false);
                Box("PaperBack", detail, new Vector3(0, 0, .003f), new Vector3(.108f, .128f, .006f), paper);
                var detailPicture = Surface("PopUpPicture", detail, SaveMesh("PopUpPicture.asset", PictureMesh(.098f, .118f, .2f, .8f)), photoMaterial, new Vector3(0, 0, -.002f));
                for (var side = -1; side <= 1; side += 2)
                    Box("PaperSupport", detail, new Vector3(side * .034f, -.075f, .0475f), new Vector3(.012f, .1f, .004f), paper)
                        .transform.localRotation = Quaternion.Euler(-72, 0, 0);
                var labels = new GameObject("Labels").transform; labels.SetParent(visual, false);
                var title = Label("RecordTitle", labels, font, "一起打开这一页", new Vector3(0, .145f, -.012f), .012f, new Vector2(.32f, .035f));
                var caption = Label("RecordCategory", labels, font, "捏住页边 · 向外拉开", new Vector3(0, -.14f, -.012f), .009f, new Vector2(.32f, .035f));
                title.color = caption.color = new Color(.94f, .87f, .67f);
                var oldVolumes = root.GetComponents<Collider>();
                foreach (var volume in oldVolumes) Object.DestroyImmediate(volume);
                data.Update();
                var volumes = data.FindProperty("_grabVolumes"); volumes.arraySize = 2;
                var renderers = data.FindProperty("_renderers"); renderers.arraySize = 2;
                for (var i = 0; i < 2; i++)
                {
                    volumes.GetArrayElementAtIndex(i).objectReferenceValue = grips[i].GetComponent<BoxCollider>();
                    renderers.GetArrayElementAtIndex(i).objectReferenceValue = grips[i].GetComponentInChildren<Renderer>();
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                var handGrab = root.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
                handGrab.MaxSelectingInteractors = 1;
                // The paper hinges move while its root stays fixed; keep the rendered hand at its tracked pose.
                handGrab.HandAlignment = Oculus.Interaction.HandGrab.HandAlignType.None;
                var grabbable = root.GetComponent<Oculus.Interaction.Grabbable>();
                grabbable.InjectOptionalOneGrabTransformer(foldout);
                grabbable.InjectOptionalTwoGrabTransformer(foldout);
                var grabData = new SerializedObject(grabbable); grabData.FindProperty("_maxGrabPoints").intValue = 1; grabData.ApplyModifiedPropertiesWithoutUndo();
                folded.FindProperty("_leftPage").objectReferenceValue = pages[0];
                folded.FindProperty("_rightPage").objectReferenceValue = pages[1];
                folded.FindProperty("_leftGrip").objectReferenceValue = grips[0];
                folded.FindProperty("_rightGrip").objectReferenceValue = grips[1];
                folded.FindProperty("_detail").objectReferenceValue = detail;
                folded.FindProperty("_labels").objectReferenceValue = labels;
                folded.FindProperty("_detailPicture").objectReferenceValue = detailPicture;
                folded.FindProperty("_handGrab").objectReferenceValue = handGrab;
                var photoArray = folded.FindProperty("_pictures"); photoArray.arraySize = 2;
                for (var i = 0; i < 2; i++) photoArray.GetArrayElementAtIndex(i).objectReferenceValue = pictures[i];
                folded.ApplyModifiedPropertiesWithoutUndo();
                foldout.Preview(0);
                var view = root.GetComponent<FieldbookPageView>() ?? root.AddComponent<FieldbookPageView>();
                var viewData = new SerializedObject(view);
                viewData.FindProperty("_title").objectReferenceValue = title;
                viewData.FindProperty("_caption").objectReferenceValue = caption;
                viewData.ApplyModifiedPropertiesWithoutUndo();
                var result = PrefabUtility.SaveAsPrefabAsset(root, path);
                Track(path);
                return result;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void PublishTools()
        {
            var path = Presentation + "VisitorAtlasHubPresentation.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(root.GetComponent<VisitorAtlasHubPresentation>());
                data.FindProperty("_entryHideLocalPosition").vector3Value = new Vector3(0, -.28f, .02f);
                var help = (Button)data.FindProperty("_helpButton").objectReferenceValue;
                var discovery = (Button)data.FindProperty("_discoveryButton").objectReferenceValue;
                if (discovery == null) discovery = Object.Instantiate(help, help.transform.parent);
                discovery.name = "CurrentDiscovery";
                discovery.GetComponent<RectTransform>().anchoredPosition = new Vector2(-116, -86);
                discovery.GetComponentInChildren<TMP_Text>(true).text = "当前发现";
                discovery.onClick = new Button.ButtonClickedEvent();
                data.FindProperty("_discoveryButton").objectReferenceValue = discovery;
                var recall = (Button)data.FindProperty("_recallFairyButton").objectReferenceValue;
                if (recall == null) recall = Object.Instantiate(help, help.transform.parent);
                recall.name = "RecallFairy";
                recall.GetComponent<RectTransform>().anchoredPosition = new Vector2(116, -86);
                recall.GetComponentInChildren<TMP_Text>(true).text = "召回精灵";
                recall.onClick = new Button.ButtonClickedEvent();
                data.FindProperty("_recallFairyButton").objectReferenceValue = recall;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Track(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void PublishWorld(Mesh page, Material paper, Material cover, Material brass)
        {
            var path = Presentation + "CollectionWorldPresentation.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var frontend = root.GetComponent<CollectionWorldFrontend>();
                var data = new SerializedObject(frontend);
                var summary = root.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "RewardSubtitle");
                summary.text = "这一页已入册，随时可以翻开回看。";
                summary.rectTransform.sizeDelta = new Vector2(540, 48);
                summary.fontSize = 18;
                data.FindProperty("_rewardSummary").objectReferenceValue = summary;
                root.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "RewardKicker").text = "见闻册 · 共同记录";
                // Details are populated by the selected record; do not ship an unrelated demo fact.
                root.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "DetailSummary").text = string.Empty;
                var book = (Transform)data.FindProperty("_bookResponseRoot").objectReferenceValue;
                var baseRoot = (Transform)data.FindProperty("_recordDisplayRoot").objectReferenceValue;
                var slot = (Transform)data.FindProperty("_artifactReturnSlot").objectReferenceValue;
                var position = slot.localPosition;
                // Keep stable serialized reception identities, replace only their presentation children.
                for (var i = book.childCount - 1; i >= 0; i--)
                    if (book.GetChild(i) != slot) Object.DestroyImmediate(book.GetChild(i).gameObject);
                Clear(baseRoot);
                book.name = "FieldbookReception";
                baseRoot.name = "RecordedPages";
                var hub = AssetDatabase.LoadAssetAtPath<GameObject>(Presentation + "VisitorAtlasHubPresentation.prefab");
                var source = hub.GetComponentsInChildren<Transform>(true).Single(t => t.name == "BookVisual");
                var closedBook = Object.Instantiate(source.gameObject, baseRoot);
                closedBook.name = "RecordedBook";
                closedBook.transform.localPosition = Vector3.zero;
                closedBook.transform.localRotation = Quaternion.Euler(-15, 0, 0);
                var bookBounds = source.GetComponent<MeshFilter>().sharedMesh.bounds;
                var bookScale = .28f / Mathf.Max(bookBounds.size.x, bookBounds.size.y, bookBounds.size.z);
                closedBook.transform.localScale = Vector3.one * bookScale;
                closedBook.transform.localPosition = -(closedBook.transform.localRotation * bookBounds.center) * bookScale;
                slot.position = baseRoot.position;
                book.gameObject.SetActive(false);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Track(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void CombineBook(Transform book)
        {
            var filters = book.GetComponentsInChildren<MeshFilter>();
            var groups = filters.GroupBy(filter => filter.GetComponent<Renderer>().sharedMaterial).ToArray();
            var combined = new List<(Mesh, Material)>();
            foreach (var group in groups)
            {
                var parts = group.Select(filter => new CombineInstance {
                    mesh = filter.sharedMesh,
                    transform = book.worldToLocalMatrix * filter.transform.localToWorldMatrix
                }).ToArray();
                var mesh = new Mesh { name = "OpenBook" + group.Key.name };
                mesh.CombineMeshes(parts, true, true);
                combined.Add((SaveMesh("OpenBook" + group.Key.name + ".asset", mesh), group.Key));
            }
            Clear(book);
            foreach (var part in combined) Surface(part.Item2.name, book, part.Item1, part.Item2, Vector3.zero);
        }

        static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, Vector3 position, float size, Vector2 bounds)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.AddComponent<TextMeshPro>(); label.font = font; label.text = text;
            label.fontSize = size * 10f; label.color = new Color(.08f, .19f, .15f);
            label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = bounds;
            label.enableAutoSizing = false; label.raycastTarget = false;
            return label;
        }

        static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static Renderer Surface(string name, Transform parent, Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            return renderer;
        }

        static Mesh LeafMesh()
        {
            const int steps = 16;
            var vertices = new Vector3[steps + 2];
            vertices[0] = new Vector3(0, 0, -.002f);
            for (var i = 0; i <= steps; i++)
            {
                var angle = (float)i / steps * Mathf.PI * 2;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * .021f * Mathf.Abs(Mathf.Sin(angle)), Mathf.Cos(angle) * .036f, 0);
            }
            var triangles = new int[steps * 6];
            for (var i = 0; i < steps; i++)
            {
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = i + 2;
                triangles[i * 6 + 3] = 0; triangles[i * 6 + 4] = i + 2; triangles[i * 6 + 5] = i + 1;
            }
            var mesh = new Mesh { name = "FieldbookLeaf", vertices = vertices, triangles = triangles };
            mesh.normals = Enumerable.Repeat(Vector3.back, vertices.Length).ToArray(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh PictureMesh(float width, float height, float u0, float u1)
        {
            var mesh = new Mesh { name = "FoldoutPicture" };
            mesh.vertices = new[] { new Vector3(-width/2,-height/2,0), new Vector3(-width/2,height/2,0), new Vector3(width/2,height/2,0), new Vector3(width/2,-height/2,0) };
            mesh.uv = new[] { new Vector2(u0,0), new Vector2(u0,1), new Vector2(u1,1), new Vector2(u1,0) };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh SaveMesh(string name, Mesh mesh)
        {
            var path = Root + "/" + name; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            Track(path); return mesh;
        }

        static Material Material(string name, Color color)
        {
            var path = Root + "/" + name; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            material.SetFloat("_Smoothness", .18f);
            material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); Track(path); return material;
        }

        static void Track(string path) { Outputs.Add(path); Outputs.Add(path + ".meta"); }
        static void Clear(Transform parent) { for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }
    }
}
