using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Explicit authoring only. A generic training cabinet based on V07, not a
    // manufacturer device or a claim about an actual hospital's equipment.
    public static class StorageRoomPublish
    {
        const string Output="Assets/EndoscopyTheme/Resources/FullScriptRooms/Storage";
        public static void Publish()
        {
            int code=1;GameObject room=null,cabinet=null,cube=null,cylinder=null;
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Android required.");
                Directory.CreateDirectory(Output);AssetDatabase.Refresh();
                room=new GameObject("SuppliedArchitectureStorageRoom");
                var architecture=Resources.Load<GameObject>(FullScriptRoomCatalog.WaitingRoom);
                if(!architecture)throw new InvalidOperationException("Published source architecture missing.");
                foreach(Transform child in architecture.transform)
                    if(child.name.StartsWith("Architecture")||child.name.StartsWith("CorridorEntrance"))
                        Object.Instantiate(child.gameObject,room.transform,false);
                RoomMaterialPublish.IsolateEmbeddedMaterials(room,Output);
                Material Surface(string name,Color color,float metal,float smooth)
                {
                    string path=Output+"/"+name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!material){material=new Material(Resources.Load<Material>("EndoscopyRoom/RoomSurface"));AssetDatabase.CreateAsset(material,path);}
                    material.color=color;material.mainTexture=null;material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);
                    EditorUtility.SetDirty(material);return material;
                }
                var white=Surface("PowderCoatedShell",new Color(.9f,.92f,.91f),.15f,.42f);
                var steel=Surface("DrySteelInterior",new Color(.64f,.69f,.71f),.55f,.52f);
                var rubber=Surface("DoorGasket",new Color(.065f,.075f,.08f),0,.2f);
                var glass=Surface("InspectionGlass",new Color(.8f,.91f,.92f,.12f),0,.6f);
                glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);
                glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
                cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var cubeMesh=cube.GetComponent<MeshFilter>().sharedMesh;var cylinderMesh=cylinder.GetComponent<MeshFilter>().sharedMesh;
                cabinet=new GameObject("TrainingStorageCabinet");
                // Meshes are combined per material and door, not hundreds of live cubes.
                var batches=new Dictionary<(Transform,Material),List<CombineInstance>>();
                void Piece(Transform parent,Material mat,Vector3 center,Vector3 size,bool round=false,Quaternion? rotation=null)
                {
                    var key=(parent,mat);if(!batches.TryGetValue(key,out var list)){list=new List<CombineInstance>();batches.Add(key,list);}
                    list.Add(new CombineInstance{mesh=round?cylinderMesh:cubeMesh,transform=Matrix4x4.TRS(center,rotation??Quaternion.identity,size)});
                }
                var frame=cabinet.transform;
                // Open-front double-door body with real interior volume, not a solid box.
                Piece(frame,white,new Vector3(-.6f,1.15f,0),new Vector3(.035f,2.1f,.64f));
                Piece(frame,white,new Vector3(.6f,1.15f,0),new Vector3(.035f,2.1f,.64f));
                Piece(frame,steel,new Vector3(0,1.15f,.305f),new Vector3(1.18f,2.1f,.025f));
                Piece(frame,steel,new Vector3(0,.23f,0),new Vector3(1.17f,.025f,.59f));
                Piece(frame,steel,new Vector3(0,2.02f,0),new Vector3(1.17f,.025f,.59f));
                Piece(frame,white,new Vector3(0,2.18f,0),new Vector3(1.23f,.04f,.64f));
                Piece(frame,white,new Vector3(0,.12f,0),new Vector3(1.23f,.04f,.64f));
                // Ventilation plenum openings have dark recesses and individually separated slats.
                foreach(float y in new[]{.2f,2.09f})
                {
                    Piece(frame,rubber,new Vector3(0,y,-.291f),new Vector3(1.13f,.12f,.018f));
                    for(int n=0;n<8;n++)Piece(frame,white,new Vector3(0,y-.055f+n*.015f,-.32f),new Vector3(1.13f,.007f,.018f));
                    for(int n=0;n<4;n++)Piece(frame,white,new Vector3(-.57f+n*.38f,y,-.327f),new Vector3(.012f,.13f,.02f));
                }
                foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.23f,.23f})
                {
                    Piece(frame,steel,new Vector3(x,.065f,z),new Vector3(.038f,.035f,.038f),true);
                    Piece(frame,rubber,new Vector3(x,.017f,z),new Vector3(.072f,.017f,.072f),true);
                }
                // Empty mounting rail: no fake hanging endoscope made from the coiled source.
                Piece(frame,steel,new Vector3(0,1.9f,.12f),new Vector3(1.12f,.025f,.025f));
                for(int n=0;n<3;n++)Piece(frame,rubber,new Vector3(-.36f+n*.36f,1.86f,.095f),new Vector3(.07f,.09f,.08f));
                for(int side=0;side<2;side++)
                {
                    float sign=side==0?1:-1;
                    var leaf=new GameObject(side==0?"LeftDoor":"RightDoor").transform;leaf.SetParent(frame,false);
                    leaf.localPosition=new Vector3(-sign*.585f,0,-.345f);
                    float mid=sign*.29f;
                    foreach(float x in new[]{sign*.017f,sign*.565f})
                        Piece(leaf,white,new Vector3(x,1.14f,0),new Vector3(.034f,1.72f,.035f));
                    foreach(float y in new[]{.295f,1.985f})
                        Piece(leaf,white,new Vector3(mid,y,0),new Vector3(.58f,.035f,.035f));
                    foreach(float x in new[]{sign*.04f,sign*.54f})
                        Piece(leaf,rubber,new Vector3(x,1.14f,.007f),new Vector3(.009f,1.64f,.012f));
                    foreach(float y in new[]{.323f,1.958f})
                        Piece(leaf,rubber,new Vector3(mid,y,.007f),new Vector3(.5f,.009f,.012f));
                    Piece(leaf,glass,new Vector3(mid,1.14f,.01f),new Vector3(.493f,1.627f,.008f));
                    float handleX=sign*.495f;
                    Piece(leaf,steel,new Vector3(handleX,1.12f,-.072f),new Vector3(.022f,.16f,.022f),true);
                    foreach(float y in new[]{.975f,1.265f})Piece(leaf,steel,new Vector3(handleX,y,-.041f),new Vector3(.024f,.024f,.065f));
                    foreach(float y in new[]{.5f,1.77f})Piece(leaf,steel,new Vector3(0,y,0),new Vector3(.035f,.055f,.035f),true);
                    var grip=new GameObject("HandleAnchor").transform;grip.SetParent(leaf,false);grip.localPosition=new Vector3(handleX,1.12f,-.07f);
                }
                int count=0;
                foreach(var batch in batches)
                {
                    string name=(batch.Key.Item1==frame?"Body":batch.Key.Item1.name)+"_"+batch.Key.Item2.name;
                    var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();
                    string path=Output+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                    var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(batch.Key.Item1,false);
                    part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=batch.Key.Item2;count+=mesh.triangles.Length/3;
                }
                PrefabUtility.SaveAsPrefabAsset(cabinet,Output+"/StorageCabinet.prefab");
                cabinet.transform.SetParent(room.transform,false);cabinet.transform.localPosition=new Vector3(-.9f,0,2);
                var view=new GameObject("StorageObservation").transform;view.SetParent(room.transform,false);view.localPosition=new Vector3(-.9f,0,1);
                PrefabUtility.SaveAsPrefabAsset(room,Output+"/StorageRoom.prefab");
                File.WriteAllText(Output+"/provenance.txt","Architecture: supplied OfficeRoom-derived Waiting/Architecture and CorridorEntrance meshes; no waiting chairs or partition.\nCabinet: authored generic structural model based on V07; 1.23m wide, 2.2m high, .64m deep; not a manufacturer specification.\nTwo hinged leaves, glass, seals, handles, feet, vent openings, visible interior and empty mounting rail. Hanging instruments and actual ventilation performance NOT provided.\nCabinet triangles: "+count+"\n");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                var dependencies=AssetDatabase.GetDependencies(Output+"/StorageRoom.prefab",true);File.WriteAllLines(Output+"/dependencies.txt",dependencies);
                if(dependencies.Any(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith("WaitingRoom.prefab")||p.EndsWith("Chair.prefab")))throw new InvalidOperationException("Storage room still references a whole source asset.");
                code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{if(cube)Object.DestroyImmediate(cube);if(cylinder)Object.DestroyImmediate(cylinder);if(cabinet && !cabinet.transform.parent)Object.DestroyImmediate(cabinet);if(room)Object.DestroyImmediate(room);}
            EditorApplication.Exit(code);
        }
    }
}
