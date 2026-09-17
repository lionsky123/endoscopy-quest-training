using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Model.Contracts { public interface IModelDefinitionSource { bool TryGet(SceneId sceneId, out ModelDefinition definition); } }
