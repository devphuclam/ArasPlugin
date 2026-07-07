using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdeaCadConnector.Core.Localization;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class LibraryLocalizationTests
    {
        [Fact]
        public void LibraryView_UsesLocalizedKeys_InAllSupportedCultures()
        {
            var repoRoot = FindRepoRoot();
            var xamlPath = Path.Combine(repoRoot, "src", "IdeaCadConnector.Desktop", "LibraryView.xaml");
            var xaml = File.ReadAllText(xamlPath);

            var keys = Regex.Matches(xaml, @"Path=\[(?<key>[^\]]+)\]")
                .Cast<Match>()
                .Select(match => match.Groups["key"].Value)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.NotEmpty(keys);

            var locales = GetLocales();
            foreach (var culture in TranslationResources.SupportedCultures)
            {
                Assert.True(locales.ContainsKey(culture), $"Missing locale dictionary for {culture}.");
                foreach (var key in keys)
                {
                    Assert.True(
                        locales[culture].ContainsKey(key),
                        $"Missing localization key '{key}' for culture '{culture}'.");
                }
            }
        }

        private static Dictionary<string, Dictionary<string, string>> GetLocales()
        {
            var field = typeof(TranslationResources).GetField("_locales", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);

            var value = field.GetValue(null);
            Assert.IsType<Dictionary<string, Dictionary<string, string>>>(value);
            return (Dictionary<string, Dictionary<string, string>>)value;
        }

        private static string FindRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                if (File.Exists(Path.Combine(dir, "IdeaCadConnector.sln")) &&
                    Directory.Exists(Path.Combine(dir, "src")) &&
                    Directory.Exists(Path.Combine(dir, "tests")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
