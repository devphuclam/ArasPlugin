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

        public IronCadNormalizeExportCommand(IronCadAddin addin)
        {
            _addin = addin ?? throw new ArgumentNullException(nameof(addin));
        }

        public void Execute()
        {
            string backup = null;
            IronCadSceneSnapshot snapshot = null;
            PdmNormalizationPlan plan = null;
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
                    !string.Equals(Path.GetExtension(activePath), ".ics", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SAVED");
                if (doc.Modified) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SAVED");

                snapshot = _reader.Read(scene);
                var suggestedProject = PdmNameNormalizer.DeriveProjectCodeFromRootFileName(activePath);
                plan = new PdmNormalizationPlanner().CreatePlan(suggestedProject, "A", snapshot.Root);
                if (plan.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                var defaultOutput = Path.Combine(Path.GetDirectoryName(activePath), plan.ProjectCode + "-export");
                var dialog = new NormalizeExportDialog(plan, defaultOutput);
                if (dialog.ShowDialog() != true) return;

                backup = _writer.CreateBackup(activePath);
                var packageDirectory = Path.Combine(dialog.OutputFolder,
                    dialog.ProjectCode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                _writer.Apply(snapshot, plan);
                scene.SaveAs(activePath, eZLinksSaveOptions.Z_LINKS_SAVE_ALL, true);
                var rootFile = _writer.Export(scene, snapshot, plan, packageDirectory);
                var manifest = CreateManifest(plan);
                File.WriteAllText(Path.Combine(packageDirectory, "pdm-bom-manifest.json"),
                    new PdmPackageManifestWriter().Serialize(manifest));
                var validation = new PdmPackageValidator().Validate(packageDirectory, manifest);
                if (!validation.IsValid)
                    throw new InvalidOperationException("MISSING_EXPORTED_FILE");
                app.OpenFile(rootFile, false);

                MessageBox.Show(
                    "Chuẩn hóa và xuất PDM thành công.\n\nProject: " + plan.ProjectCode +
                    "\nPackage: " + packageDirectory +
                    "\nAssemblies: " + plan.Assemblies.Count +
                    "\nParts: " + plan.Parts.Count +
                    "\nExported .ics files: " + Directory.GetFiles(Path.Combine(packageDirectory, "cad"), "*.ics").Length +
                    "\nBackup: " + backup,
                    "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (snapshot != null && plan != null)
                {
                    try { _writer.Restore(snapshot, plan); } catch { }
                }
                MessageBox.Show(ToStableError(ex), "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private static PdmPackageManifest CreateManifest(PdmNormalizationPlan plan)
        {
            var root = plan.Root;
            return new PdmPackageManifest
            {
                ProjectCode = plan.ProjectCode,
                Revision = plan.Revision,
                RootNodeId = root.NodeId,
                RootItemCode = root.ItemCode,
                RootFile = "cad/" + root.CanonicalFileName,
                Items = plan.Items.Select(i => new PdmManifestItem
                {
                    NodeId = i.NodeId,
                    ItemCode = i.ItemCode,
                    ItemType = i.ItemType,
                    DisplayName = i.DisplayName,
                    SceneName = i.SceneName,
                    FileName = "cad/" + i.CanonicalFileName,
                    Revision = i.Revision
                }).ToArray(),
                Bom = plan.Items.Where(i => !string.IsNullOrWhiteSpace(i.ParentNodeId)).Select((i, index) => new PdmManifestBomEdge
                {
                    ParentNodeId = i.ParentNodeId,
                    ChildNodeId = i.NodeId,
                    FindNumber = (index + 1) * 10,
                    Quantity = 1,
                    QuantityStatus = "OccurrenceBased"
                }).ToArray(),
                Warnings = plan.Warnings.Select(w => w.ToString()).ToArray()
            };
        }

        private static string ToStableError(Exception ex)
        {
            var category = ex as InvalidOperationException;
            var value = category == null ? null : category.Message;
            return "Không thể chuẩn hóa và xuất PDM.\nMã lỗi: " +
                (string.IsNullOrWhiteSpace(value) ? "EXTERNAL_EXPORT_FAILED" : value);
        }
    }
}
