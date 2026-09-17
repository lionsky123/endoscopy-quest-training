namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>Explicit user confirmation of a configured fieldbook entry; not a detected QR.</summary>
    public interface IConfirmedContentEntry
    {
        bool TryOpenConfirmedEntry(string entryValue);
    }
}
