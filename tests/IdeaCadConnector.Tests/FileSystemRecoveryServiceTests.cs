using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Workspace.Recovery;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class FileSystemRecoveryServiceTests : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly FileSystemRecoveryService _service;
        private readonly string _sourceDir;

        public FileSystemRecoveryServiceTests()
        {
            _workspaceRoot = Path.Combine(
                Path.GetTempPath(),
                "FileSystemRecoveryServiceTests",
                Guid.NewGuid().ToString("N"));
            _sourceDir = Path.Combine(_workspaceRoot, "source");
            Directory.CreateDirectory(_sourceDir);
            _service = new FileSystemRecoveryService(_workspaceRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_workspaceRoot))
            {
                try { Directory.Delete(_workspaceRoot, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public async Task Success_CreatesBackupWithVerifiedHash()
        {
            var sourcePath = CreateSourceFile("test.ics", "CAD content for success test");

            var result = await _service.CreateRecoveryCopyAsync("CAD-001", sourcePath, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.NotNull(result.SourceHash);
            Assert.NotNull(result.BackupHash);
            Assert.Equal(result.SourceHash, result.BackupHash, ignoreCase: true);
        }

        [Fact]
        public async Task Success_BackupContentMatchesSource()
        {
            var sourcePath = CreateSourceFile("hash-check.ics", "Content for hash verification");

            var result = await _service.CreateRecoveryCopyAsync("CAD-002", sourcePath, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.True(File.Exists(result.BackupPath));
            var content = File.ReadAllText(result.BackupPath);
            Assert.Equal("Content for hash verification", content);
        }

        [Fact]
        public async Task BackupStoredUnderDotIdeaPdmRecovery()
        {
            var sourcePath = CreateSourceFile("recovery-path.ics", "Content");

            var result = await _service.CreateRecoveryCopyAsync("CAD-003", sourcePath, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Contains(
                Path.Combine(".idea-pdm", "recovery", "CAD-003"),
                result.BackupPath);
        }

        [Fact]
        public async Task SourceNotFound_ReturnsFailure()
        {
            var missingPath = Path.Combine(_sourceDir, "nonexistent.ics");

            var result = await _service.CreateRecoveryCopyAsync("CAD-004", missingPath, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.True(
                result.ErrorMessage.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected error to contain 'not found' but got: {result.ErrorMessage}");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task InvalidCadId_ReturnsFailure(string cadId)
        {
            var sourcePath = CreateSourceFile("invalid-cad.ics", "content");

            var result = await _service.CreateRecoveryCopyAsync(cadId, sourcePath, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task InvalidPath_ReturnsFailure(string path)
        {
            var result = await _service.CreateRecoveryCopyAsync("CAD-005", path, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task CopyFails_ReturnsFailure()
        {
            var sourcePath = CreateSourceFile("copy-fail.ics", "CAD content");

            using (var exclusiveLock = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var result = await _service.CreateRecoveryCopyAsync("CAD-006", sourcePath, CancellationToken.None);
                Assert.False(result.Succeeded);
            }
        }

        [Fact]
        public async Task ConcurrentCadIds_UseIsolatedDirectories()
        {
            var src1 = CreateSourceFile("concurrent-1.ics", "Content one");
            var src2 = CreateSourceFile("concurrent-2.ics", "Content two");

            var task1 = _service.CreateRecoveryCopyAsync("CAD-CON-1", src1, CancellationToken.None);
            var task2 = _service.CreateRecoveryCopyAsync("CAD-CON-2", src2, CancellationToken.None);

            var results = await Task.WhenAll(task1, task2);

            Assert.True(results[0].Succeeded);
            Assert.True(results[1].Succeeded);
            Assert.Contains("CAD-CON-1", results[0].BackupPath);
            Assert.Contains("CAD-CON-2", results[1].BackupPath);
        }

        [Fact]
        public async Task MultipleBackups_SameCadId_AllSucceed()
        {
            var sourcePath = CreateSourceFile("multiple.ics", "Multiple backups content");

            var first = await _service.CreateRecoveryCopyAsync("CAD-009", sourcePath, CancellationToken.None);
            Assert.True(first.Succeeded);

            var second = await _service.CreateRecoveryCopyAsync("CAD-009", sourcePath, CancellationToken.None);
            Assert.True(second.Succeeded);

            var backupDir = Path.Combine(_workspaceRoot, ".idea-pdm", "recovery", "CAD-009");
            Assert.True(Directory.Exists(backupDir));
            Assert.Equal(2, Directory.GetFiles(backupDir).Length);
        }

        private string CreateSourceFile(string name, string content)
        {
            var path = Path.Combine(_sourceDir, name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
