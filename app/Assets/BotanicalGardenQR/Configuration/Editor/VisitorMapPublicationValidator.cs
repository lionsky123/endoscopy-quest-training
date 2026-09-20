using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    /// <summary>Validates the standard generated model/route unit without publishing or fixing it.</summary>
    public static class VisitorMapPublicationValidator
    {
        public const string DefinitionPath = "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json";
        public const string SourcePath = "Tools/Authoring/visitor-map.json";
        public const string ModelPath = "Assets/StreamingAssets/VisitorAtlasHub/basement_map_8x8_6points.glb";
        public static void Validate(ICollection<ConfigurationIssue> issues)
        {
            try
            {
                var definition = JsonUtility.FromJson<MapDefinition>(File.ReadAllText(DefinitionPath));
                MapDefinitionValidation.Validate(definition);
                if (!string.IsNullOrEmpty(definition.roomResource))
                {
                    ValidateRoom(definition);
                    return;
                }
                if (Digest(File.ReadAllBytes(SourcePath)) != definition.sourceDigest || Digest(File.ReadAllBytes(ModelPath)) != definition.modelDigest)
                    throw new InvalidOperationException("Published map source/model digest mismatch; regenerate the authored model and route unit.");
                var data = File.ReadAllBytes(ModelPath);
                if (data.Length < 20 || BitConverter.ToUInt32(data, 0) != 0x46546c67)
                    throw new InvalidOperationException("Invalid GLB.");
                var length = BitConverter.ToInt32(data, 12);
                if (length <= 0 || length > data.Length - 20)
                    throw new InvalidOperationException("Invalid GLB JSON chunk.");
                var gltf = JsonUtility.FromJson<Gltf>(Encoding.UTF8.GetString(data, 20, length));
                if (gltf.nodes == null || !gltf.nodes.Any(n => n.extras?.MapSourceDigest == definition.sourceDigest))
                    throw new InvalidOperationException("The model was not generated from the published map source.");
                foreach (var point in definition.points)
                {
                    var node = gltf.nodes.SingleOrDefault(n => n.name == "MapPoint_" + point.id);
                    if (node?.translation == null || node.translation.Length != 3 || MapPosition.Distance(new MapPosition(-node.translation[0], node.translation[1], node.translation[2]), point.position) > .001f)
                        throw new InvalidOperationException("Model point geometry disagrees with its navigation point: " + point.id);
                }


            }
            catch (Exception exception)when (exception is IOException || exception is ArgumentException || exception is InvalidOperationException)
            {
                issues.Add(new ConfigurationIssue("CFG-MAP-001", DefinitionPath, exception.Message));
            }
        }

        static string Digest(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }

        static void ValidateRoom(MapDefinition definition)
        {
            const string roomPath = "Assets/EndoscopyTheme/Resources/EndoscopyRoom/";
            if (definition.roomResource != "EndoscopyRoom" || definition.scale != 1f || definition.points.Length != 6)
                throw new InvalidOperationException("The VR room requires six metre-scale stations.");
            if (Digest(File.ReadAllBytes(SourcePath)) != definition.sourceDigest ||
                Digest(File.ReadAllBytes(roomPath + "geometry.bytes")) != definition.modelDigest)
                throw new InvalidOperationException("Room geometry/source changed; republish the room map.");
            var source = JsonUtility.FromJson<MapDefinition>(File.ReadAllText(SourcePath));
            if (source == null || source.points == null || source.points.Length != definition.points.Length ||
                MapPosition.Distance(source.start, definition.start) > .001f)
                throw new InvalidOperationException("Published room start/stations disagree with source.");
            for (var i = 0; i < definition.points.Length; i++)
                if (source.points[i].id != definition.points[i].id ||
                    MapPosition.Distance(source.points[i].position, definition.points[i].position) > .001f)
                    throw new InvalidOperationException("Room station differs from source: " + definition.points[i].id);
            if (!File.Exists(roomPath + "manifest.json") || !File.Exists(roomPath + "RoomSurface.mat"))
                throw new InvalidOperationException("Room material publication is incomplete.");
        }

        [Serializable]
        sealed class Gltf
        {
            public Node[] nodes;
        }

        [Serializable]
        sealed class Node
        {
            public string name;
            public float[] translation;
            public Extras extras;
        }

        [Serializable]
        sealed class Extras
        {
            public string MapSourceDigest;
        }
    }
}
