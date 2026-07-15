using System;
using System.IO;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmIronCadAdapterTests
    {
        [Fact]
        public void Factory_PassesConfiguredExecutablePathToAdapter()
        {
            const string configuredPath = @"C:\Path\To\IRONCAD.exe";

            var adapter = IronCadAdapterFactory.Create(configuredPath);

            var externalAdapter = Assert.IsType<IronCadExternalAdapter>(adapter);
            Assert.Equal(configuredPath, externalAdapter.ConfiguredExecutablePath);
        }

        [Fact]
        public void Factory_MissingExecutablePathCreatesDiscoveringAdapter()
        {
            var adapter = Assert.IsType<IronCadExternalAdapter>(IronCadAdapterFactory.Create(null));

            Assert.Null(adapter.ConfiguredExecutablePath);
        }

        [Fact]
        public void ExternalAdapter_InvalidConfiguredPathUsesDiscoveredExecutable()
        {
            var executablePath = CreateTestExecutable();
            try
            {
                var resolver = new IronCadExecutableResolver(
                    () => Array.Empty<string>(),
                    () => Array.Empty<string>(),
                    () => new[] { executablePath });
                var adapter = new IronCadExternalAdapter(@"C:\missing\IRONCAD.exe", resolver);

                Assert.Equal(executablePath, adapter.ResolvedExecutablePath);
            }
            finally
            {
                File.Delete(executablePath);
                Directory.Delete(Path.GetDirectoryName(executablePath));
            }
        }

        [Fact]
        public void Factory_FromSessionUsesConfiguredExecutablePath()
        {
            string originalPath = AppSessionContext.Current.IronCadExecutablePath;
            try
            {
                AppSessionContext.Current.IronCadExecutablePath = @"C:\Path\To\ConfiguredIronCad.exe";

                var adapter = Assert.IsType<IronCadExternalAdapter>(IronCadAdapterFactory.CreateFromSession());

                Assert.Equal(AppSessionContext.Current.IronCadExecutablePath, adapter.ConfiguredExecutablePath);
            }
            finally
            {
                AppSessionContext.Current.IronCadExecutablePath = originalPath;
            }
        }

        [Fact]
        public void PdmProjectsViewModel_HasNoParameterlessProductionAdapterConstruction()
        {
            string sourcePath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "IdeaCadConnector.Desktop", "PdmProjectsViewModel.cs"));
            string source = File.ReadAllText(sourcePath);

            Assert.DoesNotContain("new IronCadExternalAdapter()", source);
        }

        private static string CreateTestExecutable()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PdmIronCadAdapterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "IRONCAD.exe");
            File.WriteAllText(path, "test");
            return path;
        }
    }
}
