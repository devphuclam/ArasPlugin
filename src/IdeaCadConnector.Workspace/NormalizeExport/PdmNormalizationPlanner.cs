using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmNormalizationPlanner
    {
        public PdmNormalizationPlan CreatePlan(string projectCode, string revision, PdmSourceNode root)
        {
            return CreatePlan(projectCode, revision, root, new PdmNormalizationLimits(), null, null);
        }

        public PdmNormalizationPlan CreatePlan(string projectCode, string revision, PdmSourceNode root,
            PdmNormalizationLimits limits)
        {
            return CreatePlan(projectCode, revision, root, limits, null, null);
        }

        public PdmNormalizationPlan CreateFinalPlan(PdmSourceNode root, NormalizeExportDialogResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var sourceEdits = (result.Edits ?? Enumerable.Empty<NormalizeExportEdit>())
                .Where(e => e != null && e.SourceNode != null)
                .GroupBy(e => e.SourceNode)
                .ToDictionary(g => g.Key, g => g.Last());
            var keyEdits = (result.Edits ?? Enumerable.Empty<NormalizeExportEdit>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.OccurrencePath))
                .GroupBy(e => e.OccurrencePath, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
            return CreatePlan(result.ProjectCode, result.Revision, root, new PdmNormalizationLimits(), sourceEdits, keyEdits);
        }

        private PdmNormalizationPlan CreatePlan(string projectCode, string revision, PdmSourceNode root,
            PdmNormalizationLimits limits, IDictionary<PdmSourceNode, NormalizeExportEdit> edits,
            IDictionary<string, NormalizeExportEdit> editsByKey)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (limits == null) limits = new PdmNormalizationLimits();
            var plan = new PdmNormalizationPlan
            {
                ProjectCode = PdmNameNormalizer.NormalizeProjectCode(projectCode),
                Revision = string.IsNullOrWhiteSpace(revision) ? "A" : revision.Trim().ToUpperInvariant()
            };
            var counters = new Dictionary<PdmNodeKind, int>();
            var nodeCount = 0;
            Visit(root, null, 0, "0", plan, counters, limits, new HashSet<PdmSourceNode>(), edits, editsByKey, ref nodeCount);
            AddDuplicateWarnings(plan);
            return plan;
        }

        private static void Visit(PdmSourceNode source, string parentId, int depth, string occurrencePath,
            PdmNormalizationPlan plan, IDictionary<PdmNodeKind, int> counters,
            PdmNormalizationLimits limits, ISet<PdmSourceNode> active,
            IDictionary<PdmSourceNode, NormalizeExportEdit> edits,
            IDictionary<string, NormalizeExportEdit> editsByKey, ref int nodeCount)
        {
            if (depth > limits.MaxDepth) throw new InvalidOperationException("PDM_TRAVERSAL_LIMIT_EXCEEDED");
            if (nodeCount >= limits.MaxNodeCount) throw new InvalidOperationException("PDM_TRAVERSAL_LIMIT_EXCEEDED");
            nodeCount++;
            if (!active.Add(source)) throw new InvalidOperationException("PDM_TRAVERSAL_CYCLE");
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
                    Depth = depth,
                    EditKey = occurrencePath,
                    OccurrencePath = occurrencePath
                };
            }
            if (source.Kind == PdmNodeKind.Assembly || source.Kind == PdmNodeKind.Part)
            {
                var parsed = PdmNameNormalizer.ParseNodeName(source.Name);
                var properties = source.Properties ?? new PdmSourceProperties();
                NormalizeExportEdit edit = null;
                edits?.TryGetValue(source, out edit);
                if (edit == null) editsByKey?.TryGetValue(occurrencePath, out edit);
                var code = edit != null && !string.IsNullOrWhiteSpace(edit.ItemCode) ? edit.ItemCode : string.IsNullOrWhiteSpace(properties.ItemCode)
                    ? parsed.ItemCode
                    : PdmNameNormalizer.NormalizeCode(properties.ItemCode);
                if (string.IsNullOrWhiteSpace(code))
                    code = NextCode(source.Kind, counters);
                var nodeId = edit != null && !string.IsNullOrWhiteSpace(edit.NodeId) ? edit.NodeId :
                    (string.IsNullOrWhiteSpace(properties.NodeId) ? Guid.NewGuid().ToString("D") : properties.NodeId);
                var type = source.Kind == PdmNodeKind.Assembly ? "ASM" : "PRT";
                var displayName = edit != null && !string.IsNullOrWhiteSpace(edit.DisplayName)
                    ? PdmNameNormalizer.NormalizeDisplayName(edit.DisplayName) : parsed.DisplayName;
                code = PdmNameNormalizer.NormalizeCode(code);
                var item = new PdmPlanItem
                {
                    SourceNode = source,
                    EditKey = occurrencePath,
                    OccurrencePath = occurrencePath,
                    NodeId = nodeId,
                    ParentNodeId = parentId,
                    SourceKind = source.Kind,
                    ItemType = type,
                    ItemCode = code,
                    DisplayName = displayName,
                    SceneName = code + "_" + displayName,
                    ProjectCode = plan.ProjectCode,
                    Revision = plan.Revision,
                    IsGeneric = parsed.IsGeneric && !(edit != null && edit.GenericNameConfirmed),
                    SourceWasGeneric = parsed.IsGeneric,
                    GenericNameConfirmed = edit != null && edit.GenericNameConfirmed,
                    Depth = depth,
                    CanonicalFileName = PdmNameNormalizer.CreateCanonicalFileName(
                        plan.ProjectCode, type, code, displayName)
                };
                if (item.IsGeneric) plan.Warnings.Add(PdmPlanWarning.GenericDisplayName);
                if (source.Kind == PdmNodeKind.Assembly) plan.Assemblies.Add(item); else plan.Parts.Add(item);
                if (plan.Root == null && parentId == null) plan.Root = item;
                currentParent = nodeId;
            }

            var childIndex = 0;
            foreach (var child in children)
                Visit(child, currentParent, depth + 1, occurrencePath + "/" + childIndex++, plan, counters, limits, active, edits, editsByKey, ref nodeCount);
            active.Remove(source);
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
