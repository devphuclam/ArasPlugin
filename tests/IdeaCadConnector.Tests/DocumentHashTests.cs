using System;
using System.Collections.Generic;
using System.IO;
using IdeaCadConnector.Workspace;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class DocumentHashTests
    {
        [Fact]
        public void DocumentFileIdentityService_ComputesSha256_KnownVector()
        {
            // SHA256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "abc");
                var hash = DocumentFileIdentityService.ComputeSha256(tempFile);
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void DocumentFileIdentityService_ReturnsNull_ForMissingFile()
        {
            var hash = DocumentFileIdentityService.ComputeSha256(@"C:\nonexistent\file.pdf");
            Assert.Null(hash);
        }

        [Fact]
        public void DocumentFileIdentityService_ReturnsNull_ForNullPath()
        {
            Assert.Null(DocumentFileIdentityService.ComputeSha256(null));
            Assert.Null(DocumentFileIdentityService.ComputeSha256(""));
            Assert.Null(DocumentFileIdentityService.ComputeSha256("   "));
        }

        [Fact]
        public void DocumentFileIdentityService_ReturnsUnavailable_WhenStreamCannotBeOpened()
        {
            var identity = DocumentFileIdentityService.ResolveAndRead(
                Path.GetTempPath(),
                "blocked.pdf",
                "blocked.pdf",
                _ => throw new UnauthorizedAccessException());

            Assert.False(identity.IsAvailable);
            Assert.Null(identity.FileHash);
            Assert.Equal(0L, identity.FileSize);
            Assert.Contains("not readable", identity.FailureReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DocumentFingerprint_IsPopulated_WhenFileExists()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Create a PDF file
                var pdfFile = Path.Combine(tempFolder, "doc.pdf");
                File.WriteAllText(pdfFile, "test content for fingerprint");

                // Analyze the folder
                var analyzer = new Aras01FolderAnalyzer(new PdmNamingPolicy());
                var folderAnalysis = analyzer.Analyze(tempFolder);

                // Convert to AnalyzeResult
                var result = PushPreviewMapper.ToAnalyzeResult(folderAnalysis, null);

                Assert.NotNull(result);
                Assert.Single(result.DocumentFiles);
                Assert.NotNull(result.DocumentFiles[0].Fingerprint);
                Assert.Equal(64, result.DocumentFiles[0].Fingerprint.Length);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }

        [Fact]
        public void DocumentFingerprint_IsNull_WhenFileMissingAtScanTime()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Create a PDF file
                var pdfFile = Path.Combine(tempFolder, "doc.pdf");
                File.WriteAllText(pdfFile, "test content");

                // Delete the file BEFORE scanning
                File.Delete(pdfFile);

                // Analyze the folder - no PDF files should be detected
                var analyzer = new Aras01FolderAnalyzer(new PdmNamingPolicy());
                var folderAnalysis = analyzer.Analyze(tempFolder);

                // No document files should be detected
                Assert.Empty(folderAnalysis.DocumentFiles);

                // Convert to AnalyzeResult
                var result = PushPreviewMapper.ToAnalyzeResult(folderAnalysis, null);

                // No document files should be in the result
                Assert.Empty(result.DocumentFiles);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }

        [Fact]
        public void DocumentPreviewRow_HasCorrectHashAndSize()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Create a PDF file
                var pdfFile = Path.Combine(tempFolder, "doc.pdf");
                File.WriteAllText(pdfFile, "builder test content");

                // Analyze the folder
                var analyzer = new Aras01FolderAnalyzer(new PdmNamingPolicy());
                var folderAnalysis = analyzer.Analyze(tempFolder);

                // Convert to AnalyzeResult
                var analyzeResult = PushPreviewMapper.ToAnalyzeResult(folderAnalysis, null);

                // Build preview
                var preview = new PdmPushPreviewBuilder().Build(analyzeResult, "main", "test");

                Assert.Single(preview.Documents);
                var doc = preview.Documents[0];
                Assert.Equal("doc.pdf", doc.RelativePath);
                Assert.True(Path.IsPathRooted(doc.SourceFilePath));
                Assert.Equal(DocumentFileIdentityService.ComputeSha256(pdfFile), doc.FileHash);
                Assert.Equal(new FileInfo(pdfFile).Length, doc.FileSize);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }

        [Fact]
        public void BusinessStructureDocument_UsesResolvedPhysicalIdentity()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                var pdfFile = Path.Combine(tempFolder, "doc.pdf");
                File.WriteAllText(pdfFile, "business document");
                var business = new PdmBusinessStructureAnalysis { FolderPath = tempFolder };
                business.RootNodes.Add(new PdmBusinessNode
                {
                    Code = "01-01",
                    Name = "Document",
                    DisplayName = "Document",
                    NodeType = "Component",
                    SourceFileName = "doc.pdf"
                });

                var result = PushPreviewMapper.ToAnalyzeResult(
                    new PdmFolderAnalysis { FolderPath = tempFolder },
                    business);
                var preview = new PdmPushPreviewBuilder().Build(result, "main", "test");
                var document = Assert.Single(preview.Documents);

                Assert.Equal("doc.pdf", document.RelativePath);
                Assert.Equal(Path.GetFullPath(pdfFile), document.SourceFilePath);
                Assert.Equal(DocumentFileIdentityService.ComputeSha256(pdfFile), document.FileHash);
                Assert.Equal(new FileInfo(pdfFile).Length, document.FileSize);
                Assert.True(preview.Readiness.CanPush);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        [Fact]
        public void MissingDocumentFile_ProducesBlockingWarning()
        {
            // Test that missing document files produce blocking warnings
            // This tests the validation added in MapWarnings
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Create a PDF file
                var pdfFile = Path.Combine(tempFolder, "doc.pdf");
                File.WriteAllText(pdfFile, "test content");

                // Analyze the folder
                var analyzer = new Aras01FolderAnalyzer(new PdmNamingPolicy());
                var folderAnalysis = analyzer.Analyze(tempFolder);

                // Manually remove the file from DocumentFiles to simulate a race condition
                // where the file was detected but is now missing
                folderAnalysis.DocumentFiles.Clear();
                folderAnalysis.DocumentFiles.Add(new PdmParsedFile
                {
                    FullPath = pdfFile,
                    RelativePath = "doc.pdf",
                    FileName = "doc.pdf",
                    ProjectCode = "TEST"
                });

                // Now delete the file
                File.Delete(pdfFile);

                // Convert to AnalyzeResult - this should trigger the validation
                var result = PushPreviewMapper.ToAnalyzeResult(folderAnalysis, null);

                // Check that there's a blocking warning for the missing file
                var missingWarning = result.Warnings.FirstOrDefault(w =>
                    w.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.NotNull(missingWarning);
                Assert.True(missingWarning.BlocksPush);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                }
            }
        }

    }
}
