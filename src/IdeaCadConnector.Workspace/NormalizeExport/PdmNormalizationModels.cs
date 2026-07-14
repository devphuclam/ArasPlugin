using System;
using System.Collections.Generic;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public enum PdmNodeKind
    {
        Technical,
        SceneRoot,
        Assembly,
        Part
    }

    public sealed class PdmSourceProperties
    {
        public string NodeId { get; set; }
        public string ItemCode { get; set; }
        public string Revision { get; set; }
        public string DisplayName { get; set; }
        public string ProjectCode { get; set; }
    }

    public sealed class PdmSourceNode
    {
        public PdmNodeKind Kind { get; set; }
        public string Name { get; set; }
        public PdmSourceProperties Properties { get; set; }
        public IEnumerable<PdmSourceNode> Children { get; set; }
    }

    public sealed class PdmNameParts
    {
        public string ItemCode { get; set; }
        public string DisplayName { get; set; }
        public bool IsGeneric { get; set; }
    }

    public sealed class PdmPlanItem
    {
        public PdmSourceNode SourceNode { get; set; }
        public string NodeId { get; set; }
        public PdmNodeKind SourceKind { get; set; }
        public string ItemType { get; set; }
        public string ItemCode { get; set; }
        public string DisplayName { get; set; }
        public string SceneName { get; set; }
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public string CanonicalFileName { get; set; }
        public int Depth { get; set; }
        public bool IsGeneric { get; set; }
        public string ParentNodeId { get; set; }
    }

    public enum PdmPlanWarning
    {
        DuplicateItemCode,
        DuplicateFileName,
        GenericDisplayName
    }

    public sealed class PdmNormalizationPlan
    {
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public PdmPlanItem Root { get; set; }
        public IList<PdmPlanItem> Assemblies { get; } = new List<PdmPlanItem>();
        public IList<PdmPlanItem> Parts { get; } = new List<PdmPlanItem>();
        public IList<PdmPlanWarning> Warnings { get; } = new List<PdmPlanWarning>();

        public IEnumerable<PdmPlanItem> Items
        {
            get
            {
                foreach (var item in Assemblies) yield return item;
                foreach (var item in Parts) yield return item;
            }
        }
    }

    public sealed class PdmPackageManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public string RootNodeId { get; set; }
        public string RootItemCode { get; set; }
        public string RootFile { get; set; }
        public IEnumerable<PdmManifestItem> Items { get; set; } = new PdmManifestItem[0];
        public IEnumerable<PdmManifestBomEdge> Bom { get; set; } = new PdmManifestBomEdge[0];
        public IEnumerable<string> Warnings { get; set; } = new string[0];
    }

    public sealed class PdmManifestItem
    {
        public string NodeId { get; set; }
        public string ItemCode { get; set; }
        public string ItemType { get; set; }
        public string DisplayName { get; set; }
        public string SceneName { get; set; }
        public string FileName { get; set; }
        public string Revision { get; set; }
    }

    public sealed class PdmManifestBomEdge
    {
        public string ParentNodeId { get; set; }
        public string ChildNodeId { get; set; }
        public int FindNumber { get; set; }
        public decimal Quantity { get; set; }
        public string QuantityStatus { get; set; }
    }

    public enum PdmPackageValidationIssue
    {
        MissingRootFile,
        MissingFile,
        UnknownBomNode,
        DuplicateItemCode,
        DuplicateFileName,
        BomCycle
    }

    public sealed class PdmPackageValidationResult
    {
        public IList<PdmPackageValidationIssue> Issues { get; } = new List<PdmPackageValidationIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
    }
}
