using System;
using System.IO;

namespace IdeaCadConnector.Desktop.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Idea",
            "IdeaCadConnector");

        private static readonly string LanguageFilePath = Path.Combine(SettingsDirectory, "language.txt");

        public static string LoadLanguage()
        {
            try
            {
                if (!File.Exists(LanguageFilePath))
                    return null;

                return File.ReadAllText(LanguageFilePath)?.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static void SaveLanguage(string cultureName)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(LanguageFilePath, cultureName);
            }
            catch
            {
            }
        }
    }
}
