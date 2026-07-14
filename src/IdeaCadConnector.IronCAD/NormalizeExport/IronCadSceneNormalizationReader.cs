using System;
using System.Collections.Generic;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadSceneSnapshot
    {
        public PdmSourceNode Root { get; set; }
        public IDictionary<PdmSourceNode, IZElement> Elements { get; } =
            new Dictionary<PdmSourceNode, IZElement>();
    }

    public sealed class IronCadSceneNormalizationReader
    {
        public IronCadSceneSnapshot Read(IZSceneDoc scene)
        {
            if (scene == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
            IZElement top;
            try { top = scene.GetTopElement(); }
            catch (Exception ex) { throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex); }
            if (top == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            var snapshot = new IronCadSceneSnapshot();
            snapshot.Root = ReadElement(top, snapshot, true);
            return snapshot;
        }

        private static PdmSourceNode ReadElement(IZElement element, IronCadSceneSnapshot snapshot, bool isRoot = false)
        {
            PdmNodeKind kind;
            string name;
            try
            {
                kind = isRoot ? PdmNodeKind.SceneRoot : MapKind(element.Type);
                name = element.Name ?? string.Empty;
            }
            catch (Exception ex) { throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex); }
            var node = new PdmSourceNode { Kind = kind, Name = name, Properties = ReadProperties(element), Children = new List<PdmSourceNode>() };
            snapshot.Elements[node] = element;
            var children = (List<PdmSourceNode>)node.Children;
            IZArray array;
            try { array = element.GetChildrenZArray(); }
            catch (Exception ex)
            {
                if (node.Kind == PdmNodeKind.Technical) return node;
                throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex);
            }
            if (array == null) return node;
            int count;
            array.Count(out count);
            for (int i = 0; i < count; i++)
            {
                object value;
                try { array.Get(i, out value); }
                catch
                {
                    if (node.Kind == PdmNodeKind.Technical) continue;
                    throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                }
                var child = value as IZElement;
                if (child != null) children.Add(ReadElement(child, snapshot));
            }
            return node;
        }

        private static PdmNodeKind MapKind(eZElementType type)
        {
            switch (type)
            {
                case eZElementType.Z_ELEMENT_ASSEMBLY: return PdmNodeKind.Assembly;
                case eZElementType.Z_ELEMENT_PART: return PdmNodeKind.Part;
                default: return PdmNodeKind.Technical;
            }
        }

        private static PdmSourceProperties ReadProperties(IZElement element)
        {
            var result = new PdmSourceProperties();
            IZCustomPropMgr manager;
            try { manager = element.GetCustomPropManager(1); }
            catch { return result; }
            if (manager == null) return result;
            result.NodeId = Read(manager, "PDM.NodeId");
            result.ItemCode = Read(manager, "PDM.ItemCode");
            result.Revision = Read(manager, "PDM.Revision");
            result.DisplayName = Read(manager, "PDM.DisplayName");
            result.ProjectCode = Read(manager, "PDM.ProjectCode");
            return result;
        }

        private static string Read(IZCustomPropMgr manager, string name)
        {
            try
            {
                string value;
                bool found;
                manager.GetCustomPropAsString(name, out value, out found);
                return found ? value : null;
            }
            catch { return null; }
        }
    }
}
