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
            Assert.Null(request.RelativePath);
            Assert.Null(request.FileHash);
            Assert.Equal(0L, request.FileSize);
        }

        [Fact]
        public void DocumentPreviewRow_NewFields_HaveDefaultValues()
        {
            var row = new DocumentPreviewRow();

            Assert.Null(row.SourceFilePath);
            Assert.Null(row.RelativePath);
            Assert.Null(row.FileHash);
            Assert.Equal(0L, row.FileSize);
        }

        [Fact]
        public void DocumentPreviewRow_To_PdmDocumentRequest_MapsNewFields()
        {
            var row = new DocumentPreviewRow
            {
                SourceFileName = "spec.pdf",
                RelativePath = "docs/spec.pdf",
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
                RelativePath = row.RelativePath,
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
            Assert.Equal(row.RelativePath, request.RelativePath);
            Assert.Equal(row.FileHash, request.FileHash);
            Assert.Equal(row.FileSize, request.FileSize);
        }

        [Fact]
        public void PdmPushPreviewBuilder_MapsAnalyzedDocumentPhysicalFileFields()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "builder mapping");
                var analysis = new PdmFolderAnalysis { FolderPath = Path.GetDirectoryName(tempFile) };
                analysis.DetailFiles.Add(new PdmParsedFile
                {
                    FullPath = Path.Combine(Path.GetDirectoryName(tempFile), "IRONCASE_Ver1.0_001.ics"),
                    RelativePath = "IRONCASE_Ver1.0_001.ics",
                    FileName = "IRONCASE_Ver1.0_001.ics",
                    LogicalPartCode = "IRONCASE-01"
                });
                analysis.DocumentFiles.Add(new PdmParsedFile
                {
                    FullPath = tempFile,
                    RelativePath = Path.GetFileName(tempFile),
                    FileName = Path.GetFileName(tempFile),
                    LogicalPartCode = "01-01"
                });
                var analyzeResult = PushPreviewMapper.ToAnalyzeResult(analysis, null);

                var preview = new PdmPushPreviewBuilder().Build(analyzeResult, "main", "test");
                var document = Assert.Single(preview.Documents);

                Assert.Equal(tempFile, document.SourceFilePath);
                Assert.Equal(DocumentFileIdentityService.ComputeSha256(tempFile), document.FileHash);
                Assert.Equal(new FileInfo(tempFile).Length, document.FileSize);
                Assert.True(preview.Readiness.CanPush);
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
            var analysis = new PdmFolderAnalysis { FolderPath = Path.GetDirectoryName(missingPath) };
            analysis.DocumentFiles.Add(new PdmParsedFile
            {
                FullPath = missingPath,
                RelativePath = Path.GetFileName(missingPath),
                FileName = Path.GetFileName(missingPath),
                LogicalPartCode = "01-02"
            });
            var analyzeResult = PushPreviewMapper.ToAnalyzeResult(analysis, null);

            var preview = new PdmPushPreviewBuilder().Build(analyzeResult, "main", "test");
            var document = Assert.Single(preview.Documents);

            Assert.Equal(missingPath, document.SourceFilePath);
            Assert.Null(document.FileHash);
            Assert.Equal(0L, document.FileSize);
            Assert.Contains(preview.Warnings, warning => warning.BlocksPush);
            Assert.False(preview.Readiness.CanPush);
        }
    }
}
