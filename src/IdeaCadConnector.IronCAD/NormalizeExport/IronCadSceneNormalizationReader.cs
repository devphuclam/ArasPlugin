using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            return Read(scene, new PdmNormalizationLimits());
        }

        public IronCadSceneSnapshot Read(IZSceneDoc scene, PdmNormalizationLimits limits)
        {
            if (scene == null) throw new InvalidOperationException("ACTIVE_DOCUMENT_NOT_SCENE");
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            IZElement top;
            try { top = scene.GetTopElement(); }
            catch (Exception ex) { throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex); }
            if (top == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            var snapshot = new IronCadSceneSnapshot();
            var active = new HashSet<IZElement>(ReferenceComparer<IZElement>.Instance);
            var nodeCount = 0;
            snapshot.Root = ReadElement(top, snapshot, limits, active, ref nodeCount, true, 0);
            return snapshot;
        }

        private static PdmSourceNode ReadElement(IZElement element, IronCadSceneSnapshot snapshot,
            PdmNormalizationLimits limits, ISet<IZElement> active, ref int nodeCount,
            bool isRoot, int depth)
        {
            if (element == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
            if (depth > limits.MaxDepth || nodeCount >= limits.MaxNodeCount)
                throw new InvalidOperationException("PDM_TRAVERSAL_LIMIT_EXCEEDED");
            if (!active.Add(element)) throw new InvalidOperationException("PDM_TRAVERSAL_CYCLE");
            nodeCount++;
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
            catch (Exception ex) when (IronCadDependencyDiscovery.IsIgnorableModelLinkPathFailure(ex))
            {
                active.Remove(element);
                return node;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex);
            }
            if (array == null) return node;
            int count;
            array.Count(out count);
            for (int i = 0; i < count; i++)
            {
                object value;
                try { array.Get(i, out value); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED", ex);
                }
                var child = value as IZElement;
                if (child == null) throw new InvalidOperationException("SCENE_TRAVERSAL_FAILED");
                children.Add(ReadElement(child, snapshot, limits, active, ref nodeCount, false, depth + 1));
            }
            active.Remove(element);
            return node;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) { return object.ReferenceEquals(x, y); }
            public int GetHashCode(T obj) { return RuntimeHelpers.GetHashCode(obj); }
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
            result.ItemType = Read(manager, "PDM.ItemType");
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
