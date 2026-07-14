using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using interop.ICApiIronCAD;
using IdeaCadConnector.Ui.Views;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadNormalizeExportCommand
    {
        private readonly IronCadAddin _addin;
        private readonly IronCadSceneNormalizationReader _reader = new IronCadSceneNormalizationReader();
        private readonly IronCadSceneNormalizationWriter _writer = new IronCadSceneNormalizationWriter();
        private readonly IronCadDependencyDiscovery _dependencyDiscovery = new IronCadDependencyDiscovery();
        private readonly IronCadDocumentActivationVerifier _activationVerifier = new IronCadDocumentActivationVerifier();
        public bool IsRunning { get; private set; }

        public IronCadNormalizeExportCommand(IronCadAddin addin) { _addin = addin ?? throw new ArgumentNullException(nameof(addin)); }

        public void Execute()
        {
            if (IsRunning) return;
            IsRunning = true;
            IZBaseApp app = null;
            IZDoc stagedSourceDoc = null;
            IZDoc exportedStagingDoc = null;
            IZDoc pendingPackageDoc = null;
            IZDoc finalPackageDoc = null;
            string stagingDirectory = null;
            string sourceStagingDirectory = null;
            string pendingDirectory = null;
            string finalDirectory = null;
            string successMessage = null;
            Exception failure = null;
            bool finalPackagePublished = false;
            var cleanupFailures = new List<Exception>();
            PdmPackagePublicationTransaction publication = null;
            try
            {
                app = _addin.IronCADApp;
                if (app == null) throw Fail("ACTIVE_DOCUMENT_UNAVAILABLE", "Không tìm thấy phiên IronCAD đang hoạt động.");
                var originalSourceDocument = app.ActiveDoc;
                var originalSourceDoc = originalSourceDocument as IZDoc;
                var sourceScene = originalSourceDocument as IZSceneDoc;
                if (originalSourceDocument == null) throw Fail("ACTIVE_DOCUMENT_UNAVAILABLE", "Không có tài liệu IronCAD đang hoạt động.");
                if (originalSourceDoc == null) throw Fail("ACTIVE_DOCUMENT_UNAVAILABLE", "Không thể nhận diện source document an toàn.");
                if (sourceScene == null) throw Fail("ACTIVE_DOCUMENT_NOT_SCENE", "Tài liệu đang mở không phải IronCAD Scene.");
                var activePath = originalSourceDocument.Name;
                if (string.IsNullOrWhiteSpace(activePath) || !Path.IsPathRooted(activePath) ||
                    !string.Equals(Path.GetExtension(activePath), ".ics", StringComparison.OrdinalIgnoreCase) || originalSourceDocument.Modified)
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
                pendingDirectory = publicationPaths.PendingDirectory;
                var outputIssues = new PdmOutputSafetyValidator().Validate(dialog.Result.OutputFolder, activePath, pendingDirectory);
                if (outputIssues.Count != 0) throw Fail("PREFLIGHT_VALIDATION_FAILED", "Thư mục xuất không đạt kiểm tra an toàn.", string.Join(",", outputIssues));
                if (Directory.Exists(pendingDirectory)) throw Fail("PACKAGE_COMMIT_FAILED", "Thư mục package đang chờ đã tồn tại.");

                stagingDirectory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "PDM-staging", Guid.NewGuid().ToString("N"));
                sourceStagingDirectory = stagingDirectory + "-source";
                var packageStaging = pendingDirectory;
                Directory.CreateDirectory(sourceStagingDirectory);
                var stagedSourcePath = Path.Combine(sourceStagingDirectory, Path.GetFileName(activePath));
                File.Copy(activePath, stagedSourcePath, false);
                stagedSourceDoc = app.OpenFile(stagedSourcePath, false);
                EnsureTemporaryDocument(stagedSourceDoc, originalSourceDoc, "STAGING_DOCUMENT_OPEN_FAILED");
                var stagedScene = _activationVerifier.VerifyScene(app, stagedSourcePath, "STAGING_DOCUMENT");

                var stagedSnapshot = _reader.Read(stagedScene);
                if (stagedSnapshot.Root.Properties == null) stagedSnapshot.Root.Properties = new PdmSourceProperties();
                if (string.IsNullOrWhiteSpace(stagedSnapshot.Root.Properties.NodeId)) stagedSnapshot.Root.Properties.NodeId = finalPlan.Root.NodeId;
                var stagedPlan = new PdmNormalizationPlanner().CreateFinalPlan(stagedSnapshot.Root, dialog.Result);
                if (PdmRoundTripPlanComparer.Compare(finalPlan, stagedPlan).Count != 0)
                    throw Fail("STAGED_TREE_MISMATCH", "Staged Scene Tree không khớp preview đã phê duyệt.");
                preflight = new PdmNormalizationPreflightValidator().Validate(stagedPlan, dialog.Result.OutputFolder);
                if (preflight.Count != 0) throw Fail("PREFLIGHT_VALIDATION_FAILED", "Staged plan không đạt kiểm tra an toàn.", string.Join(",", preflight));

                _writer.Apply(stagedSnapshot, stagedPlan);
                var stagedRootFile = _writer.Export(stagedScene, stagedSnapshot, stagedPlan, packageStaging);
                var manifest = PdmManifestV2Factory.Create(stagedPlan);
                File.WriteAllText(Path.Combine(packageStaging, "pdm-bom-manifest.json"), new PdmPackageManifestWriter().Serialize(manifest));
                EnsurePackageValid(packageStaging, manifest, "PACKAGE_VALIDATION_FAILED");

                CloseDocumentOrThrow(app, ref stagedSourceDoc);
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f)))
                    throw Fail("SOURCE_FILE_CHANGED", "File nguồn đã thay đổi trong quá trình xuất.");

                publication = new PdmPackagePublicationTransaction(packageStaging, pendingDirectory, finalDirectory);
                publication.MoveToPending();
                var pendingRootPath = Path.Combine(pendingDirectory, "cad", Path.GetFileName(stagedRootFile));
                pendingPackageDoc = app.OpenFile(pendingRootPath, false);
                EnsureTemporaryDocument(pendingPackageDoc, originalSourceDoc, "PENDING_PACKAGE_VALIDATION_FAILED");
                var pendingScene = _activationVerifier.VerifyScene(app, pendingRootPath, "PENDING_ROOT");
                EnsurePackageValid(pendingDirectory, manifest, "PENDING_PACKAGE_VALIDATION_FAILED");
                new IronCadExportPackageVerifier(_reader).Verify(pendingScene, stagedPlan, pendingDirectory,
                    Path.GetDirectoryName(activePath), sourceStagingDirectory);
                CloseDocumentOrThrow(app, ref pendingPackageDoc);

                publication.CommitPendingReplacingFinal();
                finalPackagePublished = true;
                var finalRootPath = Path.Combine(finalDirectory, "cad", Path.GetFileName(stagedRootFile));
                finalPackageDoc = app.OpenFile(finalRootPath, false);
                EnsureTemporaryDocument(finalPackageDoc, originalSourceDoc, "FINAL_ROOT_OPEN_FAILED");
                var finalScene = _activationVerifier.VerifyScene(app, finalRootPath, "FINAL_ROOT");
                EnsurePackageValid(finalDirectory, manifest, "FINAL_PACKAGE_VALIDATION_FAILED");
                new IronCadExportPackageVerifier(_reader).Verify(finalScene, stagedPlan, finalDirectory,
                    Path.GetDirectoryName(activePath), sourceStagingDirectory);
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f)))
                    throw Fail("SOURCE_FILE_CHANGED", "The source file changed during final package validation.");
                successMessage = "Chuẩn hóa và xuất PDM thành công.\n\nPackage: " + finalDirectory + "\nSource files verified unchanged.";
            }
            catch (Exception ex) { failure = ex; Trace.WriteLine(ex); WriteRuntimeFailureLog(ex); }
            finally
            {
                if (failure != null) CloseDocumentBestEffort(app, ref finalPackageDoc, cleanupFailures);
                CloseDocumentBestEffort(app, ref pendingPackageDoc, cleanupFailures);
                CloseDocumentBestEffort(app, ref exportedStagingDoc, cleanupFailures);
                CloseDocumentBestEffort(app, ref stagedSourceDoc, cleanupFailures);

                if (publication != null && failure != null && pendingPackageDoc == null)
                    TryCleanupDirectory(publication.PendingDirectory, "PENDING_PACKAGE_ROLLBACK_FAILED", cleanupFailures);
                if (publication != null && failure != null && finalPackagePublished && finalPackageDoc == null)
                    TryCleanupDirectory(publication.FinalDirectory, "FINAL_PACKAGE_ROLLBACK_FAILED", cleanupFailures);
                if (stagedSourceDoc == null && exportedStagingDoc == null && pendingPackageDoc == null)
                {
                    TryCleanupDirectory(stagingDirectory, "STAGING_CLEANUP_FAILED", cleanupFailures);
                    TryCleanupDirectory(sourceStagingDirectory, "SOURCE_STAGING_CLEANUP_FAILED", cleanupFailures);
                }
                else cleanupFailures.Add(Fail("DOCUMENT_CLOSE_FAILED", "Không thể đóng hết temporary document trước cleanup."));

                IsRunning = false;
                if (cleanupFailures.Count != 0)
                {
                    if (finalPackagePublished)
                    {
                        CloseDocumentBestEffort(app, ref finalPackageDoc, cleanupFailures);
                        if (finalPackageDoc == null)
                            TryCleanupDirectory(finalDirectory, "FINAL_PACKAGE_ROLLBACK_FAILED", cleanupFailures);
                    }
                    foreach (var cleanupError in cleanupFailures) Trace.WriteLine(cleanupError);
                    failure = Fail("STAGING_CLEANUP_FAILED", "Không thể hoàn tất cleanup transaction.",
                        string.Join(Environment.NewLine, cleanupFailures.Select(e => e.ToString())));
                    successMessage = null;
                }
                if (failure != null) MessageBox.Show(PdmNormalizeExportErrorFormatter.Format(failure), "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Error);
                else if (!string.IsNullOrWhiteSpace(successMessage)) MessageBox.Show(successMessage, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private static void EnsurePackageValid(string directory, PdmPackageManifest manifest, string code)
        {
            var validation = new PdmPackageValidator().Validate(directory, manifest);
            if (!validation.IsValid) throw Fail(code, "Package không đạt kiểm tra an toàn.", string.Join(",", validation.Issues));
        }

        private static PdmNormalizeExportException Fail(string code, string userMessage, string details = null, Exception inner = null)
        {
            return new PdmNormalizeExportException(code, userMessage, details, inner);
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
                // Diagnostics must never replace the user-facing failure.
            }
        }

    }
}
