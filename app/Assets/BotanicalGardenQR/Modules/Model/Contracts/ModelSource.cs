using System;
using UnityEngine;
namespace BotanicalGardenQR.Model.Contracts
{
    public enum ModelSourceKind { Prefab, Glb }
    public sealed class ModelSource
    {
        ModelSource(ModelSourceKind kind,UnityEngine.Object asset,string path) { Kind=kind; Asset=asset; StreamingAssetsPath=path; }
        public ModelSourceKind Kind { get; }
        public UnityEngine.Object Asset { get; }
        public string StreamingAssetsPath { get; }
        public static ModelSource FromPrefab(GameObject prefab)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            return new ModelSource(ModelSourceKind.Prefab,prefab,null);
        }
        public static ModelSource FromGlbAsset(UnityEngine.Object asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new ModelSource(ModelSourceKind.Glb,asset,null);
        }
        public static ModelSource FromGlbStreamingAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A StreamingAssets-relative GLB path is required.",nameof(path));
            if (System.IO.Path.IsPathRooted(path)) throw new ArgumentException("The path must be relative to StreamingAssets.",nameof(path));
            var normalized=path.Trim().Replace('\\','/');
            if(normalized=="."||normalized==".."||normalized.StartsWith("./",StringComparison.Ordinal)||normalized.StartsWith("../",StringComparison.Ordinal)||normalized.Contains("/../"))
                throw new ArgumentException("The path must stay within StreamingAssets.",nameof(path));
            return new ModelSource(ModelSourceKind.Glb,null,normalized);
        }
    }
}
