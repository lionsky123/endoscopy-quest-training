using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.ImageRing.Contracts
{
    public interface IImageRingDefinitionSource
    {
        bool TryGet(SceneId sceneId, out ImageRingDefinition definition);
    }
}
