using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmNormalizationPlanner
    {
        public PdmNormalizationPlan CreatePlan(string projectCode, string revision, PdmSourceNode root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var plan = new PdmNormalizationPlan
            {
                ProjectCode = PdmNameNormalizer.NormalizeProjectCode(projectCode),
                Revision = string.IsNullOrWhiteSpace(revision) ? "A" : revision.Trim().ToUpperInvariant()
            };
            var counters = new Dictionary<PdmNodeKind, int>();
            Visit(root, null, 0, plan, counters);
            AddDuplicateWarnings(plan);
            return plan;
        }

        private static void Visit(PdmSourceNode source, string parentId, int depth,
            PdmNormalizationPlan plan, IDictionary<PdmNodeKind, int> counters)
        {
            var children = source.Children ?? Enumerable.Empty<PdmSourceNode>();
            var currentParent = parentId;
            if (source.Kind == PdmNodeKind.SceneRoot && parentId == null && plan.Root == null)
            {
                var rootProperties = source.Properties ?? new PdmSourceProperties();
                var rootDisplayName = PdmNameNormalizer.NormalizeDisplayName(source.Name);
                if (string.IsNullOrWhiteSpace(rootDisplayName)) rootDisplayName = plan.ProjectCode;
                plan.Root = new PdmPlanItem
                {
                    SourceNode = source,
                    NodeId = string.IsNullOrWhiteSpace(rootProperties.NodeId)
                        ? Guid.NewGuid().ToString("D") : rootProperties.NodeId,
                    SourceKind = PdmNodeKind.SceneRoot,
                    ItemType = "ASM",
                    ItemCode = "ROOT",
                    DisplayName = rootDisplayName,
                    SceneName = source.Name,
                    ProjectCode = plan.ProjectCode,
                    Revision = plan.Revision,
                    CanonicalFileName = PdmNameNormalizer.CreateCanonicalFileName(
                        plan.ProjectCode, "ASM", "ROOT", rootDisplayName),
                    Depth = depth
                };
            }
            if (source.Kind == PdmNodeKind.Assembly || source.Kind == PdmNodeKind.Part)
            {
                var parsed = PdmNameNormalizer.ParseNodeName(source.Name);
                var properties = source.Properties ?? new PdmSourceProperties();
                var code = string.IsNullOrWhiteSpace(properties.ItemCode)
                    ? parsed.ItemCode
                    : PdmNameNormalizer.NormalizeCode(properties.ItemCode);
                if (string.IsNullOrWhiteSpace(code))
                    code = NextCode(source.Kind, counters);
                var nodeId = string.IsNullOrWhiteSpace(properties.NodeId)
                    ? Guid.NewGuid().ToString("D")
                    : properties.NodeId;
                var type = source.Kind == PdmNodeKind.Assembly ? "ASM" : "PRT";
                var item = new PdmPlanItem
                {
                    SourceNode = source,
                    NodeId = nodeId,
                    ParentNodeId = parentId,
                    SourceKind = source.Kind,
                    ItemType = type,
                    ItemCode = code,
                    DisplayName = parsed.DisplayName,
                    SceneName = code + "_" + parsed.DisplayName,
                    ProjectCode = plan.ProjectCode,
                    Revision = plan.Revision,
                    IsGeneric = parsed.IsGeneric,
                    Depth = depth,
                    CanonicalFileName = PdmNameNormalizer.CreateCanonicalFileName(
                        plan.ProjectCode, type, code, parsed.DisplayName)
                };
                if (item.IsGeneric) plan.Warnings.Add(PdmPlanWarning.GenericDisplayName);
                if (source.Kind == PdmNodeKind.Assembly) plan.Assemblies.Add(item); else plan.Parts.Add(item);
                if (plan.Root == null && parentId == null) plan.Root = item;
                currentParent = nodeId;
            }

            foreach (var child in children)
                Visit(child, currentParent, depth + 1, plan, counters);
        }

        private static string NextCode(PdmNodeKind kind, IDictionary<PdmNodeKind, int> counters)
        {
            int count;
            counters.TryGetValue(kind, out count);
            count++;
            counters[kind] = count;
            return (kind == PdmNodeKind.Assembly ? "ASM-" : "PRT-") + count.ToString("000");
        }

        private static void AddDuplicateWarnings(PdmNormalizationPlan plan)
        {
            if (plan.Items.GroupBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                plan.Warnings.Add(PdmPlanWarning.DuplicateItemCode);
            if (plan.Items.GroupBy(i => i.CanonicalFileName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                plan.Warnings.Add(PdmPlanWarning.DuplicateFileName);
        }
    }
}
