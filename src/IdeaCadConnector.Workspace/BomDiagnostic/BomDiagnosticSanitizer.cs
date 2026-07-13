using System.Collections.Generic;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public sealed class BomDiagnosticAggregateEvidence
    {
        public int TotalNodes { get; set; }
        public int AssemblyCount { get; set; }
        public int PartCount { get; set; }
        public int TechnicalOrUnknownCount { get; set; }
        public int MaximumDepth { get; set; }
        public int RepeatedDefinitionCount { get; set; }
        public string QuantityStatus { get; set; }
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
                TechnicalOrUnknownCount = analysis.TechnicalOrUnknownCount,
                MaximumDepth = analysis.MaxDepth,
                RepeatedDefinitionCount = analysis.RepeatedDefinitionCount,
                QuantityStatus = analysis.QuantityStatus.ToString(),
                WarningCount = analysis.Warnings.Count
            };
            foreach (var warning in analysis.Warnings) result.Warnings.Add(warning);
            return result;
        }
    }
}
