using System.Collections.Generic;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public sealed class BomDiagnosticNode
    {
        public string RuntimeId { get; set; }
        public string PersistentIdCandidate { get; set; }
        public string DefinitionIdentityCandidate { get; set; }
        public string OccurrenceIdentityCandidate { get; set; }
        public string ParentRuntimeId { get; set; }
        public int Depth { get; set; }
        public string DisplayName { get; set; }
        public string NodeKind { get; set; }
        public string ExternalFilePath { get; set; }
        public bool? IsExternal { get; set; }
        public bool? IsSuppressed { get; set; }
        public bool? IsVisible { get; set; }
        public bool? IncludedInBom { get; set; }
        public int CustomPropertyCount { get; set; }
        public int ChildCount { get; set; }
        public int? Quantity { get; set; }
        public IList<BomDiagnosticNode> Children { get; } = new List<BomDiagnosticNode>();
    }
}
