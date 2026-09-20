namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public static class ClinicalCourseScenes
    {
        public static bool IsLaterLesson(string id) => id == "baobab" || id == "bottle_tree" ||
            id == "ceiba" || id == "macrozamia" || id == "welwitschia";
        public static bool Contains(string id) => id == "giant_saguaro" || IsLaterLesson(id);
    }
}
