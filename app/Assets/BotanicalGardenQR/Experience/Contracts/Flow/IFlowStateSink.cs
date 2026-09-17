namespace BotanicalGardenQR.Experience.Contracts.Flow
{
    public interface IFlowStateSink
    {
        void OnStateChanged(ExperienceFlowState state);
    }
}
