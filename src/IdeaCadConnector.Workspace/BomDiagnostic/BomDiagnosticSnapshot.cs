using System.Collections.Generic;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public enum BomDiagnosticQuantityStatus
    {
        Verified,
        IdentityUnavailable,
        AmbiguousDefinition
    }

    public sealed class BomDiagnosticQuantityRow
    {
        public string ParentRuntimeId { get; set; }
        public string DefinitionIdentity { get; set; }
        public int Quantity { get; set; }
        public BomDiagnosticQuantityStatus Status { get; set; }
    }

    public sealed class BomDiagnosticAnalysis
    {
        public BomDiagnosticNode RootNode { get; set; }
        public IList<BomDiagnosticNode> DepthFirstNodes { get; } = new List<BomDiagnosticNode>();
        public IList<BomDiagnosticQuantityRow> Quantities { get; } = new List<BomDiagnosticQuantityRow>();
        public IList<string> Warnings { get; } = new List<string>();
        public int AssemblyCount { get; set; }
        public int PartCount { get; set; }
        public int SceneRootCount { get; set; }
        public int TechnicalOrUnknownCount { get; set; }
        public int MaxDepth { get; set; }
        public int RepeatedDefinitionCount { get; set; }
        public BomDiagnosticQuantityStatus QuantityStatus { get; set; }
    }

    public sealed class BomDiagnosticSnapshot
    {
        public string DocumentName { get; set; }
        public string AuthoringToolVersion { get; set; }
        public string ActiveDocumentType { get; set; }
        public bool TopElementAvailable { get; set; }
        public BomDiagnosticAnalysis Analysis { get; set; }
        public string LocalReportWarning { get; set; } =
            "This local report may contain proprietary CAD metadata, names and paths.";
    }
}
