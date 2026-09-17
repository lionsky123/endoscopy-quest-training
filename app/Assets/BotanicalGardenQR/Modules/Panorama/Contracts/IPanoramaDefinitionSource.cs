using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Panorama.Contracts { public interface IPanoramaDefinitionSource { bool TryGet(SceneId sceneId, out PanoramaDefinition definition); } }
