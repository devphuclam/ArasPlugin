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
        public bool IsRunning { get; private set; }

        public IronCadNormalizeExportCommand(IronCadAddin addin) { _addin = addin ?? throw new ArgumentNullException(nameof(addin)); }

        public void Execute()
        {
            if (IsRunning) return;
            IsRunning = true;
            string stagingDirectory = null;
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
                var initialPlan = new PdmNormalizationPlanner().CreatePlan(
                    PdmNameNormalizer.DeriveProjectCodeFromRootFileName(activePath), "A", snapshot.Root);
                if (initialPlan.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                var dialog = new NormalizeExportDialog(initialPlan,
                    Path.Combine(Path.GetDirectoryName(activePath), initialPlan.ProjectCode + "-export"));
                if (dialog.ShowDialog() != true || dialog.Result == null) return;

                var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(snapshot.Root, dialog.Result);
                var issues = new PdmNormalizationPreflightValidator().Validate(finalPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED: " + string.Join(",", issues));

                stagingDirectory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "PDM-staging", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stagingDirectory);
                var stagedSourcePath = Path.Combine(stagingDirectory, Path.GetFileName(activePath));
                File.Copy(activePath, stagedSourcePath, false);
                app.OpenFile(stagedSourcePath, false);
                var stagedScene = app.ActiveDoc as IZSceneDoc;
                if (stagedScene == null) throw new InvalidOperationException("STAGING_DOCUMENT_NOT_SCENE");
                var stagedSnapshot = _reader.Read(stagedScene);
                var stagedPlan = new PdmNormalizationPlanner().CreateFinalPlan(stagedSnapshot.Root, dialog.Result);
                issues = new PdmNormalizationPreflightValidator().Validate(stagedPlan, dialog.Result.OutputFolder);
                if (issues.Count != 0) throw new InvalidOperationException("PREFLIGHT_VALIDATION_FAILED: " + string.Join(",", issues));

                var packageStaging = Path.Combine(stagingDirectory, "package");
                var packageDirectory = Path.Combine(dialog.Result.OutputFolder,
                    dialog.Result.ProjectCode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                _writer.Apply(stagedSnapshot, stagedPlan);
                var stagedRootFile = _writer.Export(stagedScene, stagedSnapshot, stagedPlan, packageStaging);
                var manifest = CreateManifest(stagedPlan);
                File.WriteAllText(Path.Combine(packageStaging, "pdm-bom-manifest.json"),
                    new PdmPackageManifestWriter().Serialize(manifest));
                var validation = new PdmPackageValidator().Validate(packageStaging, manifest);
                if (!validation.IsValid) throw new InvalidOperationException("MISSING_EXPORTED_FILE");
                if (Directory.Exists(packageDirectory)) throw new InvalidOperationException("OUTPUT_PACKAGE_EXISTS");
                Directory.Move(packageStaging, packageDirectory);
                var rootFile = Path.Combine(packageDirectory, "cad", Path.GetFileName(stagedRootFile));
                app.OpenFile(rootFile, false);

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
                    catch (Exception cleanupError) { System.Diagnostics.Trace.WriteLine("PDM_CLEANUP_FAILED: " + cleanupError); }
                }
                IsRunning = false;
            }
        }

        private static PdmPackageManifest CreateManifest(PdmNormalizationPlan plan)
        {
            var root = plan.Root;
            return new PdmPackageManifest
            {
                ProjectCode = plan.ProjectCode, Revision = plan.Revision, RootNodeId = root.NodeId,
                RootItemCode = root.ItemCode, RootFile = "cad/" + root.CanonicalFileName,
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
