using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public sealed class BomDiagnosticAggregateEvidence
    {
        public int TotalNodes { get; set; }
        public int AssemblyCount { get; set; }
        public int PartCount { get; set; }
        public int SceneRootCount { get; set; }
        public int TechnicalOrUnknownCount { get; set; }
        public int MaximumDepth { get; set; }
        public int RepeatedDefinitionCount { get; set; }
        public string QuantityStatus { get; set; }
        public int NodesWithRuntimeIds { get; set; }
        public int NodesWithDefinitionIdentityCandidates { get; set; }
        public int NodesWithOccurrenceIdentityCandidates { get; set; }
        public int ExternalLinkCount { get; set; }
        public int SuppressedCount { get; set; }
        public int HiddenCount { get; set; }
        public int ExcludedFromBomCount { get; set; }
        public int WarningCount { get; set; }
        public IList<string> Warnings { get; } = new List<string>();

        public string ToJson() { return JsonConvert.SerializeObject(this, Formatting.Indented); }
    }

    public static class BomDiagnosticSanitizer
    {
        public static BomDiagnosticAggregateEvidence CreateAggregate(BomDiagnosticAnalysis analysis)
        {
            analysis = analysis ?? new BomDiagnosticAnalysis();
            var result = new BomDiagnosticAggregateEvidence
            {
                TotalNodes = analysis.DepthFirstNodes.Count,
                AssemblyCount = analysis.AssemblyCount,
                PartCount = analysis.PartCount,
                SceneRootCount = analysis.SceneRootCount,
                TechnicalOrUnknownCount = analysis.TechnicalOrUnknownCount,
                MaximumDepth = analysis.MaxDepth,
                RepeatedDefinitionCount = analysis.RepeatedDefinitionCount,
                QuantityStatus = analysis.QuantityStatus.ToString(),
                NodesWithRuntimeIds = analysis.DepthFirstNodes.Count(node => !string.IsNullOrWhiteSpace(node.RuntimeId)),
                NodesWithDefinitionIdentityCandidates = analysis.DepthFirstNodes.Count(node => !string.IsNullOrWhiteSpace(node.DefinitionIdentityCandidate)),
                NodesWithOccurrenceIdentityCandidates = analysis.DepthFirstNodes.Count(node => !string.IsNullOrWhiteSpace(node.OccurrenceIdentityCandidate)),
                ExternalLinkCount = analysis.DepthFirstNodes.Count(node => node.IsExternal == true),
                SuppressedCount = analysis.DepthFirstNodes.Count(node => node.IsSuppressed == true),
                HiddenCount = analysis.DepthFirstNodes.Count(node => node.IsVisible == false),
                ExcludedFromBomCount = analysis.DepthFirstNodes.Count(node => node.IncludedInBom == false)
            };
            foreach (var warning in analysis.Warnings)
            {
                var category = BomDiagnosticWarningCategories.Classify(warning);
                if (!result.Warnings.Contains(category)) result.Warnings.Add(category);
            }
            result.WarningCount = result.Warnings.Count;
            return result;
        }
    }

    public static class BomDiagnosticWarningCategories
    {
        public static string Classify(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning)) return "UNKNOWN_READ_WARNING";
            if (warning.IndexOf("active document", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "ACTIVE_DOCUMENT_READ_FAILED";
            if (warning.IndexOf("top element", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "TOP_ELEMENT_UNAVAILABLE";
            if (warning.IndexOf("custom propert", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "CUSTOM_PROPERTY_READ_FAILED";
            if (warning.IndexOf("children", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "CHILD_ENUMERATION_FAILED";
            if (warning.IndexOf("model link", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                warning.IndexOf("external-link", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "MODEL_LINK_READ_FAILED";
            if (warning.IndexOf("cycle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "CYCLE_DETECTED";
            if (warning.IndexOf("depth", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "DEPTH_LIMIT_REACHED";
            if (warning.IndexOf("node limit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "NODE_LIMIT_REACHED";
            return "UNKNOWN_READ_WARNING";
        }
    }
}
