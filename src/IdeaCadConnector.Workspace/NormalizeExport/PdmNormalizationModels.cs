using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public static class PdmFeatureFlags
    {
        public const string EnablePdmNormalizeExport = "EnablePdmNormalizeExport";

        public static bool IsNormalizeExportEnabled(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class NormalizeExportEdit
    {
        public PdmSourceNode SourceNode { get; set; }
        public string EditKey { get; set; }
        public string OccurrencePath { get; set; }
        public string NodeId { get; set; }
        public string ItemCode { get; set; }
        public string DisplayName { get; set; }
        public bool GenericNameConfirmed { get; set; }
    }

    public sealed class NormalizeExportDialogResult
    {
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public string OutputFolder { get; set; }
        public IEnumerable<NormalizeExportEdit> Edits { get; set; } = new NormalizeExportEdit[0];
    }

    public sealed class PdmNormalizationLimits
    {
        public int MaxDepth { get; set; } = 128;
        public int MaxNodeCount { get; set; } = 100000;
    }

    public enum PdmPreflightIssue
    {
        InvalidProjectCode,
        InvalidRevision,
        EmptyItemCode,
        EmptyDisplayName,
        DuplicateItemCode,
        DuplicateFileName,
        DuplicateNodeId,
        GenericNameNotConfirmed,
        InvalidOutputFolder
        ,InvalidNodeId, InvalidSceneName, InvalidCanonicalFileName, DuplicateOccurrencePath,
        OutputSourceOverlap, PathTraversal, InvalidOutputPath
    }

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
        public string OccurrencePath { get; set; }
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
        public string EditKey { get; set; }
        public string OccurrencePath { get; set; }
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
        public bool SourceWasGeneric { get; set; }
        public bool GenericNameConfirmed { get; set; }
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
        public int SchemaVersion { get; set; } = 2;
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public string RootNodeId { get; set; }
        public string RootItemCode { get; set; }
        public string RootFile { get; set; }
        public string RootOccurrenceId { get; set; }
        public IEnumerable<PdmManifestDefinition> Definitions { get; set; } = new PdmManifestDefinition[0];
        public IEnumerable<PdmManifestOccurrence> Occurrences { get; set; } = new PdmManifestOccurrence[0];
        [JsonProperty("legacyItemsProjection")]
        public IEnumerable<PdmManifestItem> Items { get; set; } = new PdmManifestItem[0];
        [JsonIgnore]
        public IEnumerable<PdmManifestBomEdge> Bom { get; set; } = new PdmManifestBomEdge[0];
        [JsonProperty("bom")]
        public IEnumerable<PdmManifestBomV2> BomV2 { get; set; } = new PdmManifestBomV2[0];
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
        BomCycle,
        InvalidSchemaVersion,
        InvalidManifestPath,
        DuplicateManifestId,
        DuplicateOccurrencePath,
        UnknownOccurrence,
        InvalidQuantity,
        MissingDefinition
        ,RootOccurrenceInvalid, OrphanDefinition, ParentPathMismatch, OccurrenceCycle,
        MissingDefinitionFile, OrphanFile
    }

    public sealed class PdmPackageValidationResult
    {
        public IList<PdmPackageValidationIssue> Issues { get; } = new List<PdmPackageValidationIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
    }

    public sealed class PdmManifestBomV2
    {
        public string ParentOccurrenceId { get; set; }
        public string ChildDefinitionId { get; set; }
        public decimal Quantity { get; set; }
        public string QuantityStatus { get; set; }
    }

    public sealed class PdmNormalizeExportException : Exception
    {
        public string Code { get; private set; }
        public string UserMessage { get; private set; }
        public string InternalDetails { get; private set; }
        public PdmNormalizeExportException(string code, string userMessage, string internalDetails = null, Exception inner = null)
            : base(internalDetails, inner) { Code = code; UserMessage = userMessage; InternalDetails = internalDetails; }
    }

    public sealed class PdmManifestDefinition
    {
        public string DefinitionId { get; set; }
        public string NodeId { get; set; }
        public string ItemCode { get; set; }
        public string ItemType { get; set; }
        public string DisplayName { get; set; }
        public string Revision { get; set; }
        public string FileName { get; set; }
    }

    public sealed class PdmManifestOccurrence
    {
        public string OccurrenceId { get; set; }
        public string OccurrencePath { get; set; }
        public string ParentOccurrenceId { get; set; }
        public string DefinitionId { get; set; }
        public int FindNumber { get; set; }
    }

    public sealed class PdmNormalizationPreflightValidator
    {
        public IList<PdmPreflightIssue> Validate(PdmNormalizationPlan plan, string outputFolder)
        {
            var issues = new List<PdmPreflightIssue>();
            if (plan == null || string.IsNullOrWhiteSpace(plan.ProjectCode)) issues.Add(PdmPreflightIssue.InvalidProjectCode);
            else { try { PdmNameNormalizer.NormalizeProjectCode(plan.ProjectCode); } catch { issues.Add(PdmPreflightIssue.InvalidProjectCode); } }
            if (plan == null || string.IsNullOrWhiteSpace(plan.Revision) || plan.Revision.Length > 20) issues.Add(PdmPreflightIssue.InvalidRevision);
            if (string.IsNullOrWhiteSpace(outputFolder) || !Path.IsPathRooted(outputFolder) || !Directory.Exists(outputFolder)) issues.Add(PdmPreflightIssue.InvalidOutputFolder);
            if (plan == null || plan.Root == null) return issues;
            var items = plan.Items.ToList();
            if (items.Any(i => string.IsNullOrWhiteSpace(i.ItemCode) || !PdmNameNormalizer.IsCanonicalCode(i.ItemCode))) issues.Add(PdmPreflightIssue.EmptyItemCode);
            if (items.Any(i => string.IsNullOrWhiteSpace(i.DisplayName))) issues.Add(PdmPreflightIssue.EmptyDisplayName);
            if (items.Any(i => i.SourceWasGeneric && !i.GenericNameConfirmed)) issues.Add(PdmPreflightIssue.GenericNameNotConfirmed);
            if (items.GroupBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) issues.Add(PdmPreflightIssue.DuplicateItemCode);
            if (items.GroupBy(i => i.CanonicalFileName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) issues.Add(PdmPreflightIssue.DuplicateFileName);
            var ids = items.Concat(new[] { plan.Root }).Select(i => i.NodeId).ToList();
            if (ids.Any(id => string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out _)) || ids.Count != ids.Distinct(StringComparer.OrdinalIgnoreCase).Count()) issues.Add(PdmPreflightIssue.DuplicateNodeId);
            if (items.Select(i => i.OccurrencePath).Concat(new[] { plan.Root.OccurrencePath }).Any(string.IsNullOrWhiteSpace) ||
                items.Select(i => i.OccurrencePath).Concat(new[] { plan.Root.OccurrencePath }).Distinct(StringComparer.Ordinal).Count() != items.Count() + 1)
                issues.Add(PdmPreflightIssue.DuplicateOccurrencePath);
            if (items.Any(i => string.IsNullOrWhiteSpace(i.SceneName) || i.SceneName.IndexOf("..", StringComparison.Ordinal) >= 0)) issues.Add(PdmPreflightIssue.InvalidSceneName);
            if (items.Any(i => string.IsNullOrWhiteSpace(i.CanonicalFileName) || Path.IsPathRooted(i.CanonicalFileName) || i.CanonicalFileName.Contains(".."))) issues.Add(PdmPreflightIssue.InvalidCanonicalFileName);
            return issues;
        }
    }
}
