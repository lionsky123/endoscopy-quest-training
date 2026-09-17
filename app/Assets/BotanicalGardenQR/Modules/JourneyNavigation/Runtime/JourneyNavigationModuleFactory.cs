using BotanicalGardenQR.JourneyNavigation.Contracts;
namespace BotanicalGardenQR.JourneyNavigation.Runtime
{
    public static class JourneyNavigationModuleFactory
    {
        public static IJourneyNavigation Create() => new JourneyNavigationController();
    }
}
