using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Workspace.Models;

namespace IdeaCadConnector.Workspace.Recovery
{
    public sealed class FileSystemRecoveryService : IRecoveryCopyService
    {
        private readonly string _workspaceRoot;

        public FileSystemRecoveryService(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                throw new ArgumentException("Workspace root must not be null or empty.", nameof(workspaceRoot));
            _workspaceRoot = workspaceRoot;
        }

        public async Task<RecoveryCopyResult> CreateRecoveryCopyAsync(
            string cadId, string workingFilePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId))
                return Fail("CAD ID must not be null or empty.");
            if (string.IsNullOrWhiteSpace(workingFilePath))
                return Fail("Working file path must not be null or empty.");
            if (!File.Exists(workingFilePath))
                return Fail($"Working file not found: {workingFilePath}");

            string backupPath = null;

            try
            {
                string sourceHash;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(workingFilePath))
                {
                    ct.ThrowIfCancellationRequested();
                    var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), ct);
                    sourceHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                }

                var backupDir = GetRecoveryDirectory(cadId);
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
                var fileName = Path.GetFileName(workingFilePath);
                backupPath = Path.Combine(backupDir, $"{timestamp}-{fileName}");

                await Task.Run(() => File.Copy(workingFilePath, backupPath, overwrite: false), ct);

                string backupHash;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(backupPath))
                {
                    ct.ThrowIfCancellationRequested();
                    var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), ct);
                    backupHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                }

                if (!string.Equals(sourceHash, backupHash, StringComparison.OrdinalIgnoreCase))
                {
                    SafeDelete(backupPath);
                    return Fail("Backup hash does not match source hash. Partial backup cleaned up.");
                }

                return new RecoveryCopyResult
                {
                    Succeeded = true,
                    BackupPath = backupPath,
                    SourceHash = sourceHash,
                    BackupHash = backupHash,
                    ErrorMessage = null
                };
            }
            catch (OperationCanceledException)
            {
                SafeDelete(backupPath);
                return Fail("Recovery copy creation was cancelled.");
            }
            catch (Exception ex)
            {
                SafeDelete(backupPath);
                return Fail($"Recovery copy failed: {ex.Message}");
            }
        }

        private static void SafeDelete(string path)
        {
            if (path != null && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }

        public string GetRecoveryDirectory(string cadId)
        {
            if (string.IsNullOrWhiteSpace(cadId))
                throw new ArgumentException("CAD ID must not be null or empty.", nameof(cadId));
            return Path.Combine(_workspaceRoot, ".idea-pdm", "recovery", cadId);
        }

        public Task CleanExpiredCopiesAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var recoveryRoot = Path.Combine(_workspaceRoot, ".idea-pdm", "recovery");
                if (!Directory.Exists(recoveryRoot))
                    return;

                foreach (var cadDir in Directory.GetDirectories(recoveryRoot))
                {
                    ct.ThrowIfCancellationRequested();
                    foreach (var file in Directory.GetFiles(cadDir))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var lastWrite = File.GetLastWriteTimeUtc(file);
                            if (DateTime.UtcNow - lastWrite > TimeSpan.FromDays(30))
                                File.Delete(file);
                        }
                        catch
                        {
                        }
                    }

                    try
                    {
                        if (Directory.GetFiles(cadDir).Length == 0)
                            Directory.Delete(cadDir);
                    }
                    catch
                    {
                    }
                }
            }, ct);
        }

        private static RecoveryCopyResult Fail(string message)
        {
            return new RecoveryCopyResult
            {
                Succeeded = false,
                ErrorMessage = message,
                BackupPath = null,
                SourceHash = null,
                BackupHash = null
            };
        }
    }
}
