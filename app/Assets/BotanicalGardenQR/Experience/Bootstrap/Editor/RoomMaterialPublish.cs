using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    internal static class RoomMaterialPublish
    {
        // Preserve source surface settings but detach the FBX container dependency.
        internal static void IsolateEmbeddedMaterials(GameObject root,string output)
        {
            var copies=new Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int n=0;n<materials.Length;n++)
                {
                    var source=materials[n];
                    if(!source || !AssetDatabase.GetAssetPath(source).EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))continue;
                    if(!copies.TryGetValue(source,out var copy))
                    {
                        // Embedded textures must be explicitly extracted, never silently dropped.
                        foreach(var property in source.GetTexturePropertyNames())
                            if(AssetDatabase.GetAssetPath(source.GetTexture(property)).EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Embedded texture requires extraction: "+source.name+" / "+property);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
                        string path=output+"/SourceSurface_"+guid+"_"+localId+".mat";
                        copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(copy){EditorUtility.CopySerialized(source,copy);EditorUtility.SetDirty(copy);}
                        else {copy=new Material(source);AssetDatabase.CreateAsset(copy,path);}
                        copies.Add(source,copy);
                    }
                    materials[n]=copy;
                }
                renderer.sharedMaterials=materials;
            }
        }
    }
}
