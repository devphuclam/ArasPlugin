using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IdeaCadConnector.Workspace.NormalizeExport;
using interop.ICApiIronCAD;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadDependencyRecord
    {
        public IZElement Occurrence { get; set; }
        public string NodeKind { get; set; }
        public bool IsExternal { get; set; }
        public string LinkPath { get; set; }
        public string ResolvedSourcePath { get; set; }
        public string ParentOccurrencePath { get; set; }
    }

    public sealed class IronCadDependencySet
    {
        public IList<IronCadDependencyRecord> Records { get; } = new List<IronCadDependencyRecord>();
        public bool DiscoveryComplete { get; set; }
        public string IsolationStatus { get; set; }
        public int ExternalDependencyCount { get { return Records.Count; } }
    }

    public sealed class IronCadDependencyDiscovery
    {
        public IronCadDependencySet Discover(IZSceneDoc scene, string sourceRoot)
        {
            return Discover(scene, sourceRoot, new PdmNormalizationLimits());
        }

        public IronCadDependencySet Discover(IZSceneDoc scene, string sourceRoot, PdmNormalizationLimits limits)
        {
            if (scene == null) throw new InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED");
            if (string.IsNullOrWhiteSpace(sourceRoot)) throw new InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED");
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            var set = new IronCadDependencySet();
            try
            {
                var root = scene.GetTopElement();
                var active = new HashSet<IZElement>(ReferenceComparer<IZElement>.Instance);
                var nodeCount = 0;
                Walk(root, null, "0", 0, Path.GetFullPath(sourceRoot), set, limits, active, ref nodeCount);
                foreach (var group in set.Records.GroupBy(r => r.ResolvedSourcePath, StringComparer.OrdinalIgnoreCase))
                    if (group.Select(r => r.NodeKind).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                        throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                set.DiscoveryComplete = true;
                set.IsolationStatus = set.ExternalDependencyCount == 0 ? "NotRequired" : "RequiredButRelinkUnverified";
                return set;
            }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex) { throw new InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED", ex); }
        }

        private static void Walk(IZElement element, string parentPath, string occurrencePath, int depth, string sourceRoot, IronCadDependencySet set, PdmNormalizationLimits limits, ISet<IZElement> active, ref int nodeCount)
        {
            if (element == null) throw new InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED");
            if (depth > limits.MaxDepth || nodeCount >= limits.MaxNodeCount) throw new InvalidOperationException("DEPENDENCY_TRAVERSAL_LIMIT_EXCEEDED");
            if (!active.Add(element)) throw new InvalidOperationException("DEPENDENCY_TRAVERSAL_CYCLE");
            nodeCount++;
            string link = null;
            bool external = false;
            try
            {
                var sceneElement = element as IZSceneElement;
                if (sceneElement != null)
                {
                    try { link = sceneElement.ModelLinkPath; }
                    catch (Exception ex) when (IsIgnorableModelLinkPathFailure(ex)) { link = null; }
                }
                var part = element as IZPart;
                var assembly = element as IZAssembly;
                bool linked;
                if (part != null) { var p = part.GetExternallyLinkedInfo(out linked); external |= linked; if (!string.IsNullOrWhiteSpace(p)) link = p; }
                if (assembly != null) { var p = assembly.GetExternallyLinkedInfo(out linked); external |= linked; if (!string.IsNullOrWhiteSpace(p)) link = p; }
            }
            catch (Exception ex) { throw new InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED", ex); }
            if (external || !string.IsNullOrWhiteSpace(link))
            {
                if (string.IsNullOrWhiteSpace(link) || !Path.IsPathRooted(link))
                    throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                var resolved = Path.GetFullPath(link);
                if (!IsWithin(resolved, sourceRoot) || !File.Exists(resolved))
                    throw new InvalidOperationException("BLOCKED_SOURCE_DEPENDENCY_ISOLATION");
                set.Records.Add(new IronCadDependencyRecord
                {
                    Occurrence = element, NodeKind = element.Type.ToString(), IsExternal = true,
                    LinkPath = link, ResolvedSourcePath = resolved, ParentOccurrencePath = parentPath
                });
            }
            IZArray children;
            try { children = element.GetChildrenZArray(); }
            catch (Exception ex) when (IsIgnorableModelLinkPathFailure(ex)) { children = null; }
            int count = 0;
            if (children == null) { active.Remove(element); return; }
            children.Count(out count);
            for (var i = 0; i < count; i++) { object value; children.Get(i, out value); Walk(value as IZElement, occurrencePath, occurrencePath + "/" + i, depth + 1, sourceRoot, set, limits, active, ref nodeCount); }
            active.Remove(element);
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) { return object.ReferenceEquals(x, y); }
            public int GetHashCode(T obj) { return RuntimeHelpers.GetHashCode(obj); }
        }

        public static bool IsIgnorableModelLinkPathFailure(Exception exception)
        {
            var com = exception as COMException;
            return com != null && unchecked((uint)com.ErrorCode) == 0x80004005u;
        }

        private static bool IsWithin(string path, string root)
        {
            var rootWithSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}
