using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Library
{
    public enum LibraryType
    {
        Personal,
        Team,
        Standard
    }

    public enum LibraryEntryStatus
    {
        Draft,
        PendingReview,
        Published,
        Deprecated
    }

    public enum LibraryRevisionPolicy
    {
        Pinned,
        LatestReleased,
        LatestCurrent
    }

    public enum LibrarySourceKind
    {
        Generated,
        LibraryReference
    }

    public enum LibraryVisibilityFilter
    {
        Active,
        Archived,
        All
    }
}

namespace IdeaCadConnector.Core.Dto.Library
{
    using IdeaCadConnector.Core.Errors;
    using IdeaCadConnector.Core.Library;

    public sealed class PartLibrarySummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public LibraryType LibraryType { get; set; }
        public int ItemCount { get; set; }
        public bool CanContribute { get; set; }
        public bool IsPublic { get; set; }
        public string Status { get; set; }
        public string DefaultRevisionPolicy { get; set; }
    }

    public sealed class CreatePartLibraryRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public LibraryType LibraryType { get; set; }
        public string DefaultRevisionPolicy { get; set; }
        public bool IsPublic { get; set; }
    }

    public sealed class UpdatePartLibraryRequest
    {
        public string LibraryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public LibraryType LibraryType { get; set; }
        public string DefaultRevisionPolicy { get; set; }
        public bool IsPublic { get; set; }
    }

    public sealed class LibraryMutationResult
    {
        public bool Success { get; set; }
        public string LibraryId { get; set; }
        public string ErrorMessage { get; set; }
        public ArasErrorCode? ErrorCode { get; set; }
    }

    public sealed class PartPickerSearchRequest
    {
        public string Keyword { get; set; }
        public string LifecycleState { get; set; }
        public string PartType { get; set; }
        public string MajorRev { get; set; }
        public bool? CurrentOnly { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string SortBy { get; set; }
        public string SortDirection { get; set; }
    }

    public sealed class PartPickerSearchResponse
    {
        public IReadOnlyList<PartPickerSearchResultItem> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class PartPickerSearchResultItem
    {
        public string PartId { get; set; }
        public string ConfigId { get; set; }
        public string PartNumber { get; set; }
        public string Name { get; set; }
        public string PartType { get; set; }
        public string MajorRev { get; set; }
        public string Generation { get; set; }
        public string LifecycleState { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsReleased { get; set; }
        public string CadStatus { get; set; }
        public string ModifiedOn { get; set; }
    }

    public sealed class PartPreview
    {
        public string ConfigId { get; set; }
        public string PartId { get; set; }
        public string Revision { get; set; }
        public string LifecycleState { get; set; }
        public string Generation { get; set; }
        public string CadStatus { get; set; }
        public bool IsEligibleForReuse { get; set; }
        public string IneligibilityReason { get; set; }
    }

    public sealed class AddSelectedPartToLibraryRequest
    {
        public string LibraryId { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public string Category { get; set; }
        public string Tags { get; set; }
        public string Note { get; set; }
        public string SourceProject { get; set; }
        public string SourceCommit { get; set; }
    }

    public sealed class MoveLibraryEntryRequest
    {
        public string EntryId { get; set; }
        public string TargetLibraryId { get; set; }
    }

    public sealed class MoveLibraryEntryResult
    {
        public bool Success { get; set; }
        public string EntryId { get; set; }
        public string SourceLibraryId { get; set; }
        public string TargetLibraryId { get; set; }
        public string PreservedEntryStatus { get; set; }
        public string PreservedLifecycleState { get; set; }
        public ArasErrorCode? ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class PartRevisionHistoryRequest
    {
        public string PartConfigId { get; set; }
        public string PartId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public sealed class PartRevisionHistoryResponse
    {
        public IReadOnlyList<PartRevisionHistoryItem> Items { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public sealed class PartRevisionHistoryItem
    {
        public string PartId { get; set; }
        public string ConfigId { get; set; }
        public string PartNumber { get; set; }
        public string Name { get; set; }
        public string PartType { get; set; }
        public string MajorRev { get; set; }
        public string Generation { get; set; }
        public string LifecycleState { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsReleased { get; set; }
        public bool IsObsolete { get; set; }
        public string CadStatus { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? CreatedOn { get; set; }
        public bool CanPin { get; set; }
        public string CannotPinReason { get; set; }
    }

    public sealed class DuplicateEntryCheckResult
    {
        public bool IsDuplicate { get; set; }
        public string ExistingEntryId { get; set; }
        public LibraryEntryStatus ExistingEntryStatus { get; set; }
    }

    public sealed class PartLibrarySearchRequest
    {
        public string LibraryId { get; set; }
        public string SearchText { get; set; }
        public string TypeFilter { get; set; }
        public string StateFilter { get; set; }
        public string RevisionFilter { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public sealed class PartLibrarySearchResponse
    {
        public IReadOnlyList<PartLibraryEntrySummary> Entries { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class PartLibraryEntrySummary
    {
        public string EntryId { get; set; }
        public string LibraryId { get; set; }
        public string LibraryName { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string PartType { get; set; }
        public string Revision { get; set; }
        public string LifecycleState { get; set; }
        public string EntryLifecycleState { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public LibraryEntryStatus EntryStatus { get; set; }
        public string CadStatus { get; set; }
        public int UsageCount { get; set; }
        public bool HasNewerReleasedRevision { get; set; }
        public bool IsDeprecated { get; set; }
        public bool ResolutionFailed { get; set; }
        public string ResolutionError { get; set; }
        public bool CanAddToProject { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? LastUsedOn { get; set; }
    }

    public sealed class PartLibraryEntryDetails
    {
        public string EntryId { get; set; }
        public string LibraryId { get; set; }
        public string LibraryName { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string PartType { get; set; }
        public string Revision { get; set; }
        public string LifecycleState { get; set; }
        public string EntryLifecycleState { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public LibraryEntryStatus EntryStatus { get; set; }
        public string CadStatus { get; set; }
        public string PrimaryCadId { get; set; }
        public string PrimaryCadFileName { get; set; }
        public string PrimaryCadState { get; set; }
        public string LockedBy { get; set; }
        public int UsageCount { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Tags { get; set; }
        public bool HasNewerReleasedRevision { get; set; }
        public bool ResolutionFailed { get; set; }
        public string ResolutionError { get; set; }
        public bool CanAddToProject { get; set; }
    }

    public sealed class AddPartToLibraryRequest
    {
        public string LibraryId { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string Category { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public string Tags { get; set; }
        public string Note { get; set; }
        public string SourceProject { get; set; }
        public string SourceCommit { get; set; }
    }

    public sealed class AddPartToLibraryResult
    {
        public bool Success { get; set; }
        public string EntryId { get; set; }
        public bool AlreadyExists { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class ResolveLibraryPartResult
    {
        public string EntryId { get; set; }
        public string ResolvedPartId { get; set; }
        public string ResolvedPartConfigId { get; set; }
        public string ResolvedRevision { get; set; }
        public string LifecycleState { get; set; }
        public string CadStatus { get; set; }
        public bool HasNewerReleasedRevision { get; set; }
    }

    public sealed class LibraryUsageRequest
    {
        public string LibraryEntryId { get; set; }
        public string PartId { get; set; }
        public string ProjectCode { get; set; }
        public string ParentPartId { get; set; }
        public int Quantity { get; set; }
        public string UsedBy { get; set; }
        public string CommitId { get; set; }
        public string ActionType { get; set; }
        public string IdempotencyKey { get; set; }
    }

    public sealed class RecordLibraryUsageResult
    {
        public bool Success { get; set; }
        public bool AlreadyExists { get; set; }
        public bool TrackingUnavailable { get; set; }
        public string UsageId { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsedOn { get; set; }
        public string IdempotencyKey { get; set; }
        public string WarningMessage { get; set; }
    }

    public sealed class UpdateLibraryRevisionPolicyRequest
    {
        public string EntryId { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public string PinnedPartId { get; set; }
    }

    public sealed class UpdateLibraryRevisionPolicyResult
    {
        public bool Success { get; set; }
        public string EntryId { get; set; }
        public LibraryRevisionPolicy RevisionPolicy { get; set; }
        public string ResolvedPartId { get; set; }
        public string ResolvedPartConfigId { get; set; }
        public string ResolvedRevision { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum WhereUsedSource
    {
        Bom,
        LibraryUsage
    }
}

namespace IdeaCadConnector.Core.Contracts
{
    using IdeaCadConnector.Core.Dto.Library;
    using IdeaCadConnector.Core.Errors;
    using IdeaCadConnector.Core.Library;

    public interface IPartLibraryClient : IDisposable
    {
        Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(
            LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active,
            CancellationToken cancellationToken = default);

        Task<LibraryMutationResult> CreateLibraryAsync(
            CreatePartLibraryRequest request,
            CancellationToken cancellationToken);

        Task<LibraryMutationResult> UpdateLibraryAsync(
            UpdatePartLibraryRequest request,
            CancellationToken cancellationToken);

        Task<LibraryMutationResult> ArchiveLibraryAsync(
            string libraryId,
            CancellationToken cancellationToken);

        Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(
            MoveLibraryEntryRequest request,
            CancellationToken cancellationToken);

        Task<PartPickerSearchResponse> SearchPartsAsync(
            PartPickerSearchRequest request,
            CancellationToken cancellationToken);

        Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(
            PartRevisionHistoryRequest request,
            CancellationToken cancellationToken);

        Task<PartPreview> GetPartPreviewAsync(
            string partId,
            CancellationToken cancellationToken);

        Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(
            string libraryId,
            string partConfigId,
            CancellationToken cancellationToken);

        Task<PartLibrarySearchResponse> SearchEntriesAsync(
            PartLibrarySearchRequest request,
            CancellationToken cancellationToken);

        Task<PartLibraryEntryDetails> GetEntryAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<AddPartToLibraryResult> AddPartAsync(
            AddPartToLibraryRequest request,
            CancellationToken cancellationToken);

        Task RemoveEntryAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task MoveEntryAsync(
            string entryId,
            string targetLibraryId,
            CancellationToken cancellationToken);

        Task<ResolveLibraryPartResult> ResolvePartAsync(
            string entryId,
            LibraryRevisionPolicy policy,
            CancellationToken cancellationToken);

        Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(
            UpdateLibraryRevisionPolicyRequest request,
            CancellationToken cancellationToken);

        Task PublishEntryAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task DeprecateEntryAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(
            string partId,
            CancellationToken cancellationToken);

        Task<RecordLibraryUsageResult> RecordUsageAsync(
            LibraryUsageRequest request,
            CancellationToken cancellationToken);

        Task<LibraryEntryCadDetails> GetCadDetailsAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<LibraryEntryBomDetails> GetBomDetailsAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<LibraryEntryRevisionDetails> GetRevisionDetailsAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<LibraryEntryWhereUsedDetails> GetWhereUsedDetailsAsync(
            string entryId,
            CancellationToken cancellationToken);

        Task<LibraryEntryDetailBundle> GetDetailBundleAsync(
            string entryId,
            CancellationToken cancellationToken);
    }
}
