using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Video.Contracts
{
    public interface IVideoDefinitionSource { bool TryGet(SceneId sceneId, out VideoDefinition definition); }
}
