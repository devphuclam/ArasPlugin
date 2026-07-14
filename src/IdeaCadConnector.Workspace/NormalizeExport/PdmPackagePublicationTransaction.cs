using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackagePublicationTransaction
    {
        public PdmPackagePublicationTransaction(string stagingDirectory, string pendingDirectory, string finalDirectory)
        {
            StagingDirectory = Path.GetFullPath(stagingDirectory ?? throw new ArgumentNullException(nameof(stagingDirectory)));
            PendingDirectory = Path.GetFullPath(pendingDirectory ?? throw new ArgumentNullException(nameof(pendingDirectory)));
            FinalDirectory = Path.GetFullPath(finalDirectory ?? throw new ArgumentNullException(nameof(finalDirectory)));
        }

        public string StagingDirectory { get; private set; }
        public string PendingDirectory { get; private set; }
        public string FinalDirectory { get; private set; }

        public void MoveToPending()
        {
            if (string.Equals(StagingDirectory, PendingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(StagingDirectory))
                    throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Package staging directory is missing.");
                return;
            }
            if (!Directory.Exists(StagingDirectory) || Directory.Exists(PendingDirectory) || Directory.Exists(FinalDirectory))
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Không thể tạo package đang chờ xác nhận.");
            try { MoveWithRetry(StagingDirectory, PendingDirectory); }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex)
            {
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Cannot create the pending package.", ex.ToString(), ex);
            }
        }

        public void CommitPending()
        {
            if (string.Equals(StagingDirectory, PendingDirectory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(PendingDirectory, FinalDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(FinalDirectory))
                    throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Final package directory is missing.");
                return;
            }
            if (!Directory.Exists(PendingDirectory) || Directory.Exists(FinalDirectory))
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Không thể công bố package cuối cùng.");
            try { MoveWithRetry(PendingDirectory, FinalDirectory); }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex)
            {
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Cannot publish the final package.", ex.ToString(), ex);
            }
        }

        public void CommitPendingReplacingFinal()
        {
            if (string.Equals(Path.GetFullPath(PendingDirectory), Path.GetFullPath(FinalDirectory), StringComparison.OrdinalIgnoreCase))
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Pending and final package directories must be different.");
            if (!Directory.Exists(PendingDirectory))
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Pending package directory is missing.");
            try
            {
                if (Directory.Exists(FinalDirectory)) Directory.Delete(FinalDirectory, true);
                MoveWithRetry(PendingDirectory, FinalDirectory);
            }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex)
            {
                throw new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Cannot replace the final package.", ex.ToString(), ex);
            }
        }

        private static void MoveWithRetry(string source, string destination)
        {
            IOException last = null;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (IOException ex)
                {
                    last = ex;
                    if (attempt == 11) throw;
                    Thread.Sleep(250);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt == 11) throw;
                    Thread.Sleep(250);
                }
            }
            if (last != null) throw last;
        }

        public void RollbackPending()
        {
            if (Directory.Exists(PendingDirectory)) Directory.Delete(PendingDirectory, true);
        }

        public void RollbackFinal()
        {
            if (Directory.Exists(FinalDirectory)) Directory.Delete(FinalDirectory, true);
        }
    }

    public sealed class PdmTransactionCleanupResult
    {
        public bool StagedSourceClosed { get; set; }
        public bool ExportedStagingClosed { get; set; }
        public bool PendingDocumentClosed { get; set; }
        public bool FinalFailureDocumentClosed { get; set; }
        public bool StagingDirectoryRemoved { get; set; }
        public bool PendingDirectoryRemoved { get; set; }
        public bool FailedFinalDirectoryRemoved { get; set; }
        public System.Collections.Generic.IList<string> Issues { get; } = new System.Collections.Generic.List<string>();
        public bool IsSuccessful { get { return Issues.Count == 0; } }
    }

    public sealed class PdmTransactionCleanup<TDocument> where TDocument : class
    {
        private readonly string _stagingDirectory;
        private readonly string _pendingDirectory;
        private readonly string _finalDirectory;
        private readonly bool _operationFailed;
        private PdmTransactionCleanupResult _result;
        private TDocument _stagedSourceDocument;
        private TDocument _exportedStagingDocument;
        private TDocument _pendingPackageDocument;
        private TDocument _finalPackageDocument;

        public PdmTransactionCleanup(TDocument stagedSourceDocument, TDocument exportedStagingDocument,
            TDocument pendingPackageDocument, TDocument finalPackageDocument, string stagingDirectory,
            string pendingDirectory, string finalDirectory, bool operationFailed)
        {
            _stagedSourceDocument = stagedSourceDocument;
            _exportedStagingDocument = exportedStagingDocument;
            _pendingPackageDocument = pendingPackageDocument;
            _finalPackageDocument = finalPackageDocument;
            _stagingDirectory = stagingDirectory;
            _pendingDirectory = pendingDirectory;
            _finalDirectory = finalDirectory;
            _operationFailed = operationFailed;
        }

        public TDocument StagedSourceDocument { get { return _stagedSourceDocument; } }
        public TDocument ExportedStagingDocument { get { return _exportedStagingDocument; } }
        public TDocument PendingPackageDocument { get { return _pendingPackageDocument; } }
        public TDocument FinalPackageDocument { get { return _finalPackageDocument; } }

        public PdmTransactionCleanupResult Execute(Action<TDocument> closeDocument, Action<string> deleteDirectory)
        {
            if (_result != null) return _result;
            if (closeDocument == null) throw new ArgumentNullException(nameof(closeDocument));
            if (deleteDirectory == null) throw new ArgumentNullException(nameof(deleteDirectory));

            var result = new PdmTransactionCleanupResult();
            if (_operationFailed)
                Close(ref _finalPackageDocument, closeDocument, "DOCUMENT_CLOSE_FAILED:final", result.Issues);
            Close(ref _pendingPackageDocument, closeDocument, "DOCUMENT_CLOSE_FAILED:pending", result.Issues);
            Close(ref _exportedStagingDocument, closeDocument, "DOCUMENT_CLOSE_FAILED:exported-staging", result.Issues);
            Close(ref _stagedSourceDocument, closeDocument, "DOCUMENT_CLOSE_FAILED:staged-source", result.Issues);

            result.FinalFailureDocumentClosed = !_operationFailed || FinalPackageDocument == null;
            result.PendingDocumentClosed = PendingPackageDocument == null;
            result.ExportedStagingClosed = ExportedStagingDocument == null;
            result.StagedSourceClosed = StagedSourceDocument == null;

            if (result.StagedSourceClosed && result.ExportedStagingClosed)
                result.StagingDirectoryRemoved = Delete(_stagingDirectory, deleteDirectory, "STAGING_CLEANUP_FAILED", result.Issues);
            if (_operationFailed && result.PendingDocumentClosed)
                result.PendingDirectoryRemoved = Delete(_pendingDirectory, deleteDirectory, "PENDING_PACKAGE_ROLLBACK_FAILED", result.Issues);
            else result.PendingDirectoryRemoved = true;
            if (_operationFailed && result.FinalFailureDocumentClosed)
                result.FailedFinalDirectoryRemoved = Delete(_finalDirectory, deleteDirectory, "FINAL_PACKAGE_ROLLBACK_FAILED", result.Issues);
            else result.FailedFinalDirectoryRemoved = true;

            _result = result;
            return result;
        }

        private static void Close(ref TDocument document, Action<TDocument> closeDocument,
            string issueCode, ICollection<string> issues)
        {
            if (document == null) return;
            try
            {
                closeDocument(document);
                document = null;
            }
            catch (Exception ex) { issues.Add(issueCode + ":" + ex); }
        }

        private static bool Delete(string directory, Action<string> deleteDirectory,
            string issueCode, ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(directory)) return true;
            try
            {
                deleteDirectory(directory);
                return true;
            }
            catch (Exception ex)
            {
                issues.Add(issueCode + ":" + ex);
                return false;
            }
        }
    }

    public static class PdmNormalizeExportErrorFormatter
    {
        public static string Format(Exception exception)
        {
            var structured = exception as PdmNormalizeExportException;
            if (structured == null)
                return "Mã lỗi: UNEXPECTED_NORMALIZE_EXPORT_FAILURE\nKhông thể chuẩn hóa và xuất PDM an toàn.";
            return "Mã lỗi: " + structured.Code + "\n" + structured.UserMessage;
        }
    }
}
