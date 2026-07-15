using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmCadLaunchActionUiTests
    {
        [Fact]
        public void PdmProjectsView_CadActionUsesOneCommandAndDisabledTooltip()
        {
            var xaml = ReadRepoFile("src", "IdeaCadConnector.Desktop", "PdmProjectsView.xaml");

            Assert.Contains("Command=\"{Binding OpenInIronCadCommand}\"", xaml);
            Assert.Contains("ToolTip=\"{Binding OpenInIronCadToolTip}\"", xaml);
            Assert.Contains("Visibility=\"{Binding HasOpenInIronCadAction", xaml);
            Assert.DoesNotContain("Command=\"{Binding CheckoutCommand}\"", xaml);
        }

        [Fact]
        public void PdmProjectsViewModel_ProjectsAllCadActionPropertiesFromSharedState()
        {
            var source = ReadRepoFile("src", "IdeaCadConnector.Desktop", "PdmProjectsViewModel.cs");

            Assert.Contains("PdmCadLaunchActionState.Create", source);
            Assert.Contains("public bool CanOpenInIronCad => CadLaunchActionState.IsEnabled", source);
            Assert.Contains("public bool HasOpenInIronCadAction => CadLaunchActionState.IsVisible", source);
            Assert.Contains("public string OpenInIronCadModeText => Loc(CadLaunchActionState.LabelKey)", source);
            Assert.Contains("public string OpenInIronCadToolTip", source);
            Assert.Contains("nameof(OpenInIronCadToolTip)", source);
        }

        [Theory]
        [InlineData("PdmCheckoutAndOpenIronCad")]
        [InlineData("PdmOpenCheckedOutIronCad")]
        [InlineData("PdmCadLaunchUnavailable")]
        [InlineData("PdmCadLaunchBusy")]
        [InlineData("PdmCadLaunchConnectToAras")]
        [InlineData("PdmCadLaunchRefreshCad")]
        [InlineData("PdmCadLaunchRefreshState")]
        [InlineData("PdmCadLaunchNoReadableFile")]
        public void CadLaunchLocalizationKey_HasEnglishVietnameseAndJapaneseValues(string key)
        {
            var keys = ReadRepoFile("src", "IdeaCadConnector.Core", "Localization", "TranslationKeys.cs");
            var resources = ReadRepoFile("src", "IdeaCadConnector.Core", "Localization", "TranslationResources.cs");

            Assert.Contains("const string " + key + " = \"" + key + "\"", keys);
            Assert.Equal(3, Regex.Matches(resources, "\\[TranslationKeys\\." + key + "\\]").Count);
        }

        private static string ReadRepoFile(params string[] segments)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "IdeaCadConnector.sln")))
                root = root.Parent;
            if (root == null)
                throw new DirectoryNotFoundException("Repository root not found.");

            var path = root.FullName;
            foreach (var segment in segments)
                path = Path.Combine(path, segment);
            return File.ReadAllText(path);
        }
    }
}
