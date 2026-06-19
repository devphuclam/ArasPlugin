using System;
using System.IO;
using IdeaCadConnector.Core.Validation;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceService
    {
        private readonly WorkspaceOptions _options;

        public WorkspaceService(WorkspaceOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
        }

        public string GetCadPartPath(string partNumber)
        {
            var fileName = CadFileNamingRules.GetLocalPlaceholderFileName(partNumber);
            var root = string.IsNullOrWhiteSpace(_options.RootPath)
                ? GetDefaultRootPath()
                : _options.RootPath;

            var company = string.IsNullOrWhiteSpace(_options.CompanyCode)
                ? _options.DefaultCompanyCode
                : _options.CompanyCode.Trim();

            return Path.Combine(root, company, partNumber, fileName);
        }

        public void EnsureDirectoryForFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string GetDefaultRootPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Idea", "ArasCadWorkspace");
        }
    }
}
