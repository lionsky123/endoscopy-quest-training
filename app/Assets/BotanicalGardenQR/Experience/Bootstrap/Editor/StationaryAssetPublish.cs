using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Explicit authoring command only: no startup callback and no player build.
    public static class StationaryAssetPublish
    {
        const string Output = "Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary";
        public static void Publish()
        {
            GameObject root = null;
            int code = 1;
            try
            {
                Directory.CreateDirectory(Output);
                AssetDatabase.Refresh();
                foreach(var name in new[]{"Gastroscope", "Computer", "Desk"})
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndoscopyTheme/ImportedModels/Prepared/"+name+".prefab");
                    if(!source)throw new InvalidOperationException("Missing prepared source: "+name);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
                    try{PrefabUtility.SaveAsPrefabAsset(instance,Output+"/"+name+".prefab");}
                    finally{UnityEngine.Object.DestroyImmediate(instance);}
                }
                var door=new GameObject("Door342041");
                try
                {
                    var material=Resources.Load<Material>("EndoscopyRoom/RoomSurface");
                    foreach(var part in new[]{"Frame","Leaf"})
                    {
                        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/EndoscopyTheme/ImportedModels/Source/OfficeInteractions/342041_"+part+".asset");
                        if(!mesh)throw new InvalidOperationException("Missing supplied door part "+part);
                        var item=new GameObject(part,typeof(MeshFilter),typeof(MeshRenderer));item.transform.SetParent(door.transform,false);
                        item.GetComponent<MeshFilter>().sharedMesh=mesh;
                        item.GetComponent<MeshRenderer>().sharedMaterials=System.Linq.Enumerable.Repeat(material,mesh.subMeshCount).ToArray();
                    }
                    PrefabUtility.SaveAsPrefabAsset(door,Output+"/Door342041.prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(door);}
                PublishChair();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{if(root)UnityEngine.Object.DestroyImmediate(root);}
            EditorApplication.Exit(code);
        }
        static void PublishChair()
        {
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndoscopyTheme/ImportedModels/Prepared/OfficeRoom.prefab"));
            var root=new GameObject("SuppliedOfficeChair");
            try
            {
                var groups=new Dictionary<Material,List<CombineInstance>>();
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();if(!renderer || !filter.sharedMesh)continue;
                    var c=renderer.bounds.center;
                    // Same independently inspected chair envelope used by Office publication.
                    if(!(c.x>1.5f && c.x<2.35f && c.z>-.2f && c.z<.75f && c.y<1.45f))continue;
                    for(int s=0;s<filter.sharedMesh.subMeshCount;s++)
                    {
                        var material=renderer.sharedMaterials[Mathf.Min(s,renderer.sharedMaterials.Length-1)];
                        if(!groups.TryGetValue(material,out var list))groups.Add(material,list=new List<CombineInstance>());
                        list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=s,transform=filter.transform.localToWorldMatrix});
                    }
                }
                if(groups.Count==0)throw new InvalidOperationException("Inspected office chair envelope is empty.");
                int index=0;
                foreach(var pair in groups)
                {
                    var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                    var path=Output+"/ChairPart"+(index++)+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                    var item=new GameObject("ChairPart",typeof(MeshFilter),typeof(MeshRenderer));item.transform.SetParent(root.transform,false);
                    item.GetComponent<MeshFilter>().sharedMesh=mesh;item.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
                }
                var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                foreach(Transform child in root.transform)child.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/Chair.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
