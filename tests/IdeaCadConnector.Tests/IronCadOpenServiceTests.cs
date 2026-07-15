using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class IronCadOpenServiceTests
    {
        [Fact]
        public void IsIronCadAvailable_ExecutableExists_ReturnsTrue()
        {
            var adapter = new StubCadAdapter();
            var path = GetTestExecutablePath();
            try
            {
                CreateDummyFile(path);
                var service = new IronCadOpenService(adapter, path);
                Assert.True(service.IsIronCadAvailable);
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [Fact]
        public void IsIronCadAvailable_ExecutableMissing_ReturnsFalse()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, @"C:\nonexistent\IronCAD.exe", EmptyResolver());
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public void IsIronCadAvailable_NullPath_ReturnsFalse()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, null, EmptyResolver());
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public void IsIronCadAvailable_InvalidConfiguredPathUsesDiscoveredExecutable()
        {
            var path = GetTestExecutablePath();
            try
            {
                CreateDummyFile(path);
                var resolver = new IronCadExecutableResolver(
                    () => Array.Empty<string>(),
                    () => new[] { path },
                    () => Array.Empty<string>());
                var service = new IronCadOpenService(new StubCadAdapter(), @"C:\missing\IRONCAD.exe", resolver);

                Assert.True(service.IsIronCadAvailable);
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [Fact]
        public async Task OpenCadFileAsync_NullRequest_ReturnsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_NullPath_ReturnsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = null, IsTrustedSource = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_EmptyPath_ReturnsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = "", IsTrustedSource = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_RemoteUrl_ReturnsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = "http://evil.com/trojan.ics", IsRemoteUrl = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("Remote", result.ErrorMessage);
        }

        [Fact]
        public async Task OpenCadFileAsync_ZeroByteFile_ReturnsFileUploadNotFound()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = "empty.ics", FileSize = 0, IsTrustedSource = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.FileUploadNotFound, result.ErrorCode);
            Assert.Contains("zero", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task OpenCadFileAsync_UnapprovedExtension_ReturnsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = "file.exe", FileSize = 100, IsTrustedSource = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("extension", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task OpenCadFileAsync_UntrustedSource_ReturnsPermissionDenied()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = "test.ics", FileSize = 100, IsTrustedSource = false },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.PermissionDenied, result.ErrorCode);
            Assert.Contains("untrusted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task OpenCadFileAsync_FileNotFound_ReturnsCadNotFound()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var result = await service.OpenCadFileAsync(
                new IronCadOpenRequest { FilePath = @"C:\nonexistent\file.ics", FileSize = 100, IsTrustedSource = true },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.CadNotFound, result.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_IronCadNotAvailable_ReturnsFileUploadNotFound()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, @"C:\nonexistent\IronCAD.exe", EmptyResolver());
            var tempFile = Path.GetTempFileName() + ".ics";
            try
            {
                File.WriteAllText(tempFile, "test");
                var result = await service.OpenCadFileAsync(
                    new IronCadOpenRequest { FilePath = tempFile, FileSize = 4, IsTrustedSource = true },
                    CancellationToken.None);

                Assert.False(result.Success);
                Assert.Equal(ArasErrorCode.FileUploadNotFound, result.ErrorCode);
            }
            finally
            {
                DeleteIfExists(tempFile);
            }
        }

        [Fact]
        public async Task OpenCadFileAsync_Success_CallsAdapter()
        {
            var adapter = new StubCadAdapter();
            var path = GetTestExecutablePath();
            try
            {
                CreateDummyFile(path);
                var service = new IronCadOpenService(adapter, path);
                var tempFile = Path.GetTempFileName() + ".ics";
                try
                {
                    File.WriteAllText(tempFile, "test");
                    var result = await service.OpenCadFileAsync(
                        new IronCadOpenRequest { FilePath = tempFile, OpenMode = CadOpenMode.ReadOnly, FileSize = 4, IsTrustedSource = true },
                        CancellationToken.None);

                    Assert.True(result.Success);
                    Assert.True(adapter.OpenCalled);
                    Assert.Equal(tempFile, adapter.LastFilePath);
                    Assert.Equal(CadOpenMode.ReadOnly, adapter.LastOpenMode);
                }
                finally
                {
                    DeleteIfExists(tempFile);
                }
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [Fact]
        public async Task OpenCadFileAsync_Canceled_Throws()
        {
            var adapter = new StubCadAdapter();
            var path = GetTestExecutablePath();
            try
            {
                CreateDummyFile(path);
                var service = new IronCadOpenService(adapter, path);
                var tempFile = Path.GetTempFileName() + ".ics";
                try
                {
                    File.WriteAllText(tempFile, "test");
                    using var cts = new CancellationTokenSource();
                    cts.Cancel();

                    await Assert.ThrowsAsync<OperationCanceledException>(() =>
                        service.OpenCadFileAsync(
                            new IronCadOpenRequest { FilePath = tempFile, FileSize = 4, IsTrustedSource = true },
                            cts.Token));
                }
                finally
                {
                    DeleteIfExists(tempFile);
                }
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [Fact]
        public async Task OpenCadFileAsync_Success_ReturnsIronCadOpenResult()
        {
            var adapter = new StubCadAdapter();
            var path = GetTestExecutablePath();
            try
            {
                CreateDummyFile(path);
                var service = new IronCadOpenService(adapter, path);
                var tempFile = Path.GetTempFileName() + ".ics";
                try
                {
                    File.WriteAllText(tempFile, "test");
                    var result = await service.OpenCadFileAsync(
                        new IronCadOpenRequest { FilePath = tempFile, FileSize = 4, IsTrustedSource = true },
                        CancellationToken.None);

                    Assert.IsType<IronCadOpenResult>(result);
                    Assert.True(result.Success);
                    Assert.Null(result.ErrorMessage);
                    Assert.Null(result.ErrorCode);
                }
                finally
                {
                    DeleteIfExists(tempFile);
                }
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        private static IronCadOpenService CreateService(ICadApplicationAdapter adapter)
        {
            return new IronCadOpenService(adapter, GetTestExecutablePath());
        }

        private static IronCadExecutableResolver EmptyResolver()
        {
            return new IronCadExecutableResolver(
                () => Array.Empty<string>(),
                () => Array.Empty<string>(),
                () => Array.Empty<string>());
        }

        private static string GetTestExecutablePath()
        {
            return Path.Combine(Path.GetTempPath(), "IronCadOpenServiceTest", "IRONCAD.exe");
        }

        private static void CreateDummyFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "dummy");
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }

        private sealed class StubCadAdapter : ICadApplicationAdapter
        {
            public bool OpenCalled { get; private set; }
            public string LastFilePath { get; private set; }
            public CadOpenMode LastOpenMode { get; private set; }

            public string AuthoringTool => "IronCAD";
            public string AuthoringToolVersion => "2025";

            public CadDocumentInfo GetActiveDocumentInfo() => null;
            public CadMetadata ReadMetadata() => new CadMetadata();
            public void WriteMetadata(CadMetadata metadata) { }

            public Task OpenDocumentAsync(string filePath, CadOpenMode openMode, CancellationToken ct)
            {
                OpenCalled = true;
                LastFilePath = filePath;
                LastOpenMode = openMode;
                return Task.FromResult(0);
            }
        }
    }
}
