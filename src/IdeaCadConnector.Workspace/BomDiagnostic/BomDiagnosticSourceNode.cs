using System.Collections.Generic;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    /// <summary>
    /// Provider-neutral input from a CAD reader. It deliberately contains no COM or ICAPI types.
    /// </summary>
    public sealed class BomDiagnosticSourceNode
    {
        public string RuntimeId { get; set; }

        public string PersistentIdCandidate { get; set; }

        public string DefinitionIdentityCandidate { get; set; }

        public string OccurrenceIdentityCandidate { get; set; }

        public string DisplayName { get; set; }

        public string NodeKind { get; set; }

        public string ExternalFilePath { get; set; }

        public bool? IsExternal { get; set; }

        public bool? IsSuppressed { get; set; }

        public bool? IsVisible { get; set; }

        public bool? IncludedInBom { get; set; }

        public int CustomPropertyCount { get; set; }

        public IList<BomDiagnosticSourceNode> Children { get; set; }
    }
}
