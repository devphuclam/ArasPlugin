using System;
using System.Collections.Generic;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadDefinitionFileMapBuilder
    {
        public IDictionary<PdmSourceNode, string> Build(
            IDictionary<PdmSourceNode, ElementId> elementIds,
            PdmNormalizationPlan plan)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var allItems = new[] { plan.Root }.Concat(plan.Items).ToArray();
            var idToSourceNodes = new Dictionary<ElementId, List<PdmSourceNode>>();

            foreach (var item in allItems)
            {
                if (item.SourceNode == null) continue;
                if (!elementIds.TryGetValue(item.SourceNode, out var elementId))
                    continue;
                if (!idToSourceNodes.TryGetValue(elementId, out var list))
                    idToSourceNodes[elementId] = list = new List<PdmSourceNode>();
                list.Add(item.SourceNode);
            }

            var defFileMap = new Dictionary<PdmSourceNode, string>();
            foreach (var kvp in idToSourceNodes)
            {
                var firstItem = allItems.FirstOrDefault(i =>
                    i.SourceNode != null && kvp.Value.Contains(i.SourceNode));
                var fileName = firstItem != null
                    ? "cad/" + firstItem.CanonicalFileName
                    : "cad/unknown.ics";
                foreach (var sourceNode in kvp.Value)
                    defFileMap[sourceNode] = fileName;
            }

            return defFileMap;
        }
    }
}
