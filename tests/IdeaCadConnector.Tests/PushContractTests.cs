using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Workspace;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PushContractTests
    {
        [Fact]
        public void PdmDocumentRequest_NewFields_HaveDefaultValues()
        {
            var request = new PdmDocumentRequest();

            Assert.Null(request.SourceFilePath);
            Assert.Null(request.FileHash);
            Assert.Equal(0L, request.FileSize);
        }

        [Fact]
        public void DocumentPreviewRow_NewFields_HaveDefaultValues()
        {
            var row = new DocumentPreviewRow();

            Assert.Null(row.SourceFilePath);
            Assert.Null(row.FileHash);
            Assert.Equal(0L, row.FileSize);
        }

        [Fact]
        public void DocumentPreviewRow_To_PdmDocumentRequest_MapsNewFields()
        {
            var row = new DocumentPreviewRow
            {
                SourceFileName = "spec.pdf",
                SourceFilePath = @"C:\repo\docs\spec.pdf",
                FileHash = "abc123",
                FileSize = 4096L,
                LogicalCode = "DOC-1",
                DocumentNumber = "DOC-0001",
                Classification = "Technical Drawing",
                LinkTargetType = "Part",
                LinkedPartLogicalCode = "PART-1"
            };

            var request = new PdmDocumentRequest
            {
                SourceFileName = row.SourceFileName,
                SourceFilePath = row.SourceFilePath,
                FileHash = row.FileHash,
                FileSize = row.FileSize,
                LogicalCode = row.LogicalCode,
                DocumentNumber = row.DocumentNumber,
                Classification = row.Classification,
                LinkTargetType = row.LinkTargetType,
                LinkedPartLogicalCode = row.LinkedPartLogicalCode
            };

            Assert.Equal(row.SourceFilePath, request.SourceFilePath);
            Assert.Equal(row.FileHash, request.FileHash);
            Assert.Equal(row.FileSize, request.FileSize);
        }

        [Fact]
        public void AnalyzedDocumentFile_To_DocumentPreviewRow_SourcePathMapped()
        {
            var analyzed = new AnalyzedDocumentFile
            {
                SourcePath = @"C:\repo\docs\spec.pdf",
                LogicalCode = "DOC-1",
                Fingerprint = "fp-1"
            };

            var row = new DocumentPreviewRow
            {
                SourceFileName = Path.GetFileName(analyzed.SourcePath),
                SourceFilePath = analyzed.SourcePath,
                FileHash = analyzed.Fingerprint
            };

            Assert.Equal(analyzed.SourcePath, row.SourceFilePath);
            Assert.Equal("spec.pdf", row.SourceFileName);
        }

        [Fact]
        public void AnalyzedDocumentFile_To_DocumentPreviewRow_FileHashFromFingerprint()
        {
            var analyzed = new AnalyzedDocumentFile
            {
                SourcePath = @"C:\repo\docs\spec.pdf",
                Fingerprint = "fp-abc-123"
            };

            var row = new DocumentPreviewRow
            {
                FileHash = analyzed.Fingerprint
            };

            Assert.Equal("fp-abc-123", row.FileHash);
        }

        [Fact]
        public void AnalyzedDocumentFile_To_DocumentPreviewRow_FileSizeFromFile()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "hello world");
                var analyzed = new AnalyzedDocumentFile
                {
                    SourcePath = tempFile,
                    Fingerprint = "fp-1"
                };

                var fileSize = File.Exists(analyzed.SourcePath)
                    ? new FileInfo(analyzed.SourcePath).Length
                    : 0L;

                var row = new DocumentPreviewRow
                {
                    SourceFilePath = analyzed.SourcePath,
                    FileHash = analyzed.Fingerprint,
                    FileSize = fileSize
                };

                Assert.Equal(new FileInfo(tempFile).Length, row.FileSize);
                Assert.True(row.FileSize > 0);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}