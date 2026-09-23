using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // A derived training layout, not a scan or a claim about a real hospital.
    // Every visible mesh is copied from the supplied architecture/furniture.
    public static class WaitingRoomPublish
    {
        const string Output="Assets/EndoscopyTheme/Resources/FullScriptRooms/Waiting";
        public static void Publish()
        {
            int code=1;GameObject source=null,room=null;
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Android required.");
                Directory.CreateDirectory(Output);AssetDatabase.Refresh();
                source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndoscopyTheme/ImportedModels/Prepared/OfficeRoom.prefab"));
                room=new GameObject("SuppliedArchitectureWaitingRoom");
                var template=Resources.Load<Material>("EndoscopyRoom/RoomSurface");
                Material Surface(string name,Color color,string texture=null)
                {
                    var path=Output+"/"+name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!material){material=new Material(template);AssetDatabase.CreateAsset(material,path);}
                    material.color=color;material.mainTexture=texture==null?null:Resources.Load<Texture2D>(texture);
                    material.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(material);return material;
                }
                var wall=Surface("PaintedWall",new Color(.91f,.94f,.93f));
                var floor=Surface("ReferenceTerrazzo",Color.white,"ClinicalCourse/FullScriptVisuals/hospital-terrazzo-v1");
                var partition=Surface("PartitionBase",new Color(.32f,.52f,.56f));
                var glass=Surface("PartitionGlazing",new Color(.73f,.87f,.88f,.24f));
                glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);
                glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
                var copied=new List<string>();
                GameObject Part(MeshFilter original,string name,Matrix4x4 adjustment,Material overrideMaterial=null,bool floorUv=false)
                {
                    var src=original.sharedMesh;
                    var matrix=adjustment*Matrix4x4.Translate(Vector3.down*.15f)*original.transform.localToWorldMatrix;
                    var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                    var combine=Enumerable.Range(0,src.subMeshCount).Select(s=>new CombineInstance{mesh=src,subMeshIndex=s,transform=matrix}).ToArray();
                    mesh.CombineMeshes(combine,false,true);
                    if(floorUv)mesh.uv=mesh.vertices.Select(p=>new Vector2(p.x,p.z)*.5f).ToArray();
                    var path=Output+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                    var item=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));item.transform.SetParent(room.transform,false);
                    item.GetComponent<MeshFilter>().sharedMesh=mesh;
                    item.GetComponent<MeshRenderer>().sharedMaterials=overrideMaterial?Enumerable.Repeat(overrideMaterial,mesh.subMeshCount).ToArray():original.GetComponent<MeshRenderer>().sharedMaterials;
                    item.AddComponent<MeshCollider>().sharedMesh=mesh;
                    copied.Add(name+" <- "+original.name);return item;
                }
                var filters=source.GetComponentsInChildren<MeshFilter>();
                var rear=filters.Single(f=>f.name.Contains("[337345]"));
                int index=0;
                foreach(var f in filters)
                {
                    bool architecture=f.name.Contains("基本墙")||f.name.Contains("楼板")||f.name.Contains("天花板")||f.name.Contains("吸顶灯")||f.name.Contains("[341335]");
                    if(!architecture || f.name.Contains("[342106]") || f.name.Contains("[342138]") || f==rear)continue;
                    bool isFloor=f.name.Contains("楼板");
                    Part(f,"Architecture"+(index++),Matrix4x4.identity,isFloor?floor:f.name.Contains("基本墙")?wall:null,isFloor);
                    // Mirror the existing door opening as an independent corridor entrance.
                    if(f.name.Contains("[337030]")||f.name.Contains("[336901]")||f.name.Contains("[336964]")||f.name.Contains("[341335]"))
                        Part(f,"CorridorEntrance"+index,Matrix4x4.Rotate(Quaternion.Euler(0,180,0)),f.name.Contains("基本墙")?wall:null);
                }
                var b=rear.GetComponent<Renderer>().bounds;b.center+=Vector3.down*.15f;
                Matrix4x4 PartitionAt(float height,float centerY)=>Matrix4x4.TRS(new Vector3(0,centerY,.35f),Quaternion.identity,new Vector3(1,height/b.size.y,1))*Matrix4x4.Translate(-b.center);
                Part(rear,"PhysicalPartitionBase",PartitionAt(1.05f,.525f),partition);
                Part(rear,"PhysicalPartitionGlass",PartitionAt(1.95f,2.025f),glass);
                var chair=Resources.Load<GameObject>("FullScriptRooms/Stationary/Chair");
                if(!chair)throw new InvalidOperationException("Previously published supplied chair missing.");
                for(int n=0;n<3;n++)
                {
                    var seat=UnityEngine.Object.Instantiate(chair,room.transform,false);seat.name="WaitingSeat"+n;
                    seat.transform.localPosition=new Vector3(-2.05f,0,-1.57f+n*.70f);seat.transform.localRotation=Quaternion.Euler(0,90,0);
                }
                // Clearly named anchors are authoring/inspection data, not completion flags.
                foreach(var entry in new[]{("WaitingObservation",new Vector3(.4f,0,-1.05f)),("ClinicalCorridorObservation",new Vector3(.4f,0,1.65f))})
                {var anchor=new GameObject(entry.Item1);anchor.transform.SetParent(room.transform,false);anchor.transform.localPosition=entry.Item2;}
                RoomMaterialPublish.IsolateEmbeddedMaterials(room,Output);
                PrefabUtility.SaveAsPrefabAsset(room,Output+"/WaitingRoom.prefab");
                File.WriteAllLines(Output+"/source-meshes.txt",copied);
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                var dependencies=AssetDatabase.GetDependencies(Output+"/WaitingRoom.prefab",true);
                File.WriteAllLines(Output+"/dependencies.txt",dependencies);
                if(dependencies.Any(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Waiting still references a source FBX.");
                code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{if(source)UnityEngine.Object.DestroyImmediate(source);if(room)UnityEngine.Object.DestroyImmediate(room);}
            EditorApplication.Exit(code);
        }
    }
}
