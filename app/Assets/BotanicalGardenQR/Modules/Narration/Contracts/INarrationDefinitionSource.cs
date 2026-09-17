using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Narration.Contracts { public interface INarrationDefinitionSource { bool TryGet(SceneId sceneId,out NarrationDefinition definition); } }
