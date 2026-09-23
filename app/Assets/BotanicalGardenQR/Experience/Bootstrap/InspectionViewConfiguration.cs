using System;
using System.Linq;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // Shared authoring data: small coordinates only, never references all room models.
    [CreateAssetMenu(menuName="Endoscopy/Inspection view configuration")]
    public sealed class InspectionViewConfiguration : ScriptableObject
    {
        public const string ResourcePath="ClinicalCourse/inspection-views";
        [Serializable] public sealed class View
        {
            public string id;
            public Vector3 position;
            public float yaw;
            public Vector3 Forward => Quaternion.Euler(0,yaw,0)*Vector3.forward;
        }
        [Serializable] public sealed class Room
        {
            public string id;
            public View initial=new View {id="Initial"};
            public View[] inspections=Array.Empty<View>();
        }
        public Room[] rooms=Array.Empty<Room>();
        public static InspectionViewConfiguration Load()=>Resources.Load<InspectionViewConfiguration>(ResourcePath);
        public Room Find(string id)=>rooms.FirstOrDefault(r=>r.id==id);
        public View Find(string room,string task)=>Find(room)?.inspections.FirstOrDefault(v=>v.id==task);
    }
}
