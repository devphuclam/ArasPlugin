using System;
using System.Collections.Generic;

namespace IdeaCadConnector.Workspace
{
    public sealed class AnalyzeResult
    {
        public string RepositoryCode { get; set; }
        public string ProjectName { get; set; }
        public string PackageSourcePath { get; set; }
        public string CadSourcePath { get; set; }
        public string PolicyVersion { get; set; }
        public IReadOnlyList<AnalyzedStructureNode> StructureNodes { get; set; }
        public IReadOnlyList<AnalyzedCadFile> CadFiles { get; set; }
        public IReadOnlyList<AnalyzedDocumentFile> DocumentFiles { get; set; }
        public IReadOnlyList<AnalyzedIgnoredFile> IgnoredFiles { get; set; }
        public IReadOnlyList<AnalyzeWarning> Warnings { get; set; }
        public AnalyzeSummary Summary { get; set; }
    }

    public sealed class AnalyzedStructureNode
    {
        public string LogicalCode { get; set; }
        public string ParentLogicalCode { get; set; }
        public string DisplayName { get; set; }
        public string NodeType { get; set; }
        public int Quantity { get; set; }
        public string SourceDocumentPath { get; set; }
        public string PrimaryCadPath { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class AnalyzedCadFile
    {
        public string SourcePath { get; set; }
        public string RelativePath { get; set; }
        public string LogicalCode { get; set; }
        public string CadRole { get; set; }
        public string VersionToken { get; set; }
        public string Fingerprint { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class AnalyzedDocumentFile
    {
        public string SourcePath { get; set; }
        public string RelativePath { get; set; }
        public string LogicalCode { get; set; }
        public string DocumentRole { get; set; }
        public string LinkTargetType { get; set; }
        public string Fingerprint { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class AnalyzedIgnoredFile
    {
        public string SourcePath { get; set; }
        public string RelativePath { get; set; }
        public string Reason { get; set; }
    }

    public sealed class AnalyzeWarning
    {
        public string Source { get; set; }
        public string Message { get; set; }
        public bool BlocksPush { get; set; }
    }

    public sealed class AnalyzeSummary
    {
        public int TotalStructureNodes { get; set; }
        public int CadFileCount { get; set; }
        public int DocumentFileCount { get; set; }
        public int IgnoredFileCount { get; set; }
        public int WarningCount { get; set; }
        public int BlockingIssueCount { get; set; }
        public bool IsValid { get; set; }
    }

    public sealed class PushPreview
    {
        public string RepositoryCode { get; set; }
        public string ProjectName { get; set; }
        public string TargetBranch { get; set; }
        public string CommitMessage { get; set; }
        public IReadOnlyList<PartPreviewRow> Parts { get; set; }
        public IReadOnlyList<CadPreviewRow> Cads { get; set; }
        public IReadOnlyList<DocumentPreviewRow> Documents { get; set; }
        public IReadOnlyList<IgnoredPreviewRow> IgnoredFiles { get; set; }
        public IReadOnlyList<PreviewWarning> Warnings { get; set; }
        public PushReadiness Readiness { get; set; }
    }

    public sealed class PartPreviewRow
    {
        public string LogicalCode { get; set; }
        public string ParentLogicalCode { get; set; }
        public string PartNumber { get; set; }
        public string Name { get; set; }
        public string Classification { get; set; }
        public int Quantity { get; set; }
        public string Action { get; set; }
    }

    public sealed class CadPreviewRow
    {
        public string SourceFileName { get; set; }
        public string SourceFilePath { get; set; }
        public string LogicalCode { get; set; }
        public string CadNumber { get; set; }
        public string Classification { get; set; }
        public string Action { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class DocumentPreviewRow
    {
        public string SourceFileName { get; set; }
        public string LogicalCode { get; set; }
        public string DocumentNumber { get; set; }
        public string Classification { get; set; }
        public string LinkTargetType { get; set; }
        public string Action { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class IgnoredPreviewRow
    {
        public string SourceFileName { get; set; }
        public string Reason { get; set; }
    }

    public sealed class PreviewWarning
    {
        public string Source { get; set; }
        public string Message { get; set; }
        public bool BlocksPush { get; set; }
    }

    public sealed class PushReadiness
    {
        public bool CanPush { get; set; }
        public bool HasBlockingIssues { get; set; }
        public int BlockingIssueCount { get; set; }
        public string Summary { get; set; }
    }
}
