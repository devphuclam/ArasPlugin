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
        public void Factory_MissingExecutablePathReturnsActionableFailure()
        {
            var ex = Assert.Throws<FileNotFoundException>(() => IronCadAdapterFactory.Create(null));

            Assert.Contains("IronCAD executable path is not configured", ex.Message);
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
    }
}
