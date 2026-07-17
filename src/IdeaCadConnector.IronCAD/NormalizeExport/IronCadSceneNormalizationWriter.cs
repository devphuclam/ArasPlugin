using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadSceneNormalizationWriter
    {
        public void Apply(IronCadSceneSnapshot snapshot, PdmNormalizationPlan plan)
        {
            if (snapshot == null || plan == null) throw new ArgumentNullException();
            ApplyItem(snapshot, plan.Root, false);
            foreach (var item in plan.Items) ApplyItem(snapshot, item, true);
        }

        public IronCadExportResult Export(
            IZBaseApp app,
            IZSceneDoc sourceScene,
            IronCadSceneSnapshot snapshot,
            PdmNormalizationPlan plan,
            string outputFolder)
        {
            if (app == null || sourceScene == null || snapshot == null)
                throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
            if (plan?.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");

            Directory.CreateDirectory(outputFolder);
            var cad = Path.Combine(outputFolder, "cad");
            Directory.CreateDirectory(cad);
            var rootPath = Path.Combine(cad, plan.Root.CanonicalFileName);

            var definitionMap = new IronCadDefinitionFileMapBuilder()
                .Build(snapshot.ElementIds, plan);
            SaveWithNativeExternalization(sourceScene, snapshot, plan, definitionMap, rootPath, outputFolder);

            return new IronCadExportResult
            {
                RootFilePath = rootPath,
                SourceNodeToDefFileMap = definitionMap
            };
        }

        private void SaveWithNativeExternalization(
            IZSceneDoc sourceScene,
            IronCadSceneSnapshot snapshot,
            PdmNormalizationPlan plan,
            IDictionary<PdmSourceNode, string> definitionMap,
            string rootPath,
            string outputFolder)
        {
            WriteProgress("NATIVE_BEGIN " + rootPath);

            // IronCAD's native command derives external filenames from the
            // definition names. Use canonical stems only while it performs
            // the externalization, then restore the approved scene names.
            foreach (var item in plan.Items)
            {
                if (item?.SourceNode == null) continue;
                if (!snapshot.Elements.TryGetValue(item.SourceNode, out var element))
                    throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                element.Name = Path.GetFileNameWithoutExtension(item.CanonicalFileName);
            }

            sourceScene.SaveAs(rootPath, eZLinksSaveOptions.Z_LINKS_IGNORE, true);
            WriteProgress("NATIVE_ROOT_STAGED " + rootPath);

            new IronCadNativeSaveAllExternalInvoker().Execute(Path.GetDirectoryName(rootPath));
            WriteProgress("NATIVE_COMMAND_COMPLETED");

            var missing = definitionMap.Values
                .Select(relative => Path.Combine(outputFolder, relative.Replace('/', Path.DirectorySeparatorChar)))
                .Where(path => !string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !File.Exists(path))
                .ToArray();
            if (missing.Length != 0)
            {
                var actual = Directory.GetFiles(Path.GetDirectoryName(rootPath), "*.ics")
                    .Select(Path.GetFileName);
                throw new InvalidOperationException(
                    "NATIVE_EXTERNALIZATION_FAILED Missing=" + string.Join(";", missing) +
                    " Actual=" + string.Join(";", actual));
            }

            Apply(snapshot, plan);
            sourceScene.Update();
            sourceScene.SaveAs(rootPath, eZLinksSaveOptions.Z_LINKS_SAVE_ALL, true);
            WriteProgress("NATIVE_SAVED_ALL " + rootPath);
        }

        private static void WriteProgress(string message)
        {
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "IdeaCadConnector");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "pdm-writer-progress.txt"),
                    DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        private static void ApplyItem(IronCadSceneSnapshot snapshot, PdmPlanItem item, bool rename)
        {
            if (item == null || item.SourceNode == null) return;
            if (!snapshot.Elements.TryGetValue(item.SourceNode, out var element))
                throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            ApplyItem(element, item, rename);
        }

        private static void ApplyItem(IZElement element, PdmPlanItem item, bool rename)
        {
            if (element == null || item == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            var manager = element.GetCustomPropManager(1);
            if (manager == null) throw new InvalidOperationException("CUSTOM_PROPERTY_WRITE_FAILED");
            Set(manager, "PDM.NodeId", item.NodeId);
            Set(manager, "PDM.ItemCode", item.ItemCode);
            Set(manager, "PDM.ItemType", item.ItemType);
            Set(manager, "PDM.DisplayName", item.DisplayName);
            Set(manager, "PDM.ProjectCode", item.ProjectCode);
            Set(manager, "PDM.Revision", item.Revision);
            if (rename) element.Name = item.SceneName;
        }

        private static void Set(IZCustomPropMgr manager, string name, string value)
        {
            try
            {
                manager.AddCustomPropString(name, value ?? string.Empty,
                    eZPropPersFlag.Z_PPO_PERS_FLAG_NONE, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("CUSTOM_PROPERTY_WRITE_FAILED", ex);
            }
        }
    }
}
