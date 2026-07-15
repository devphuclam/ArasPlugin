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

        Task<PdmCloneResult> CloneLatestToWorkspaceAsync(PdmCloneRequest request, CancellationToken ct);

        Task<string> FindItemIdByNumberAsync(string itemType, string itemNumber, CancellationToken ct);

        /// <summary>
        /// Creates a new major revision of a Released CAD and its linked Part.
        /// See <see cref="PdmReviseRequest"/> for the contract shape and expected AML sequence.
        ///
        /// CURRENTLY: Returns not-implemented result (<see cref="PdmReviseResult.Success"/> = false).
        /// The <see cref="HttpPdmRepositoryClient"/> implementation has a detailed comment block
        /// with the expected AML for the future server method.
        ///
        /// When the Aras server method is implemented, this endpoint creates a new working revision:
        ///   Released CAD + Part → new Part version + new CAD version (both in "Khoi tao" state).
        /// </summary>
        Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct);
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
        public string RelationshipId { get; set; }
    }

    public enum BomActionResult
    {
        Created,
        QuantityUpdated,
        Unchanged,
        InvalidParentChild,
        InvalidQuantity,
        Failed
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
        public string ExistingPartId { get; set; }
        public string ExistingPartConfigId { get; set; }
        public string ExistingPartRevision { get; set; }
        public string SourceKind { get; set; }
        public string LibraryEntryId { get; set; }
        public string RevisionPolicy { get; set; }
        public bool IsExternalReference { get; set; }
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
        public string RelativePath { get; set; }
        public string LogicalCode { get; set; }
        public string DocumentNumber { get; set; }
        public string Classification { get; set; }
        public string LinkTargetType { get; set; }
        public string LinkedPartLogicalCode { get; set; }
        public string SourceFilePath { get; set; }
        public string FileHash { get; set; }
        public long FileSize { get; set; }
    }

    public sealed class PdmBomPushResult
    {
        public string ParentLogicalCode { get; set; }
        public string ChildLogicalCode { get; set; }
        public string ParentPartId { get; set; }
        public string ChildPartId { get; set; }
        public int Quantity { get; set; }
        public bool Success { get; set; }
        public BomActionResult ActionTaken { get; set; }
        public string ErrorMessage { get; set; }
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
        public IReadOnlyList<PdmBomPushResult> BomResults { get; set; }
        public string ErrorMessage { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public sealed class PdmCloneRequest
    {
        public string RepositoryCode { get; set; }
        public string TargetFolder { get; set; }
        public string BranchName { get; set; }
    }

    public sealed class PdmCloneResult
    {
        public bool Success { get; set; }
        public string RepositoryCode { get; set; }
        public string RootPartId { get; set; }
        public string RootPartNumber { get; set; }
        public string ResolvedProjectFolder { get; set; }
        public string ResolvedCadFolder { get; set; }
        public string RootCadFilePath { get; set; }
        public int DownloadedCadFileCount { get; set; }
        public int PlaceholderDocumentCount { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
        public string ErrorMessage { get; set; }
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

    /// <summary>
    /// Request payload for creating a new revision (version) of a CAD and its linked Part.
    ///
    /// SERVER METHOD EXPECTED (not yet implemented):
    /// The future Aras server method must:
    ///   1. Version the Part (create new major revision, keep same Part Number).
    ///   2. Version the CAD (create new major revision, keep same CAD Number).
    ///   3. Set the new CAD lifecycle state to "Khoi tao" (Draft).
    ///   4. Link the new CAD to the new Part.
    ///   5. Return the new IDs and revision in <see cref="PdmReviseResult"/>.
    ///
    /// USAGE PRECONDITIONS:
    /// - Caller must verify <see cref="PdmRevisePreconditionResult.CanRevise"/> first.
    /// - The source CAD must be in "Released" state.
    /// - No active checkout lock may exist on the source CAD.
    /// - Both PartId and CadId must be known (pushed to Aras).
    /// </summary>
    public sealed class PdmReviseRequest
    {
        /// <summary>Aras ID of the current Part to version. Required.</summary>
        public string PartId { get; set; }
        /// <summary>Aras ID of the current Released CAD to version. Required.</summary>
        public string CadId { get; set; }
        /// <summary>Part Number for display/logging. Informational only.</summary>
        public string PartNumber { get; set; }
        /// <summary>CAD Number for display/logging. Informational only.</summary>
        public string CadNumber { get; set; }
        /// <summary>Optional human-readable reason for the revision.</summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Response from the future Aras server-side revise method.
    ///
    /// On success (<see cref="Success"/> = true):
    ///   <see cref="NewPartId"/>, <see cref="NewCadId"/>, <see cref="NewRevision"/>,
    ///   and <see cref="NewLifecycleState"/> are populated.
    ///   The app must call <see cref="IRevisionService.CheckPreconditionsAsync"/> again
    ///   after receiving a successful result.
    ///
    /// On failure (<see cref="Success"/> = false):
    ///   <see cref="ErrorMessage"/> describes why the revision was not created.
    ///   No new records were created on Aras.
    /// </summary>
    public sealed class PdmReviseResult
    {
        /// <summary>True if a new revision was created on Aras.</summary>
        public bool Success { get; set; }
        /// <summary>Aras ID of the newly created Part version. Set only on success.</summary>
        public string NewPartId { get; set; }
        /// <summary>Aras ID of the newly created CAD version. Set only on success.</summary>
        public string NewCadId { get; set; }
        /// <summary>The new major_rev assigned by Aras (e.g. "B", "C"). Set only on success.</summary>
        public string NewRevision { get; set; }
        /// <summary>The lifecycle state of the new CAD (expected: "Khoi tao"). Set only on success.</summary>
        public string NewLifecycleState { get; set; }
        /// <summary>Error message when Success is false. Describes why the server rejected the request.</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Precondition check result for whether a new revision can be started.
    /// Returned by <see cref="IRevisionService.CheckPreconditionsAsync"/>.
    ///
    /// <see cref="CanRevise"/> is true only when <see cref="BlockingReasons"/> is empty.
    /// <see cref="Warnings"/> are informational and do not block the revision.
    ///
    /// The ViewModel uses this to enable/disable the "Start New Revision" button
    /// and to display readiness text to the user.
    /// </summary>
    public sealed class PdmRevisePreconditionResult
    {
        /// <summary>True when no blocking reasons exist and revision can proceed.</summary>
        public bool CanRevise { get; set; }
        /// <summary>Reasons that block revision. When non-empty, CanRevise must be false.</summary>
        public IReadOnlyList<string> BlockingReasons { get; set; }
        /// <summary>Informational warnings that do not block revision.</summary>
        public IReadOnlyList<string> Warnings { get; set; }
    }
}
