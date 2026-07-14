using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public static class PdmManifestV2Factory
    {
        public static PdmPackageManifest Create(PdmNormalizationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Root == null) throw new ArgumentException("The normalization plan must have a root.", nameof(plan));

            var allItems = new[] { plan.Root }.Concat(plan.Items).ToArray();
            var occurrenceIds = allItems.ToDictionary(
                item => item.OccurrencePath,
                item => "occ-" + item.OccurrencePath.Replace('/', '-'),
                StringComparer.Ordinal);
            var occurrencePathsByNodeId = allItems.ToDictionary(
                item => item.NodeId,
                item => item.OccurrencePath,
                StringComparer.OrdinalIgnoreCase);

            return new PdmPackageManifest
            {
                SchemaVersion = 2,
                ProjectCode = plan.ProjectCode,
                Revision = plan.Revision,
                RootNodeId = plan.Root.NodeId,
                RootItemCode = plan.Root.ItemCode,
                RootFile = "cad/" + plan.Root.CanonicalFileName,
                RootOccurrenceId = occurrenceIds[plan.Root.OccurrencePath],
                Definitions = allItems.Select(CreateDefinition).ToArray(),
                Occurrences = allItems.Select(item => new PdmManifestOccurrence
                {
                    OccurrenceId = occurrenceIds[item.OccurrencePath],
                    OccurrencePath = item.OccurrencePath,
                    ParentOccurrenceId = GetParentOccurrenceId(item, occurrenceIds, occurrencePathsByNodeId),
                    DefinitionId = GetDefinitionId(item),
                    FindNumber = GetFindNumber(item.OccurrencePath)
                }).ToArray(),
                BomV2 = plan.Items.Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeId)).Select(item => new PdmManifestBomV2
                {
                    ParentOccurrenceId = occurrenceIds[occurrencePathsByNodeId[item.ParentNodeId]],
                    ChildDefinitionId = GetDefinitionId(item),
                    Quantity = 1,
                    QuantityStatus = "IdentityUnavailable"
                }).ToArray(),
                Warnings = plan.Warnings.Select(warning => warning.ToString()).ToArray()
            };
        }

        private static PdmManifestDefinition CreateDefinition(PdmPlanItem item)
        {
            return new PdmManifestDefinition
            {
                DefinitionId = GetDefinitionId(item),
                NodeId = item.NodeId,
                ItemCode = item.ItemCode,
                ItemType = item.ItemType,
                DisplayName = item.DisplayName,
                Revision = item.Revision,
                FileName = "cad/" + item.CanonicalFileName
            };
        }

        private static string GetParentOccurrenceId(
            PdmPlanItem item,
            IDictionary<string, string> occurrenceIds,
            IDictionary<string, string> occurrencePathsByNodeId)
        {
            if (string.IsNullOrWhiteSpace(item.ParentNodeId)) return null;
            return occurrenceIds[occurrencePathsByNodeId[item.ParentNodeId]];
        }

        private static string GetDefinitionId(PdmPlanItem item)
        {
            return "def-" + item.OccurrencePath.Replace('/', '-');
        }

        private static int GetFindNumber(string occurrencePath)
        {
            var segment = occurrencePath.Split('/').Last();
            return (int.Parse(segment) + 1) * 10;
        }
    }
}
