using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public static class BomDiagnosticTreeAnalyzer
    {
        public static BomDiagnosticAnalysis Analyze(BomDiagnosticSourceNode root)
        {
            var result = new BomDiagnosticAnalysis { QuantityStatus = BomDiagnosticQuantityStatus.Verified };
            if (root == null)
            {
                result.QuantityStatus = BomDiagnosticQuantityStatus.IdentityUnavailable;
                result.Warnings.Add("Root node is missing.");
                return result;
            }

            var active = new HashSet<BomDiagnosticSourceNode>(ReferenceEqualityComparer.Instance);
            var runtimeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Walk(root, null, 0, active, runtimeIds, result);
            BuildQuantities(root, result, new HashSet<BomDiagnosticSourceNode>(ReferenceEqualityComparer.Instance));
            return result;
        }

        private static BomDiagnosticNode Walk(
            BomDiagnosticSourceNode source,
            string parentRuntimeId,
            int depth,
            ISet<BomDiagnosticSourceNode> active,
            IDictionary<string, int> runtimeIds,
            BomDiagnosticAnalysis result)
        {
            if (source == null)
            {
                result.Warnings.Add("Null child node was ignored.");
                return null;
            }
            if (!active.Add(source))
            {
                result.Warnings.Add("Tree cycle detected at runtime ID '" + (source.RuntimeId ?? "<missing>") + "'.");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(source.RuntimeId))
            {
                int count;
                runtimeIds.TryGetValue(source.RuntimeId, out count);
                count++;
                runtimeIds[source.RuntimeId] = count;
                if (count > 1)
                    result.Warnings.Add("duplicate runtime ID detected: '" + source.RuntimeId + "'.");
            }

            var node = ToNode(source, parentRuntimeId, depth);
            result.DepthFirstNodes.Add(node);
            if (result.RootNode == null)
                result.RootNode = node;
            result.MaxDepth = Math.Max(result.MaxDepth, depth);
            if (node.NodeKind == "Assembly") result.AssemblyCount++;
            else if (node.NodeKind == "Part") result.PartCount++;
            else result.TechnicalOrUnknownCount++;

            var children = source.Children;
            node.ChildCount = children == null ? 0 : children.Count;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var childNode = Walk(child, node.RuntimeId, depth + 1, active, runtimeIds, result);
                    if (childNode != null)
                        node.Children.Add(childNode);
                }
            }
            active.Remove(source);
            return node;
        }

        private static BomDiagnosticNode ToNode(BomDiagnosticSourceNode source, string parentRuntimeId, int depth)
        {
            return new BomDiagnosticNode
            {
                RuntimeId = source.RuntimeId,
                PersistentIdCandidate = source.PersistentIdCandidate,
                DefinitionIdentityCandidate = source.DefinitionIdentityCandidate,
                OccurrenceIdentityCandidate = source.OccurrenceIdentityCandidate,
                ParentRuntimeId = parentRuntimeId,
                Depth = depth,
                DisplayName = source.DisplayName,
                NodeKind = NormalizeKind(source.NodeKind),
                ExternalFilePath = source.ExternalFilePath,
                IsExternal = source.IsExternal,
                IsSuppressed = source.IsSuppressed,
                IsVisible = source.IsVisible,
                IncludedInBom = source.IncludedInBom,
                CustomPropertyCount = source.CustomPropertyCount
            };
        }

        private static string NormalizeKind(string kind)
        {
            if (string.Equals(kind, "Assembly", StringComparison.OrdinalIgnoreCase)) return "Assembly";
            if (string.Equals(kind, "Part", StringComparison.OrdinalIgnoreCase)) return "Part";
            return "TechnicalOrUnknown";
        }

        private static void BuildQuantities(
            BomDiagnosticSourceNode parent,
            BomDiagnosticAnalysis result,
            ISet<BomDiagnosticSourceNode> visited)
        {
            if (parent == null) return;
            if (!visited.Add(parent)) return;
            BuildQuantitiesForParent(parent, result);
            if (parent.Children == null) return;
            foreach (var child in parent.Children) BuildQuantities(child, result, visited);
        }

        private static void BuildQuantitiesForParent(BomDiagnosticSourceNode parent, BomDiagnosticAnalysis result)
        {
            if (parent.Children == null || parent.Children.Count == 0) return;
            var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in parent.Children)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.DefinitionIdentityCandidate))
                {
                    result.QuantityStatus = BomDiagnosticQuantityStatus.IdentityUnavailable;
                    continue;
                }
                int count;
                groups.TryGetValue(child.DefinitionIdentityCandidate, out count);
                groups[child.DefinitionIdentityCandidate] = count + 1;
            }
            foreach (var group in groups)
            {
                result.Quantities.Add(new BomDiagnosticQuantityRow
                {
                    ParentRuntimeId = parent.RuntimeId,
                    DefinitionIdentity = group.Key,
                    Quantity = group.Value,
                    Status = BomDiagnosticQuantityStatus.Verified
                });
                if (group.Value > 1) result.RepeatedDefinitionCount++;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<BomDiagnosticSourceNode>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(BomDiagnosticSourceNode x, BomDiagnosticSourceNode y) { return ReferenceEquals(x, y); }
            public int GetHashCode(BomDiagnosticSourceNode obj) { return RuntimeHelpers.GetHashCode(obj); }
        }
    }
}
