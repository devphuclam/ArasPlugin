using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Contracts
{
    public interface IPdmRepositoryClient : System.IDisposable
    {
        Task<PdmPushResult> PushAsync(PdmPushRequest request, CancellationToken ct);

        Task<PdmExistencePreview> PreviewExistenceAsync(PdmPushRequest request, CancellationToken ct);
    }

    public sealed class PdmExistencePreview
    {
        public IReadOnlyDictionary<string, bool> PartsByNumber { get; set; }
        public IReadOnlyDictionary<string, bool> CadsByNumber { get; set; }
        public IReadOnlyDictionary<string, bool> DocumentsByNumber { get; set; }
        public IReadOnlyDictionary<string, PdmBomExistenceInfo> BomByChildLogicalCode { get; set; }
    }

    public sealed class PdmBomExistenceInfo
    {
        public bool Exists { get; set; }
        public int? ExistingQuantity { get; set; }
    }

    public sealed class PdmPushRequest
    {
        public string RepositoryCode { get; set; }
        public string ProjectName { get; set; }
        public string TargetBranch { get; set; }
        public string CommitMessage { get; set; }
        public string PackageSourcePath { get; set; }
        public string CadSourcePath { get; set; }
        public IReadOnlyList<PdmPartRequest> Parts { get; set; }
        public IReadOnlyList<PdmCadRequest> Cads { get; set; }
        public IReadOnlyList<PdmDocumentRequest> Documents { get; set; }
    }

    public sealed class PdmPartRequest
    {
        public string LogicalCode { get; set; }
        public string ParentLogicalCode { get; set; }
        public string PartNumber { get; set; }
        public string Name { get; set; }
        public string Classification { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class PdmCadRequest
    {
        public string SourceFileName { get; set; }
        public string SourceFilePath { get; set; }
        public string LogicalCode { get; set; }
        public string CadNumber { get; set; }
        public string Classification { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class PdmDocumentRequest
    {
        public string SourceFileName { get; set; }
        public string LogicalCode { get; set; }
        public string DocumentNumber { get; set; }
        public string Classification { get; set; }
        public string LinkTargetType { get; set; }
        public string LinkedPartLogicalCode { get; set; }
    }

    public sealed class PdmPushResult
    {
        public bool Success { get; set; }
        public bool LiveDataUpdated { get; set; }
        public bool StagingOnly { get; set; }
        public string CommitId { get; set; }
        public IReadOnlyList<PdmItemResult> PartResults { get; set; }
        public IReadOnlyList<PdmItemResult> CadResults { get; set; }
        public IReadOnlyList<PdmItemResult> DocumentResults { get; set; }
        public string ErrorMessage { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public sealed class PdmItemResult
    {
        public string SourceKey { get; set; }
        public string ArasId { get; set; }
        public string ItemNumber { get; set; }
        public bool Success { get; set; }
        public string ActionTaken { get; set; }
        public string ErrorMessage { get; set; }
    }
}
