using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // Development geometry is explicitly separate from the surveyed deployment area.
    internal static class FullScriptRoomCatalog
    {
        internal const string DevelopmentResource = "FullScriptDevelopment";
        internal const string Overview = "本房间说明";
        internal const string Door = "房门";
        internal const string Washing = "R05_REPROCESSING";

        internal static MapDefinition Map(string id, MapDefinition washing, bool teachWashing)
        {
            if(id=="R01_OFFICE" || id=="R04_GI" || id=="R04_RESP")
            {
                // Resolve only the requested room's small map; never enumerate/load room assets.
                bool office=id=="R01_OFFICE";
                var asset=Resources.Load<TextAsset>("FullScriptRooms/"+(office?"Office":"Clinical")+"/map");
                if(!asset)throw new InvalidOperationException("Published room map is missing: "+id);
                var published=JsonUtility.FromJson<MapDefinition>(asset.text);
                MapDefinitionValidation.Validate(published);
                if(published.mapId!=(office?id:"R04_CLINICAL"))throw new InvalidOperationException("Published room identity mismatch.");
                // Each visit creates a distinct room instance; shared source geometry is not shared live state.
                published.mapId=id;
                return published;
            }
            if (id == Washing)
            {
                var map = washing.Snapshot();
                if (!teachWashing)
                {
                    map.points = new[] { new MapPoint { id = Overview, position = map.points[0].position } };
                    map.routes = new[] { new MapRoute { from = "Start", to = Overview, samples = map.routes[0].samples } };
                }
                // The exit follows the authored aisle in reverse; no line across the model.
                var reverse = map.routes.Reverse().SelectMany(r => r.samples.Reverse()).ToArray();
                var last = map.points.Last().id;
                map.points = map.points.Concat(new[] { new MapPoint { id = Door, position = map.start } }).ToArray();
                map.routes = map.routes.Concat(new[] { new MapRoute { from = last, to = Door, samples = reverse } }).ToArray();
                return map;
            }
            var start = new MapPosition(0, 0, 0);
            var overview = new MapPosition(-3, 0, 0);
            return new MapDefinition
            {
                mapId = id, roomResource = DevelopmentResource, scale = 1,
                start = start,
                points = new[] { new MapPoint { id = Overview, position = overview }, new MapPoint { id = Door, position = start } },
                routes = new[]
                {
                    new MapRoute { from = "Start", to = Overview, samples = new[] { start, new MapPosition(-1,0,0), new MapPosition(-2,0,0), overview } },
                    new MapRoute { from = Overview, to = Door, samples = new[] { overview, new MapPosition(-2,0,0), new MapPosition(-1,0,0), start } }
                }
            };
        }

        internal static void BuildDevelopmentGeometry(GameObject root, string id, List<UnityEngine.Object> owned)
        {
            var template = Resources.Load<Material>("EndoscopyRoom/RoomSurface");
            if (!template) throw new InvalidOperationException("Room surface material is missing.");
            var wall = new Material(template) { color = new Color(.64f,.74f,.77f) }; owned.Add(wall);
            var floor = new Material(template) { color = new Color(.24f,.34f,.38f) }; owned.Add(floor);
            var accent = new Material(template) { color = new Color(.16f,.53f,.48f) }; owned.Add(accent);
            void Box(string name, Vector3 center, Vector3 size, Material material)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.name = name; item.transform.SetParent(root.transform, false);
                // Bake room-local geometry for the existing guide clearance validator.
                var mesh = UnityEngine.Object.Instantiate(item.GetComponent<MeshFilter>().sharedMesh);
                owned.Add(mesh);
                mesh.vertices = mesh.vertices.Select(v => center + Vector3.Scale(v,size)).ToArray(); mesh.RecalculateBounds();
                item.GetComponent<MeshFilter>().sharedMesh = mesh;
                var collider = item.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
                item.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Box("Floor", new Vector3(-1.9f,-.10f,0), new Vector3(6.2f,.2f,5),floor);
            Box("LeftWall", new Vector3(-1.9f,1.5f,-2.5f), new Vector3(6.2f,3,.12f),wall);
            Box("RightWall", new Vector3(-1.9f,1.5f,2.5f), new Vector3(6.2f,3,.12f),wall);
            Box("BackWall", new Vector3(-5,1.5f,0), new Vector3(.12f,3,5),wall);
            Box("Ceiling", new Vector3(-1.9f,3.1f,0), new Vector3(6.2f,.15f,5),wall);
            Box("DoorLeft", new Vector3(.85f,1.5f,-1.6f), new Vector3(.12f,3,1.8f),wall);
            Box("DoorRight", new Vector3(.85f,1.5f,1.6f), new Vector3(.12f,3,1.8f),wall);
            Box("DoorLintel", new Vector3(.85f,2.75f,0), new Vector3(.12f,.5f,1.4f),accent);
            Box("DoorPanel", new Vector3(.95f,1.1f,0), new Vector3(.1f,2.2f,1.35f),accent);
            if (id == "R01_OFFICE" || id == "R00_LOBBY")
            {
                var z=id=="R01_OFFICE"?0:1.3f;
                Box("Desk", new Vector3(-3.9f,.75f,z), new Vector3(.7f,.12f,1.3f),accent);
                Box("DeskBase", new Vector3(-3.9f,.36f,z), new Vector3(.6f,.72f,.8f),wall);
                Box("Terminal", new Vector3(-3.6f,1.25f,z), new Vector3(.10f,.5f,.65f),floor);
            }
            else if (id == "R02_STORAGE")
                Box("StorageCabinet", new Vector3(-3.8f,1,1.6f), new Vector3(1.2f,2,.65f),accent);
            else if (id == "R03_WAITING")
                for (var n=0;n<3;n++) Box("Seat"+n,new Vector3(-1-n,.45f,1.6f),new Vector3(.7f,.12f,.6f),accent);
            else
            {
                Box("ClinicalBed",new Vector3(-3,.7f,1.6f),new Vector3(2,.3f,.8f),wall);
                Box("Trolley",new Vector3(-1,.5f,1.7f),new Vector3(.7f,1,.7f),accent);
            }
        }
    }
}
