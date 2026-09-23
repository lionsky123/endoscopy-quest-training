#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // An explicit editor platform adapter. Never compiled into the Quest player.
    internal static class InspectionEditorPreview
    {
        internal const string Preference = "Endoscopy.Inspection.UseHeadsetInEditor";
        internal static bool Enabled => !EditorPrefs.GetBool(Preference, false);

        internal static void Configure(VisitorRuntimeBindings bindings)
        {
            var platform = bindings.Platform;
            foreach (var manager in platform.XrRigRoot.GetComponentsInChildren<OVRManager>(true)) manager.enabled=false;
            var rig=platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
            if(rig){rig.EnsureGameObjectIntegrity();rig.enabled=false;}
            platform.InteractionRigRoot.gameObject.SetActive(false);
            foreach(var camera in platform.XrRigRoot.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            platform.Viewer.localPosition=new Vector3(0,1.2f,0);
            platform.Viewer.localRotation=Quaternion.identity;
            var view=platform.Viewer.GetComponent<Camera>();
            view.enabled=true;view.stereoTargetEye=StereoTargetEyeMask.None;view.fieldOfView=78;
        }
    }
}
#endif
