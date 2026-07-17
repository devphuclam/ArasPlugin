using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using interop.ICApiIronCAD;
using IdeaCadConnector.Ui.Views;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadNormalizeExportCommand
    {
        private readonly IZBaseApp _app;
        private readonly IIronCadSceneDocumentService _documentService;
        private readonly IronCadSceneNormalizationReader _reader = new IronCadSceneNormalizationReader();
        private readonly IronCadSceneNormalizationWriter _writer = new IronCadSceneNormalizationWriter();
        private readonly IronCadDependencyDiscovery _dependencyDiscovery = new IronCadDependencyDiscovery();
        public bool IsRunning { get; private set; }

        public IronCadNormalizeExportCommand(IZBaseApp app, IIronCadSceneDocumentService documentService)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        }

        public void Execute()
        {
            if (IsRunning) return;
            IsRunning = true;
            IZDoc stagedSourceDoc = null;
            string stagingDirectory = null;
            string sourceStagingDirectory = null;
            string finalDirectory = null;
            string successMessage = null;
            Exception failure = null;
            bool finalPackageStarted = false;
            bool finalPackagePublished = false;
            bool temporaryDocumentOpened = false;
            var cleanupFailures = new List<Exception>();
            try
            {
                if (_app == null) throw Fail("ACTIVE_DOCUMENT_UNAVAILABLE", "Không tìm thấy phiên IronCAD đang hoạt động.");
                var originalSourceDocument = GetActiveOrSingleOpenDocument(_app);
                var originalSourceDoc = originalSourceDocument as IZDoc;
                var sourceScene = originalSourceDocument as IZSceneDoc;
                if (originalSourceDocument == null) throw Fail("ACTIVE_DOCUMENT_UNAVAILABLE", "Không có tài liệu IronCAD đang hoạt động.");
                if (sourceScene == null) throw Fail("ACTIVE_DOCUMENT_NOT_SCENE", "Tài liệu đang mở không phải IronCAD Scene.");
                var activePath = originalSourceDoc != null ? originalSourceDoc.Name : GetSceneDocumentName(sourceScene);
                if (string.IsNullOrWhiteSpace(activePath) || !Path.IsPathRooted(activePath) ||
                    !string.Equals(Path.GetExtension(activePath), ".ics", StringComparison.OrdinalIgnoreCase) || IsSceneDocumentModified(originalSourceDoc, sourceScene))
                    throw Fail("ACTIVE_DOCUMENT_NOT_SAVED", "Hãy lưu Scene .ics và bảo đảm tài liệu không có thay đổi chưa lưu.");

                var dependencies = _dependencyDiscovery.Discover(sourceScene, Path.GetDirectoryName(activePath));
                if (!dependencies.DiscoveryComplete || dependencies.ExternalDependencyCount != 0)
                    throw Fail("BLOCKED_SOURCE_DEPENDENCY_ISOLATION", "Package có external dependency chưa được hỗ trợ an toàn.");
                var sourceFingerprints = new[] { PdmSourceIntegrity.Capture(activePath) }.ToList();
                var snapshot = _reader.Read(sourceScene);
                var initialPlan = new PdmNormalizationPlanner().CreatePlan(
                    PdmNameNormalizer.DeriveProjectCodeFromRootFileName(activePath), "A", snapshot.Root);
                if (initialPlan.Root == null) throw Fail("SCENE_TRAVERSAL_FAILED", "Không thể đọc Scene Tree.");

                var sourceParent = Directory.GetParent(Path.GetDirectoryName(activePath));
                var defaultOutput = sourceParent == null ? string.Empty : Path.Combine(sourceParent.FullName, initialPlan.ProjectCode + "-PDM-Export");
                var dialog = new NormalizeExportDialog(initialPlan, defaultOutput);
                if (dialog.ShowDialog() != true || dialog.Result == null) return;
                var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(snapshot.Root, dialog.Result);
                var preflight = new PdmNormalizationPreflightValidator().Validate(finalPlan, dialog.Result.OutputFolder);
                if (preflight.Count != 0) throw Fail("PREFLIGHT_VALIDATION_FAILED", "Kế hoạch xuất PDM chưa đạt kiểm tra an toàn.", string.Join(",", preflight));

                var publicationPaths = PdmPackagePublicationPaths.Create(
                    dialog.Result.OutputFolder,
                    finalPlan.ProjectCode,
                    Guid.NewGuid().ToString("N"));
                finalDirectory = publicationPaths.FinalDirectory;
                var outputIssues = new PdmOutputSafetyValidator().Validate(dialog.Result.OutputFolder, activePath, finalDirectory);
                if (outputIssues.Count != 0) throw Fail("PREFLIGHT_VALIDATION_FAILED", "Thư mục xuất không đạt kiểm tra an toàn.", string.Join(",", outputIssues));
                if (Directory.Exists(finalDirectory)) Directory.Delete(finalDirectory, true);

                stagingDirectory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "PDM-staging", Guid.NewGuid().ToString("N"));
                sourceStagingDirectory = stagingDirectory + "-source";
                var packageStaging = finalDirectory;
                Directory.CreateDirectory(sourceStagingDirectory);
                var stagedSourcePath = Path.Combine(sourceStagingDirectory, Path.GetFileName(activePath));
                File.Copy(activePath, stagedSourcePath, false);
                var stagedScene = _documentService.OpenDocument(stagedSourcePath);
                temporaryDocumentOpened = true;

                var stagedSnapshot = _reader.Read(stagedScene);
                if (stagedSnapshot.Root.Properties == null) stagedSnapshot.Root.Properties = new PdmSourceProperties();
                if (string.IsNullOrWhiteSpace(stagedSnapshot.Root.Properties.NodeId)) stagedSnapshot.Root.Properties.NodeId = finalPlan.Root.NodeId;
                var stagedPlan = new PdmNormalizationPlanner().CreateFinalPlan(stagedSnapshot.Root, dialog.Result);
                if (PdmRoundTripPlanComparer.Compare(finalPlan, stagedPlan).Count != 0)
                    throw Fail("STAGED_TREE_MISMATCH", "Staged Scene Tree không khớp preview đã phê duyệt.");
                preflight = new PdmNormalizationPreflightValidator().Validate(stagedPlan, dialog.Result.OutputFolder);
                if (preflight.Count != 0) throw Fail("PREFLIGHT_VALIDATION_FAILED", "Staged plan không đạt kiểm tra an toàn.", string.Join(",", preflight));

                stagedScene.DisableDirtyCounter();
                _writer.Apply(stagedSnapshot, stagedPlan);
                finalPackageStarted = true;
                var exportResult = _writer.Export(_app, stagedScene, stagedSnapshot, stagedPlan, packageStaging);
                var manifest = PdmManifestV2Factory.Create(stagedPlan, exportResult.SourceNodeToDefFileMap);
                File.WriteAllText(Path.Combine(packageStaging, "pdm-bom-manifest.json"), new PdmPackageManifestWriter().Serialize(manifest));
                EnsurePackageValid(packageStaging, manifest, "PACKAGE_VALIDATION_FAILED");
                finalPackagePublished = true;

                _documentService.CloseDocument();
                temporaryDocumentOpened = false;
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f)))
                    throw Fail("SOURCE_FILE_CHANGED", "File nguồn đã thay đổi trong quá trình xuất.");

                var finalRootPath = Path.Combine(finalDirectory, "cad", Path.GetFileName(exportResult.RootFilePath));
                var finalScene = _documentService.OpenDocument(finalRootPath);
                temporaryDocumentOpened = true;
                EnsurePackageValid(finalDirectory, manifest, "FINAL_PACKAGE_VALIDATION_FAILED");
                var validationContext = new IronCadExternalReferenceValidationContext
                {
                    DocumentDirectory = Path.GetDirectoryName(finalRootPath),
                    CadRoot = Path.Combine(finalDirectory, "cad"),
                    SourceRoot = sourceStagingDirectory,
                    StagingRoot = stagingDirectory
                };
                var verifier = new IronCadExportPackageVerifier(_reader);
                var validationResult = verifier.VerifyExternalLinks(finalScene, stagedPlan, validationContext);
                if (!validationResult.IsValid)
                {
                    var diagnostics = FormatValidationDiagnostics(validationResult, validationContext);
                    WriteValidationDiagnosticLog(diagnostics);
                    throw Fail("PACKAGE_VALIDATION_FAILED", "Package không đạt kiểm tra liên kết ngoài.",
                        string.Join(",", validationResult.Issues) + Environment.NewLine + diagnostics);
                }
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f)))
                    throw Fail("SOURCE_FILE_CHANGED", "The source file changed during final package validation.");
                successMessage = "Chuẩn hóa và xuất PDM thành công.\n\nPackage: " + finalDirectory + "\nSource files verified unchanged.";
            }
            catch (InvalidOperationException ex) when (IsDependencyFailure(ex.Message))
            {
                failure = Fail(
                    ex.Message,
                    ex.Message == "BLOCKED_SOURCE_DEPENDENCY_ISOLATION"
                        ? "Package có external dependency chưa được hỗ trợ an toàn."
                        : "Không thể phân tích dependency của Scene.",
                    ex.ToString(),
                    ex);
                Trace.WriteLine(failure);
                WriteRuntimeFailureLog(failure);
            }
            catch (Exception ex) { failure = ex; Trace.WriteLine(ex); WriteRuntimeFailureLog(ex); }
            finally
            {
                if (failure != null && finalPackageStarted && !temporaryDocumentOpened)
                    TryCleanupDirectory(finalDirectory, "FINAL_PACKAGE_ROLLBACK_FAILED", cleanupFailures);

                if (temporaryDocumentOpened)
                {
                    try { _documentService.CloseDocument(); }
                    catch (Exception ex) { cleanupFailures.Add(Fail("DOCUMENT_CLOSE_FAILED", "Không thể đóng temporary document.", ex.ToString(), ex)); }
                }



                if (stagedSourceDoc == null)
                {
                    TryCleanupDirectory(stagingDirectory, "STAGING_CLEANUP_FAILED", cleanupFailures);
                    TryCleanupDirectory(sourceStagingDirectory, "SOURCE_STAGING_CLEANUP_FAILED", cleanupFailures);
                }
                else cleanupFailures.Add(Fail("DOCUMENT_CLOSE_FAILED", "Không thể đóng hết temporary document trước cleanup."));

                IsRunning = false;
                if (cleanupFailures.Count != 0)
                {
                    foreach (var cleanupError in cleanupFailures) Trace.WriteLine(cleanupError);
                    var failureDetails = new List<string>();
                    if (failure != null) failureDetails.Add("PRIMARY FAILURE:" + Environment.NewLine + failure);
                    failureDetails.AddRange(cleanupFailures.Select(e => e.ToString()));
                    var cleanupFailure = Fail("STAGING_CLEANUP_FAILED", "Không thể hoàn tất cleanup transaction.",
                        string.Join(Environment.NewLine, failureDetails));
                    WriteRuntimeFailureLog(cleanupFailure);
                    if (failure == null && !finalPackagePublished)
                    {
                        failure = cleanupFailure;
                        successMessage = null;
                    }
                }
                if (failure != null) MessageBox.Show(PdmNormalizeExportErrorFormatter.Format(failure), "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Error);
                else if (!string.IsNullOrWhiteSpace(successMessage)) MessageBox.Show(successMessage, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);

                _documentService.Dispose();
            }
        }

        private static void CloseDocumentOrThrow(IZBaseApp app, ref IZDoc document)
        {
            if (document == null) return;
            try { app.CloseFile(document); document = null; }
            catch (Exception ex) { throw Fail("DOCUMENT_CLOSE_FAILED", "Không thể đóng temporary IronCAD document.", ex.ToString(), ex); }
        }

        private static void EnsureTemporaryDocument(IZDoc openedDocument, IZDoc originalSourceDocument, string errorCode)
        {
            if (openedDocument == null || object.ReferenceEquals(openedDocument, originalSourceDocument))
                throw Fail(errorCode, "IronCAD không activate temporary document vừa mở.");
        }

        private static void CloseDocumentBestEffort(IZBaseApp app, ref IZDoc document, IList<Exception> cleanupFailures)
        {
            if (document == null) return;
            try { app.CloseFile(document); document = null; }
            catch (Exception ex) { cleanupFailures.Add(Fail("DOCUMENT_CLOSE_FAILED", "Không thể đóng temporary IronCAD document.", ex.ToString(), ex)); }
        }

        private static void TryCleanupDirectory(string directory, string errorCode, IList<Exception> cleanupFailures)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            try { Directory.Delete(directory, true); }
            catch (Exception ex) { cleanupFailures.Add(Fail(errorCode, "Không thể xóa thư mục transaction.", ex.ToString(), ex)); }
        }

        private static object GetActiveOrSingleOpenDocument(IZBaseApp app)
        {
            var active = app.ActiveDoc;
            if (active is IZSceneDoc) return active;

            int openCount;
            try { openCount = app.GetOpenDocsCount(); }
            catch { return null; }
            if (openCount < 1) return active;

            object openDocs;
            try { openDocs = app.GetOpenDocs(); }
            catch { return null; }

            var array = openDocs as IZArray;
            if (array != null)
            {
                int count;
                array.Count(out count);
                for (var i = 0; i < count; i++)
                {
                    object value;
                    array.Get(i, out value);
                    if (value is IZSceneDoc) return value;
                }
                return active;
            }

            var documents = openDocs as object[];
            if (documents != null)
            {
                var scene = documents.FirstOrDefault(document => document is IZSceneDoc);
                if (scene != null) return scene;
            }
            return active;
        }

        private static string GetSceneDocumentName(IZSceneDoc scene)
        {
            try { return Convert.ToString(((dynamic)scene).Name); }
            catch { return null; }
        }

        private static bool IsSceneDocumentModified(IZDoc document, IZSceneDoc scene)
        {
            if (document != null) return document.Modified;
            try { return Convert.ToBoolean(((dynamic)scene).Modified); }
            catch { return false; }
        }

        private static void EnsurePackageValid(string directory, PdmPackageManifest manifest, string code)
        {
            var validation = new PdmPackageValidator().Validate(directory, manifest);
            if (!validation.IsValid) throw Fail(code, "Package không đạt kiểm tra an toàn.", string.Join(",", validation.Issues));
        }

        private static PdmNormalizeExportException Fail(string code, string userMessage, string details = null, Exception inner = null)
        {
            return new PdmNormalizeExportException(code, userMessage, details, inner);
        }

        private static bool IsDependencyFailure(string code)
        {
            return string.Equals(code, "BLOCKED_SOURCE_DEPENDENCY_ISOLATION", StringComparison.Ordinal)
                || string.Equals(code, "DEPENDENCY_DISCOVERY_FAILED", StringComparison.Ordinal)
                || string.Equals(code, "DEPENDENCY_TRAVERSAL_LIMIT_EXCEEDED", StringComparison.Ordinal)
                || string.Equals(code, "DEPENDENCY_TRAVERSAL_CYCLE", StringComparison.Ordinal);
        }

        private static void WriteRuntimeFailureLog(Exception exception)
        {
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "pdm-last-failure.txt"),
                    DateTime.UtcNow.ToString("O") + Environment.NewLine + exception);
            }
            catch
            {
            }
        }

        private static string FormatValidationDiagnostics(
            IronCadExternalReferenceValidationResult validation,
            IronCadExternalReferenceValidationContext context)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[PDM-VALIDATION-DIAGNOSTIC]");
            builder.AppendLine("DocumentDirectory=" + context.DocumentDirectory);
            builder.AppendLine("PackageRoot=" + context.PackageRoot);
            builder.AppendLine("CadRoot=" + context.CadRoot);
            builder.AppendLine("SourceRoot=" + context.SourceRoot);
            builder.AppendLine("StagingRoot=" + context.StagingRoot);
            builder.AppendLine("Issues:");
            foreach (var issue in validation.Issues) builder.AppendLine("  " + issue);
            builder.AppendLine("Records:");
            foreach (var record in validation.Records)
            {
                builder.AppendLine(string.Join(" | ", new[]
                {
                    "Occurrence=" + record.OccurrencePath,
                    "Reported=" + record.ReportedLinkPath,
                    "Resolved=" + record.ResolvedTargetPath,
                    "Exists=" + record.Exists,
                    "InsidePackage=" + record.InsidePackage,
                    "PointsToSource=" + record.PointsToSource,
                    "CanonicalMatch=" + record.CanonicalFileNameMatch
                }));
            }
            return builder.ToString();
        }

        private static void WriteValidationDiagnosticLog(string diagnostics)
        {
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "pdm-last-validation.txt"),
                    DateTime.UtcNow.ToString("O") + Environment.NewLine + diagnostics);
            }
            catch
            {
            }
        }

    }
}
