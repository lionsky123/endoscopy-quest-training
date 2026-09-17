using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public sealed class UserFault
    {
        public UserFault(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("A visitor-facing message is required.", nameof(message));
            Message = message;
        }

        public string Message { get; }
    }
}
