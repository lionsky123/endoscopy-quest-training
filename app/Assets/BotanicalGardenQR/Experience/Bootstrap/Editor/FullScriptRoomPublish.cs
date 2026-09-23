using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Publishes only a room's baked visual data and authored obstacle bounds, never the source FBX graph.
    public static class FullScriptRoomPublish
    {
        [Serializable] sealed class Surface { public string name,texture,textureResource; public float[] color; }
        [Serializable] sealed class Obstacle { public Vector3 center,size; }
        [Serializable] sealed class Manifest { public Surface[] materials; public Obstacle[] obstacles; public Vector3 terminal; }
        public static void Publish()
        {
            int code=1;var temporary=new List<Mesh>();GameObject root=null;
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Android target required.");
                bool clinical=Environment.GetCommandLineArgs().Contains("-bgqrClinicalRoom");
                string roomName=clinical?"Clinical":"Office";
                string Output="Assets/EndoscopyTheme/Resources/FullScriptRooms/"+roomName;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                Directory.CreateDirectory(Output);AssetDatabase.Refresh();
                root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndoscopyTheme/ImportedModels/Prepared/"+(clinical?"ClinicalRoom":"OfficeRoom")+".prefab"));
                if(!root)throw new InvalidOperationException("Prepared room missing.");
                var groups=new Dictionary<Material,List<CombineInstance>>();
                var obstacles=new List<Obstacle>();int omittedChair=0,movedScreen=0;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();if(!filter.sharedMesh || !renderer)continue;
                    var bounds=renderer.bounds;var c=bounds.center;
                    // The selected main workstation is a standing inspection bay. Other chairs remain intact.
                    // Only this chair's geometry is omitted in this derived room, not in the supplied source.
                    if(!clinical && c.x>1.5f && c.x<2.35f && c.z>-.2f && c.z<.75f && c.y<1.45f){omittedChair++;continue;}
                    var offset=new Vector3(0,-.15f,0); // Actual floor slab upper face, not its lowest bound.
                    // The desk contains two back-to-back monitors. Keep both at source positions;
                    // the terminal is attached to the aisle-facing front, not the rear monitor.
                    bounds.center+=offset;
                    if(bounds.max.y>.15f && bounds.min.y<1.8f)obstacles.Add(new Obstacle{center=bounds.center,size=bounds.size});
                    for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                    {
                        var material=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                        if(!material)throw new InvalidOperationException("Missing office material.");
                        if(!groups.TryGetValue(material,out var list))groups.Add(material,list=new List<CombineInstance>());
                        list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=Matrix4x4.Translate(offset)*filter.transform.localToWorldMatrix});
                    }
                }
                var batches=new List<(Mesh mesh,int material)>();var surfaces=new List<Surface>();
                foreach(var group in groups)
                {
                    var color=group.Key.color;var index=surfaces.Count;
                    string textureResource=null;
                    if(group.Key.mainTexture)
                    {
                        var texturePath=AssetDatabase.GetAssetPath(group.Key.mainTexture).Replace('\\','/');
                        var resourcesIndex=texturePath.LastIndexOf("/Resources/",StringComparison.Ordinal);
                        if(resourcesIndex<0)throw new InvalidOperationException("Texture must be explicitly published, not silently discarded: "+texturePath);
                        textureResource=texturePath.Substring(resourcesIndex+11);textureResource=textureResource.Substring(0,textureResource.LastIndexOf('.'));
                    }
                    surfaces.Add(new Surface{name=group.Key.name,color=new[]{color.r,color.g,color.b},textureResource=textureResource});
                    var chunk=new List<CombineInstance>();int vertices=0;
                    void Flush()
                    {
                        if(chunk.Count==0)return;
                        var mesh=new Mesh{name=roomName+"Static_"+batches.Count,indexFormat=IndexFormat.UInt32};
                        mesh.CombineMeshes(chunk.ToArray(),true,true);temporary.Add(mesh);batches.Add((mesh,index));chunk.Clear();vertices=0;
                    }
                    foreach(var combine in group.Value)
                    {
                        if(vertices+combine.mesh.vertexCount>120000)Flush();
                        chunk.Add(combine);vertices+=combine.mesh.vertexCount;
                    }
                    Flush();
                }
                var geometryPath=Output+"/geometry.bytes";
                using(var writer=new BinaryWriter(File.Create(geometryPath)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("ECR1"));writer.Write(batches.Count);
                    foreach(var batch in batches)
                    {
                        var mesh=batch.mesh;var name=Encoding.UTF8.GetBytes(mesh.name);writer.Write(name.Length);writer.Write(name);
                        var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;writer.Write(vertices.Length);
                        for(int i=0;i<vertices.Length;i++)
                        {
                            var v=vertices[i];var n=normals.Length==vertices.Length?normals[i]:Vector3.up;var t=uv.Length==vertices.Length?uv[i]:Vector2.zero;
                            writer.Write(-v.x);writer.Write(v.y);writer.Write(-v.z);writer.Write(-n.x);writer.Write(n.y);writer.Write(-n.z);writer.Write(t.x);writer.Write(t.y);
                        }
                        writer.Write(1);writer.Write(batch.material);var indices=mesh.triangles;writer.Write(indices.Length);foreach(var i in indices)writer.Write(i);
                    }
                }
                File.WriteAllText(Output+"/manifest.json",JsonUtility.ToJson(new Manifest{materials=surfaces.ToArray(),obstacles=obstacles.ToArray(),terminal=clinical?new Vector3(.7f,1.2f,.7f):new Vector3(2.775f,1.035f,.2576f)},true));
                var surfacePath=Output+"/RoomSurface.mat";
                if(!AssetDatabase.LoadAssetAtPath<Material>(surfacePath))AssetDatabase.CreateAsset(new Material(Resources.Load<Material>("EndoscopyRoom/RoomSurface")),surfacePath);
                string digest;using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(geometryPath))).Replace("-","").ToLowerInvariant();
                var points=clinical?new[]{new MapPosition(-1.866f,0,-1.4f),new MapPosition(-1.866f,0,0),new MapPosition(-.6f,0,0),new MapPosition(-.6f,0,1.2f),new MapPosition(.7f,0,1.2f)}:
                    new[]{new MapPosition(2.35f,0,-2.1f),new MapPosition(1.3f,0,-2.1f),new MapPosition(1.3f,0,-.9f),new MapPosition(2.05f,0,-.9f),new MapPosition(2.05f,0,.25f)};
                var map=new MapDefinition{mapId=clinical?"R04_CLINICAL":"R01_OFFICE",roomResource="FullScriptRooms/"+roomName,modelDigest=digest,scale=1,start=points[0],
                    points=new[]{new MapPoint{id=FullScriptRoomCatalog.Overview,position=points.Last()},new MapPoint{id=FullScriptRoomCatalog.Door,position=points[0]}},
                    routes=new[]{new MapRoute{from="Start",to=FullScriptRoomCatalog.Overview,samples=points},new MapRoute{from=FullScriptRoomCatalog.Overview,to=FullScriptRoomCatalog.Door,samples=points.Reverse().ToArray()}}};
                MapDefinitionValidation.Validate(map);
                // Validate the same authoritative bounds and swept paths that runtime will use.
                var obstacleBounds=obstacles.Select(o=>new Bounds(o.center,o.size)).ToArray();
                _=new VirtualRoomGuidePath(map,new MapFrame(new MapPosition(0,0,0),0,1),new MeshFilter[0],obstacleBounds);
                File.WriteAllText(Output+"/map.json",JsonUtility.ToJson(map,true));
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                var args=Environment.GetCommandLineArgs();var target=args[Array.IndexOf(args,"-bgqrCaptureOutput")+1];Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(target,roomName.ToLowerInvariant()+"-publication.txt"),$"Derived from supplied {roomName}Room; {batches.Count} render batches, {obstacles.Count} source obstacle boxes.\nStanding bay: omitted {omittedChair} chair mesh parts; moved {movedScreen} monitor parts 0.45m toward desk front. Original assets unchanged.\nFloor upper face offset -0.15m. Model SHA256 {digest}.\nNo APK build; Quest performance and real deployment area not verified.\n");
                code=0;
            }
            catch(Exception error){Debug.LogException(error);}
            finally{if(root)Object.DestroyImmediate(root);foreach(var mesh in temporary)Object.DestroyImmediate(mesh);}
            EditorApplication.Exit(code);
        }
    }
}
