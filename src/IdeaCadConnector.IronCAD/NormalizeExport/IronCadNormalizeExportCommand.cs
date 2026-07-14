using System;
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
            string stagingDirectory = null;
            string successMessage = null;
            Exception failure = null;
            Exception cleanupFailure = null;
            try
            {
                var app = _addin.IronCADApp;
                if (app == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_UNAVAILABLE");
                var sourceDoc = app.ActiveDoc;
                var sourceScene = sourceDoc as IZSceneDoc;
                if (sourceDoc == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_UNAVAILABLE");
                if (sourceScene == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
                var activePath = sourceDoc.Name;
                if (string.IsNullOrWhiteSpace(activePath) || !Path.IsPathRooted(activePath) ||
                    !string.Equals(Path.GetExtension(activePath), ".ics", StringComparison.OrdinalIgnoreCase) || sourceDoc.Modified)
                    throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SAVED");

                var dependencies = _dependencyDiscovery.Discover(sourceScene, Path.GetDirectoryName(activePath));
                if (!dependencies.DiscoveryComplete || dependencies.ExternalDependencyCount != 0)
                    throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                var sourceFingerprints = new[] { PdmSourceIntegrity.Capture(activePath) }.ToList();
                var snapshot = _reader.Read(sourceScene);
                var initialPlan = new PdmNormalizationPlanner().CreatePlan(
                    PdmNameNormalizer.DeriveProjectCodeFromRootFileName(activePath), "A", snapshot.Root);
                if (initialPlan.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");

                var defaultOutput = Path.Combine(Directory.GetParent(Path.GetDirectoryName(activePath)).FullName,
                    initialPlan.ProjectCode + "-PDM-Export");
                var dialog = new NormalizeExportDialog(initialPlan, defaultOutput);
                if (dialog.ShowDialog() != true || dialog.Result == null) return;
                var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(snapshot.Root, dialog.Result);
                var issues = new PdmNormalizationPreflightValidator().Validate(finalPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED");
                var requestedPackage = Path.Combine(dialog.Result.OutputFolder,
                    dialog.Result.ProjectCode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                var outputIssues = new PdmOutputSafetyValidator().Validate(dialog.Result.OutputFolder, activePath, requestedPackage);
                if (outputIssues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED");

                stagingDirectory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "PDM-staging", Guid.NewGuid().ToString("N"));
                var sourceStagingDirectory = Path.Combine(stagingDirectory, "source");
                Directory.CreateDirectory(sourceStagingDirectory);
                var stagedSourcePath = Path.Combine(sourceStagingDirectory, Path.GetFileName(activePath));
                File.Copy(activePath, stagedSourcePath, false);
                app.OpenFile(stagedSourcePath, false);
                var stagedScene = _activationVerifier.VerifyScene(app, stagedSourcePath, "STAGING_DOCUMENT");
                var stagedDoc = app.ActiveDoc as IZDoc;
                if (stagedDoc == null) throw new InvalidOperationException("STAGING_DOCUMENT_OPEN_FAILED");
                var stagedSnapshot = _reader.Read(stagedScene);
                if (stagedSnapshot.Root.Properties == null) stagedSnapshot.Root.Properties = new PdmSourceProperties();
                if (string.IsNullOrWhiteSpace(stagedSnapshot.Root.Properties.NodeId)) stagedSnapshot.Root.Properties.NodeId = finalPlan.Root.NodeId;
                var stagedPlan = new PdmNormalizationPlanner().CreateFinalPlan(stagedSnapshot.Root, dialog.Result);
                var finalByPath = finalPlan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                var stagedByPath = stagedPlan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                if (finalByPath.Count != stagedByPath.Count || finalByPath.Keys.Any(path => !stagedByPath.ContainsKey(path) || stagedByPath[path].SourceKind != finalByPath[path].SourceKind))
                    throw new InvalidOperationException("STAGED_TREE_MISMATCH");
                issues = new PdmNormalizationPreflightValidator().Validate(stagedPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED");

                var packageStaging = Path.Combine(stagingDirectory, "package");
                _writer.Apply(stagedSnapshot, stagedPlan);
                var stagedRootFile = _writer.Export(stagedScene, stagedSnapshot, stagedPlan, packageStaging);
                var manifest = CreateManifest(stagedPlan);
                File.WriteAllText(Path.Combine(packageStaging, "pdm-bom-manifest.json"), new PdmPackageManifestWriter().Serialize(manifest));
                var validation = new PdmPackageValidator().Validate(packageStaging, manifest);
                if (!validation.IsValid) throw new InvalidOperationException("PACKAGE_VALIDATION_FAILED");

                _activationVerifier.Close(app, stagedDoc, "DOCUMENT_CLOSE_FAILED");
                app.OpenFile(stagedRootFile, false);
                var exportedScene = _activationVerifier.VerifyScene(app, stagedRootFile, "EXPORTED_ROOT");
                new IronCadExportPackageVerifier(_reader).Verify(exportedScene, stagedPlan, packageStaging, Path.GetDirectoryName(activePath), sourceStagingDirectory);
                var exportedDoc = app.ActiveDoc as IZDoc;
                if (exportedDoc == null) throw new InvalidOperationException("EXPORTED_ROOT_OPEN_FAILED");
                _activationVerifier.Close(app, exportedDoc, "DOCUMENT_CLOSE_FAILED");
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f))) throw new InvalidOperationException("SOURCE_FILE_CHANGED");

                var packageDirectory = requestedPackage;
                if (Directory.Exists(packageDirectory)) throw new InvalidOperationException("PACKAGE_COMMIT_FAILED");
                try { Directory.Move(packageStaging, packageDirectory); }
                catch (Exception ex) { throw new InvalidOperationException("PACKAGE_COMMIT_FAILED", ex); }
                var finalRootPath = Path.Combine(packageDirectory, "cad", Path.GetFileName(stagedRootFile));
                app.OpenFile(finalRootPath, false);
                var finalScene = _activationVerifier.VerifyScene(app, finalRootPath, "FINAL_ROOT");
                new IronCadExportPackageVerifier(_reader).Verify(finalScene, stagedPlan, packageDirectory, Path.GetDirectoryName(activePath), stagingDirectory);
                successMessage = "Chuẩn hóa và xuất PDM thành công.\n\nPackage: " + packageDirectory + "\nSource files verified unchanged.";
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (!string.IsNullOrWhiteSpace(stagingDirectory) && Directory.Exists(stagingDirectory))
                    try { Directory.Delete(stagingDirectory, true); } catch (Exception ex) { cleanupFailure = ex; }
                IsRunning = false;
                if (cleanupFailure != null) MessageBox.Show("Không thể dọn staging an toàn.\nMã lỗi: STAGING_CLEANUP_FAILED", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
                else if (failure != null) MessageBox.Show(ToStableError(failure), "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Error);
                else if (!string.IsNullOrWhiteSpace(successMessage)) MessageBox.Show(successMessage, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static PdmPackageManifest CreateManifest(PdmNormalizationPlan plan)
        {
            var root = plan.Root;
            var allItems = new[] { root }.Concat(plan.Items).ToArray();
            var occurrenceIds = allItems.ToDictionary(i => i.OccurrencePath, i => "occ-" + i.OccurrencePath.Replace('/', '-'), StringComparer.Ordinal);
            return new PdmPackageManifest
            {
                ProjectCode = plan.ProjectCode, Revision = plan.Revision, RootNodeId = root.NodeId,
                RootItemCode = root.ItemCode, RootFile = "cad/" + root.CanonicalFileName, RootOccurrenceId = "occ-0",
                Definitions = allItems.Select(i => new PdmManifestDefinition { DefinitionId = "def-" + i.OccurrencePath.Replace('/', '-'), NodeId = i.NodeId, ItemCode = i.ItemCode, ItemType = i.ItemType, DisplayName = i.DisplayName, Revision = i.Revision, FileName = "cad/" + i.CanonicalFileName }).ToArray(),
                Occurrences = allItems.Select(i => new PdmManifestOccurrence { OccurrenceId = occurrenceIds[i.OccurrencePath], OccurrencePath = i.OccurrencePath, ParentOccurrenceId = string.IsNullOrWhiteSpace(i.ParentNodeId) ? null : occurrenceIds[allItems.Single(p => p.NodeId == i.ParentNodeId).OccurrencePath], DefinitionId = "def-" + i.OccurrencePath.Replace('/', '-'), FindNumber = GetFindNumber(i.OccurrencePath) }).ToArray(),
                BomV2 = plan.Items.Where(i => !string.IsNullOrWhiteSpace(i.ParentNodeId)).Select(i => new PdmManifestBomV2 { ParentOccurrenceId = occurrenceIds[allItems.Single(p => p.NodeId == i.ParentNodeId).OccurrencePath], ChildDefinitionId = "def-" + i.OccurrencePath.Replace('/', '-'), Quantity = 1, QuantityStatus = "IdentityUnavailable" }).ToArray(),
                Warnings = plan.Warnings.Select(w => w.ToString()).ToArray()
            };
        }

        private static int GetFindNumber(string occurrencePath) { return (int.Parse(occurrencePath.Split('/').Last()) + 1) * 10; }
        private static string ToStableError(Exception ex)
        {
            var structured = ex as PdmNormalizeExportException;
            var code = structured == null ? ex.Message : structured.Code;
            if (string.IsNullOrWhiteSpace(code) || code.IndexOf(':') >= 0) code = "EXTERNAL_EXPORT_FAILED";
            return code + "\nKhông thể chuẩn hóa và xuất PDM an toàn.";
        }
    }
}
