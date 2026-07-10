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

        [Fact]
        public void PdmPushPreviewBuilder_MapsAnalyzedDocumentPhysicalFileFields()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "builder mapping");
                var analyzeResult = CreateAnalyzeResult(new AnalyzedDocumentFile
                {
                    SourcePath = tempFile,
                    LogicalCode = "01-01",
                    DocumentRole = "PackageDetail",
                    LinkTargetType = "Part",
                    Fingerprint = "fingerprint-123",
                    LinkedPartLogicalCode = "01-01"
                });

                var preview = new PdmPushPreviewBuilder().Build(analyzeResult, "main", "test");
                var document = Assert.Single(preview.Documents);

                Assert.Equal(tempFile, document.SourceFilePath);
                Assert.Equal("fingerprint-123", document.FileHash);
                Assert.Equal(new FileInfo(tempFile).Length, document.FileSize);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void PdmPushPreviewBuilder_MapsMissingDocumentFileSizeAsZero()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
            var analyzeResult = CreateAnalyzeResult(new AnalyzedDocumentFile
            {
                SourcePath = missingPath,
                LogicalCode = "01-02",
                DocumentRole = "PackageDetail",
                LinkTargetType = "Part",
                Fingerprint = "fingerprint-missing",
                LinkedPartLogicalCode = "01-02"
            });

            var preview = new PdmPushPreviewBuilder().Build(analyzeResult, "main", "test");
            var document = Assert.Single(preview.Documents);

            Assert.Equal(missingPath, document.SourceFilePath);
            Assert.Equal("fingerprint-missing", document.FileHash);
            Assert.Equal(0L, document.FileSize);
        }

        private static AnalyzeResult CreateAnalyzeResult(AnalyzedDocumentFile document)
        {
            return new AnalyzeResult
            {
                RepositoryCode = "IRONCASE",
                ProjectName = "IRONCASE",
                StructureNodes = Array.Empty<AnalyzedStructureNode>(),
                CadFiles = Array.Empty<AnalyzedCadFile>(),
                DocumentFiles = new[] { document },
                IgnoredFiles = Array.Empty<AnalyzedIgnoredFile>(),
                Warnings = Array.Empty<AnalyzeWarning>(),
                Summary = new AnalyzeSummary
                {
                    DocumentFileCount = 1,
                    IsValid = true
                }
            };
        }
    }
}
