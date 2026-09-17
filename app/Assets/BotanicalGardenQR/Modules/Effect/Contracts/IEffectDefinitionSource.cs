using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Effect.Contracts { public interface IEffectDefinitionSource { bool TryGet(SceneId sceneId,out EffectDefinition definition); } }
