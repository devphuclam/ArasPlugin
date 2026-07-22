using System.Collections.Generic;

namespace IdeaCadConnector.Core.Configuration
{
    public sealed class EnvironmentConfiguration
    {
        public int SchemaVersion { get; set; } = 1;
        public string EnvironmentName { get; set; } = "Default";
        public ArasConfiguration Aras { get; set; } = new();
        public LocalConfiguration Local { get; set; } = new();
        public RoleConfiguration Roles { get; set; } = new();
        public DiagnosticsConfiguration Diagnostics { get; set; } = new();
    }

    public sealed class ArasConfiguration
    {
        public string BaseUrl { get; set; } = "";
        public string Database { get; set; } = "";
        public string OpenInArasBaseUrl { get; set; } = "";
        public string VaultId { get; set; } = "";
        public string OAuthClientId { get; set; } = "IOMApp";
        public string OAuthScope { get; set; } = "Innovator";
        public int? DefaultMaxSearchResults { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    public sealed class LocalConfiguration
    {
        public string VaultCacheDirectory { get; set; } = "%LOCALAPPDATA%/IdeaCadConnector/VaultCache";
        public string IronCadExecutablePath { get; set; } = "";
        public bool OpenDownloadedCadAfterDownload { get; set; } = false;
    }

    public sealed class RoleConfiguration
    {
        public List<string> ManagerUsers { get; set; } = new();
        public List<string> ReviewerUsers { get; set; } = new();
        public List<string> ContributorUsers { get; set; } = new();
        public List<string> ReadOnlyUsers { get; set; } = new();
        public List<string> PdmAdministratorUsers { get; set; } = new();
    }

    public sealed class DiagnosticsConfiguration
    {
        public string LogLevel { get; set; } = "Info";
        public bool EnableFileLogging { get; set; } = false;
        public string LogDirectory { get; set; } = "%LOCALAPPDATA%/IdeaCadConnector/Logs";
    }

    public sealed class EnvironmentConfigurationResult
    {
        public EnvironmentConfiguration Configuration { get; set; }
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public bool IsValid => Errors.Count == 0;
        public string SourcePath { get; set; }
    }
}
