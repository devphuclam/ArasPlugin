using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public static class PdmManifestV2Factory
    {
        public static PdmPackageManifest Create(PdmNormalizationPlan plan,
            IDictionary<PdmSourceNode, string> sourceNodeToDefFileMap = null)
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

            IDictionary<string, string> definitionIdByFile = null;
            IDictionary<string, PdmPlanItem> representativeByFile = null;
            var missingMapWarnings = new List<string>();
            if (sourceNodeToDefFileMap != null)
            {
                definitionIdByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                representativeByFile = new Dictionary<string, PdmPlanItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in allItems)
                {
                    if (item.SourceNode == null)
                    {
                        missingMapWarnings.Add(
                            "MISSING_DEFINITION_MAP_ENTRY: Plan item " + item.OccurrencePath
                            + " (" + item.ItemCode + ") has no SourceNode reference.");
                        continue;
                    }
                    if (!sourceNodeToDefFileMap.TryGetValue(item.SourceNode, out var defFile))
                    {
                        missingMapWarnings.Add(
                            "MISSING_DEFINITION_MAP_ENTRY: SourceNode for plan item " + item.OccurrencePath
                            + " (" + item.ItemCode + ") is not present in the definition-file map.");
                        continue;
                    }
                    if (definitionIdByFile.ContainsKey(defFile)) continue;
                    definitionIdByFile[defFile] = "def-" + defFile.Replace('/', '-').Replace('.', '-');
                    representativeByFile[defFile] = item;
                }
            }

            return new PdmPackageManifest
            {
                SchemaVersion = 2,
                ProjectCode = plan.ProjectCode,
                Revision = plan.Revision,
                RootNodeId = plan.Root.NodeId,
                RootItemCode = plan.Root.ItemCode,
                RootFile = "cad/" + plan.Root.CanonicalFileName,
                RootOccurrenceId = occurrenceIds[plan.Root.OccurrencePath],
                Definitions = sourceNodeToDefFileMap != null
                    ? BuildDeduplicatedDefinitions(allItems, sourceNodeToDefFileMap, definitionIdByFile, representativeByFile)
                    : allItems.Select(CreateDefinition).ToArray(),
                Occurrences = allItems.Select(item => BuildOccurrence(item, occurrenceIds, occurrencePathsByNodeId,
                    sourceNodeToDefFileMap, definitionIdByFile)).ToArray(),
                BomV2 = plan.Items.Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeId)).Select(item => new PdmManifestBomV2
                {
                    ParentOccurrenceId = occurrenceIds[occurrencePathsByNodeId[item.ParentNodeId]],
                    ChildDefinitionId = GetDefinitionIdForItem(item, sourceNodeToDefFileMap, definitionIdByFile),
                    Quantity = 1,
                    QuantityStatus = "IdentityUnavailable"
                }).ToArray(),
                Warnings = plan.Warnings.Select(warning => warning.ToString())
                    .Concat(missingMapWarnings).ToArray()
            };
        }

        private static PdmManifestDefinition[] BuildDeduplicatedDefinitions(
            PdmPlanItem[] allItems,
            IDictionary<PdmSourceNode, string> sourceNodeToDefFileMap,
            IDictionary<string, string> definitionIdByFile,
            IDictionary<string, PdmPlanItem> representativeByFile)
        {
            var definitions = new List<PdmManifestDefinition>();
            foreach (var item in allItems)
            {
                if (item.SourceNode == null) continue;
                if (!sourceNodeToDefFileMap.TryGetValue(item.SourceNode, out var defFile)) continue;
                if (representativeByFile.TryGetValue(defFile, out var rep) && rep != item) continue;
                var defId = definitionIdByFile[defFile];
                definitions.Add(new PdmManifestDefinition
                {
                    DefinitionId = defId,
                    NodeId = item.NodeId,
                    ItemCode = item.ItemCode,
                    ItemType = item.ItemType,
                    DisplayName = item.DisplayName,
                    Revision = item.Revision,
                    FileName = ToPackageRelativeCadPath(defFile)
                });
            }
            return definitions.ToArray();
        }

        private static PdmManifestOccurrence BuildOccurrence(
            PdmPlanItem item,
            IDictionary<string, string> occurrenceIds,
            IDictionary<string, string> occurrencePathsByNodeId,
            IDictionary<PdmSourceNode, string> sourceNodeToDefFileMap,
            IDictionary<string, string> definitionIdByFile)
        {
            var occ = new PdmManifestOccurrence
            {
                OccurrenceId = occurrenceIds[item.OccurrencePath],
                OccurrencePath = item.OccurrencePath,
                ParentOccurrenceId = GetParentOccurrenceId(item, occurrenceIds, occurrencePathsByNodeId),
                DefinitionId = GetDefinitionIdForItem(item, sourceNodeToDefFileMap, definitionIdByFile),
                FindNumber = GetFindNumber(item.OccurrencePath)
            };
            if (sourceNodeToDefFileMap != null && item.SourceNode != null
                && sourceNodeToDefFileMap.TryGetValue(item.SourceNode, out var defFile))
            {
                occ.DefinitionFile = defFile;
            }
            return occ;
        }

        private static string GetDefinitionIdForItem(
            PdmPlanItem item,
            IDictionary<PdmSourceNode, string> sourceNodeToDefFileMap,
            IDictionary<string, string> definitionIdByFile)
        {
            if (sourceNodeToDefFileMap != null && item.SourceNode != null
                && sourceNodeToDefFileMap.TryGetValue(item.SourceNode, out var defFile)
                && definitionIdByFile.TryGetValue(defFile, out var defId))
            {
                return defId;
            }
            return "def-" + item.OccurrencePath.Replace('/', '-');
        }

        private static string ToPackageRelativeCadPath(string definitionFile)
        {
            if (string.IsNullOrWhiteSpace(definitionFile)) return definitionFile;
            return definitionFile.StartsWith("cad/", StringComparison.OrdinalIgnoreCase)
                || definitionFile.StartsWith("cad\\", StringComparison.OrdinalIgnoreCase)
                ? definitionFile.Replace('\\', '/')
                : "cad/" + definitionFile.Replace('\\', '/');
        }

        private static PdmManifestDefinition CreateDefinition(PdmPlanItem item)
        {
            return new PdmManifestDefinition
            {
                DefinitionId = "def-" + item.OccurrencePath.Replace('/', '-'),
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
