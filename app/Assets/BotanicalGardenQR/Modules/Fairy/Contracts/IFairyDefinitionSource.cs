namespace BotanicalGardenQR.Fairy.Contracts
{
    public interface IFairyDefinitionSource
    {
        bool TryGet(out FairyDefinition definition);
    }
}
