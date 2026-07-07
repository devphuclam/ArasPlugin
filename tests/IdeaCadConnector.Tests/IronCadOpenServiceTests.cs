using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
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
            var service = new IronCadOpenService(adapter, @"C:\nonexistent\IronCAD.exe");
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public void IsIronCadAvailable_NullPath_ReturnsFalse()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, null);
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public async Task OpenCadFileAsync_NullPath_ThrowsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.OpenCadFileAsync(null, CadOpenMode.ReadOnly, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_EmptyPath_ThrowsValidationFailed()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.OpenCadFileAsync("", CadOpenMode.ReadOnly, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_FileNotFound_ThrowsCadNotFound()
        {
            var adapter = new StubCadAdapter();
            var service = CreateService(adapter);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.OpenCadFileAsync(@"C:\nonexistent\file.ics", CadOpenMode.ReadOnly, CancellationToken.None));

            Assert.Equal(ArasErrorCode.CadNotFound, ex.ErrorCode);
        }

        [Fact]
        public async Task OpenCadFileAsync_IronCadNotAvailable_ThrowsFileUploadNotFound()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, @"C:\nonexistent\IronCAD.exe");
            var tempFile = Path.GetTempFileName() + ".ics";
            try
            {
                File.WriteAllText(tempFile, "test");
                var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                    service.OpenCadFileAsync(tempFile, CadOpenMode.ReadOnly, CancellationToken.None));

                Assert.Equal(ArasErrorCode.FileUploadNotFound, ex.ErrorCode);
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
                    await service.OpenCadFileAsync(tempFile, CadOpenMode.ReadOnly, CancellationToken.None);

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
                        service.OpenCadFileAsync(tempFile, CadOpenMode.ReadOnly, cts.Token));
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
