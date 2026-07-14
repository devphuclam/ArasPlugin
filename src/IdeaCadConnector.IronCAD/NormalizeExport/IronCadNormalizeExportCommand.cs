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
            Exception cleanupFailure = null;
            try
            {
                var app = _addin.IronCADApp;
                if (app == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_UNAVAILABLE");
                var doc = app.ActiveDoc;
                var scene = doc as IZSceneDoc;
                if (doc == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_UNAVAILABLE");
                if (scene == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
                var activePath = doc.Name;
                if (string.IsNullOrWhiteSpace(activePath) || !Path.IsPathRooted(activePath) ||
                    !string.Equals(Path.GetExtension(activePath), ".ics", StringComparison.OrdinalIgnoreCase) || doc.Modified)
                    throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SAVED");

                var snapshot = _reader.Read(scene);
                var dependencies = _dependencyDiscovery.Discover(scene, Path.GetDirectoryName(activePath));
                if (!dependencies.DiscoveryComplete) throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                if (dependencies.ExternalDependencyCount != 0)
                    throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                var sourceFingerprints = new[] { PdmSourceIntegrity.Capture(activePath) }.ToList();
                var initialPlan = new PdmNormalizationPlanner().CreatePlan(
                    PdmNameNormalizer.DeriveProjectCodeFromRootFileName(activePath), "A", snapshot.Root);
                if (initialPlan.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                var dialog = new NormalizeExportDialog(initialPlan,
                    Path.Combine(Path.GetDirectoryName(activePath), initialPlan.ProjectCode + "-export"));
                if (dialog.ShowDialog() != true || dialog.Result == null) return;

                var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(snapshot.Root, dialog.Result);
                var issues = new PdmNormalizationPreflightValidator().Validate(finalPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED: " + string.Join(",", issues));
                var requestedPackage = Path.Combine(dialog.Result.OutputFolder,
                    dialog.Result.ProjectCode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                var outputIssues = new PdmOutputSafetyValidator().Validate(dialog.Result.OutputFolder, activePath, requestedPackage);
                if (outputIssues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED");

                stagingDirectory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "PDM-staging", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stagingDirectory);
                var stagedSourcePath = Path.Combine(stagingDirectory, Path.GetFileName(activePath));
                File.Copy(activePath, stagedSourcePath, false);
                app.OpenFile(stagedSourcePath, false);
                var stagedScene = _activationVerifier.VerifyScene(app, stagedSourcePath, "STAGING_DOCUMENT");
                var stagedSnapshot = _reader.Read(stagedScene);
                if (stagedSnapshot.Root.Properties == null) stagedSnapshot.Root.Properties = new PdmSourceProperties();
                if (string.IsNullOrWhiteSpace(stagedSnapshot.Root.Properties.NodeId))
                    stagedSnapshot.Root.Properties.NodeId = finalPlan.Root.NodeId;
                var stagedPlan = new PdmNormalizationPlanner().CreateFinalPlan(stagedSnapshot.Root, dialog.Result);
                var finalByPath = finalPlan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                var stagedByPath = stagedPlan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                if (finalByPath.Count != stagedByPath.Count || finalByPath.Keys.Any(path =>
                    !stagedByPath.ContainsKey(path) || stagedByPath[path].SourceKind != finalByPath[path].SourceKind))
                    throw new InvalidOperationException("STAGED_TREE_MISMATCH");
                issues = new PdmNormalizationPreflightValidator().Validate(stagedPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED: " + string.Join(",", issues));

                var packageStaging = Path.Combine(stagingDirectory, "package");
                var packageDirectory = requestedPackage;
                _writer.Apply(stagedSnapshot, stagedPlan);
                var stagedRootFile = _writer.Export(stagedScene, stagedSnapshot, stagedPlan, packageStaging);
                var manifest = CreateManifest(stagedPlan);
                File.WriteAllText(Path.Combine(packageStaging, "pdm-bom-manifest.json"),
                    new PdmPackageManifestWriter().Serialize(manifest));
                var validation = new PdmPackageValidator().Validate(packageStaging, manifest);
                if (!validation.IsValid) throw new InvalidOperationException("PACKAGE_VALIDATION_FAILED");
                app.OpenFile(stagedRootFile, false);
                var exportedScene = _activationVerifier.VerifyScene(app, stagedRootFile, "EXPORTED_ROOT");
                new IronCadExportPackageVerifier(_reader).Verify(exportedScene, stagedPlan);
                if (sourceFingerprints.Any(f => !PdmSourceIntegrity.Matches(f)))
                    throw new InvalidOperationException("SOURCE_FILE_CHANGED");
                if (Directory.Exists(packageDirectory)) throw new InvalidOperationException("OUTPUT_PACKAGE_EXISTS");
                Directory.Move(packageStaging, packageDirectory);
                var rootFile = Path.Combine(packageDirectory, "cad", Path.GetFileName(stagedRootFile));

                MessageBox.Show("Chuẩn hóa và xuất PDM thành công.\n\nProject: " + stagedPlan.ProjectCode +
                    "\nPackage: " + packageDirectory + "\nAssemblies: " + stagedPlan.Assemblies.Count +
                    "\nParts: " + stagedPlan.Parts.Count + "\nSource remains unchanged.",
                    "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ToStableError(ex), "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(stagingDirectory) && Directory.Exists(stagingDirectory))
                {
                    try { Directory.Delete(stagingDirectory, true); }
                    catch (Exception cleanupError) { cleanupFailure = cleanupError; System.Diagnostics.Trace.WriteLine("PDM_CLEANUP_FAILED: " + cleanupError); }
                }
                IsRunning = false;
                if (cleanupFailure != null)
                    MessageBox.Show("Không thể dọn staging an toàn.\nMã lỗi: STAGING_CLEANUP_FAILED", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static PdmPackageManifest CreateManifest(PdmNormalizationPlan plan)
        {
            var root = plan.Root;
            var allItems = new[] { root }.Concat(plan.Items).ToArray();
            var occurrenceIds = allItems.ToDictionary(i => i.NodeId, i => "occ-" + i.OccurrencePath.Replace('/', '-'), StringComparer.OrdinalIgnoreCase);
            return new PdmPackageManifest
            {
                ProjectCode = plan.ProjectCode, Revision = plan.Revision, RootNodeId = root.NodeId,
                RootItemCode = root.ItemCode, RootFile = "cad/" + root.CanonicalFileName,
                RootOccurrenceId = "occ-0",
                Definitions = allItems.Select(i => new PdmManifestDefinition
                {
                    DefinitionId = "def-" + i.OccurrencePath.Replace('/', '-'), NodeId = i.NodeId,
                    ItemCode = i.ItemCode, ItemType = i.ItemType, DisplayName = i.DisplayName,
                    Revision = i.Revision, FileName = "cad/" + i.CanonicalFileName
                }).ToArray(),
                Occurrences = allItems.Select(i => new PdmManifestOccurrence
                {
                    OccurrenceId = occurrenceIds[i.NodeId], OccurrencePath = i.OccurrencePath,
                    ParentOccurrenceId = string.IsNullOrWhiteSpace(i.ParentNodeId) ? null : occurrenceIds[i.ParentNodeId],
                    DefinitionId = "def-" + i.OccurrencePath.Replace('/', '-'), FindNumber = i.Depth * 10
                }).ToArray(),
                Items = plan.Items.Select(i => new PdmManifestItem
                {
                    NodeId = i.NodeId, ItemCode = i.ItemCode, ItemType = i.ItemType,
                    DisplayName = i.DisplayName, SceneName = i.SceneName,
                    FileName = "cad/" + i.CanonicalFileName, Revision = i.Revision
                }).ToArray(),
                Bom = plan.Items.Where(i => !string.IsNullOrWhiteSpace(i.ParentNodeId)).Select((i, index) => new PdmManifestBomEdge
                {
                    ParentNodeId = i.ParentNodeId, ChildNodeId = i.NodeId, FindNumber = (index + 1) * 10,
                    Quantity = 1, QuantityStatus = "IdentityUnavailable"
                }).ToArray(),
                Warnings = plan.Warnings.Select(w => w.ToString()).ToArray()
            };
        }

        private static string ToStableError(Exception ex)
        {
            var value = (ex as InvalidOperationException)?.Message;
            return "Không thể chuẩn hóa và xuất PDM.\nMã lỗi: " +
                (string.IsNullOrWhiteSpace(value) ? "EXTERNAL_EXPORT_FAILED" : value);
        }
    }
}
