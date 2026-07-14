using System;
using System.Collections.Generic;
using System.IO;
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

        private static void ApplyItem(IronCadSceneSnapshot snapshot, PdmPlanItem item, bool rename)
        {
            if (item == null || item.SourceNode == null) return;
            IZElement element;
            if (!snapshot.Elements.TryGetValue(item.SourceNode, out element))
                throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
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


        public string Export(IZSceneDoc scene, IronCadSceneSnapshot snapshot, PdmNormalizationPlan plan, string outputFolder)
        {
            if (scene == null || snapshot == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
            if (plan?.Root == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            Directory.CreateDirectory(outputFolder);
            var cad = Path.Combine(outputFolder, "cad");
            Directory.CreateDirectory(cad);
            var rootPath = Path.Combine(cad, plan.Root.CanonicalFileName);
            scene.SaveAsCopy(rootPath, eZLinksSaveOptions.Z_LINKS_SAVE_ALL, true);
            foreach (var item in plan.Items)
            {
                IZElement element;
                if (!snapshot.Elements.TryGetValue(item.SourceNode, out element))
                    throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                var part = element as IZPart;
                var assembly = element as IZAssembly;
                var filePath = Path.Combine(cad, item.CanonicalFileName);
                if (part != null) part.SaveAs(filePath, true);
                else if (assembly != null) assembly.SaveAs(filePath, true);
                else throw new InvalidOperationException("EXTERNAL_EXPORT_FAILED");
            }
            return rootPath;
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
