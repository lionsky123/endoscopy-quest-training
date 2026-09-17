namespace BotanicalGardenQR.Experience.Contracts.Flow
{
    public interface IFeaturePageLifecycle
    {
        FeaturePageId PageId { get; }
        FlowResult Prepare(SessionToken session, SceneId sceneId);
        FlowResult Activate(SessionToken session);
        FlowResult Deactivate(SessionToken session);
        FlowResult Release(SessionToken session);
    }
}
