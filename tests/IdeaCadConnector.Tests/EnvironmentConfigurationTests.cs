using System;
using System.IO;
using IdeaCadConnector.Core.Configuration;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class EnvironmentConfigurationTests : IDisposable
    {
        private readonly string _tempDir;

        public EnvironmentConfigurationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "IdeaCadConnector_EnvConfigTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Load_WhenNoFileExists_ReturnsDefaultsAndWarning()
        {
            var result = EnvironmentConfigurationLoader.LoadFromPath(Path.Combine(_tempDir, "nonexistent.json"));
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("not found"));
            Assert.NotNull(result.Configuration);
        }

        [Fact]
        public void Load_WhenFileIsValidTemplate_ParsesSuccessfully()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""environmentName"": ""UAT"",
                ""aras"": {
                    ""baseUrl"": ""https://aras.example.com/InnovatorServer"",
                    ""database"": ""InnovatorSolutions""
                },
                ""local"": {
                    ""vaultCacheDirectory"": ""%LOCALAPPDATA%/IdeaCadConnector/VaultCache""
                },
                ""roles"": {
                    ""managerUsers"": [""TPTKC""],
                    ""reviewerUsers"": [""TNTKC""],
                    ""contributorUsers"": [""NVTKC""],
                    ""readOnlyUsers"": [""NVLCR"", ""PM""]
                }
            }";
            string path = Path.Combine(_tempDir, "valid.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            Assert.Equal("UAT", result.Configuration.EnvironmentName);
            Assert.Equal("https://aras.example.com/InnovatorServer", result.Configuration.Aras.BaseUrl);
            Assert.Equal("InnovatorSolutions", result.Configuration.Aras.Database);
            Assert.Contains("TPTKC", result.Configuration.Roles.ManagerUsers);
            Assert.Contains("TNTKC", result.Configuration.Roles.ReviewerUsers);
            Assert.Contains("NVTKC", result.Configuration.Roles.ContributorUsers);
            Assert.Contains("NVLCR", result.Configuration.Roles.ReadOnlyUsers);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentLocalAppDataExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%LOCALAPPDATA%/IdeaCadConnector/Logs");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%LOCALAPPDATA%", expanded);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentAppDataExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%APPDATA%/IdeaCadConnector");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%APPDATA%", expanded);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentUserProfileExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%USERPROFILE%/IdeaCadConnector");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%USERPROFILE%", expanded);
        }

        [Fact]
        public void Load_WhenMalformedJson_ReturnsValidationError()
        {
            string path = Path.Combine(_tempDir, "malformed.json");
            File.WriteAllText(path, "{ this is not valid json }");

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("malformed JSON"));
        }

        [Fact]
        public void Load_WhenUnsupportedSchemaVersion_ReturnsError()
        {
            string json = @"{ ""schemaVersion"": 99 }";
            string path = Path.Combine(_tempDir, "badver.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("Unsupported schemaVersion"));
        }

        [Fact]
        public void Load_WhenEmptyFile_ReturnsWarningWithDefaults()
        {
            string path = Path.Combine(_tempDir, "empty.json");
            File.WriteAllText(path, "");

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("empty"));
        }

        [Fact]
        public void Load_WhenPasswordKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""password"": ""should-not-be-here""
                }
            }";
            string path = Path.Combine(_tempDir, "withpass.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("password"));
        }

        [Fact]
        public void Load_WhenTokenKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""token"": ""some-token""
                }
            }";
            string path = Path.Combine(_tempDir, "withtoken.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("token"));
        }

        [Fact]
        public void Load_WhenSecretKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""secret"": ""my-secret""
                }
            }";
            string path = Path.Combine(_tempDir, "withsecret.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("secret"));
        }

        [Fact]
        public void Load_WhenRoleOverride_ManagerUsersMapsTptkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""managerUsers"": [""TPTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_mgr.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("TPTKC", result.Configuration.Roles.ManagerUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ReviewerUsersMapsTntkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""reviewerUsers"": [""TNTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_rev.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("TNTKC", result.Configuration.Roles.ReviewerUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ContributorUsersMapsNvtkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""contributorUsers"": [""NVTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_contrib.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("NVTKC", result.Configuration.Roles.ContributorUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ReadOnlyUsersIncludesNvlcrPmKhachHang()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""readOnlyUsers"": [""NVLCR"", ""PM"", ""KhachHang"", ""Customer""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_ro.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("NVLCR", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("PM", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("KhachHang", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("Customer", result.Configuration.Roles.ReadOnlyUsers);
        }

        [Fact]
        public void Load_WhenEmptyIronCadPath_DefaultsSafely()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""local"": {
                    ""ironCadExecutablePath"": """"
                }
            }";
            string path = Path.Combine(_tempDir, "emptyironcad.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Empty(result.Configuration.Local.IronCadExecutablePath);
        }
    }
}
