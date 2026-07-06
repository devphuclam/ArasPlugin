using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    public sealed class HttpPartLibraryClient : IPartLibraryClient
    {
        private readonly ArasClientOptions _options;
        private readonly ILogger<HttpPartLibraryClient> _logger;
        private ArasHttpClient _http;
        private IArasAmlClient _aml;
        private string _database;
        private bool? _schemaAvailable;
        private bool _disposed;

        public HttpPartLibraryClient(ArasClientOptions options, ILogger<HttpPartLibraryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpPartLibraryClient>.Instance;
        }

        internal HttpPartLibraryClient(
            ArasClientOptions options,
            IArasAmlClient amlClient,
            ILogger<HttpPartLibraryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _aml = amlClient ?? throw new ArgumentNullException(nameof(amlClient));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpPartLibraryClient>.Instance;
        }

        public void SetSession(string accessToken, string tokenType, string database)
        {
            if (_http == null)
                _http = new ArasHttpClient(_options.BaseUri, _options.Timeout);

            _http.SetBearerToken(accessToken, tokenType ?? "Bearer");
            _database = database ?? _options.Database;
            _aml = new ArasAmlClient(_http, _database);
            _schemaAvailable = null;
        }

        public async Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(
            LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var statusFilter = visibilityFilter switch
            {
                LibraryVisibilityFilter.Active => "<status>" + PartLibrarySchemaNames.LibraryStatusActive + "</status>",
                LibraryVisibilityFilter.Archived => "<status>" + PartLibrarySchemaNames.LibraryStatusArchived + "</status>",
                _ => string.Empty
            };

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.LibraryItemType + "\" action=\"get\" " +
                "select=\"id,name,description,library_type,status,default_revision_policy,is_public\">" +
                statusFilter +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                PartLibrarySchemaNames.LibraryItemType,
                null,
                cancellationToken).ConfigureAwait(false);

            var libraries = new List<PartLibrarySummary>();
            foreach (var item in EnumerateItems(result))
            {
                var id = item["id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var name = item["name"]?.Value<string>();
                var itemCount = await CountEntriesAsync(id, cancellationToken).ConfigureAwait(false);

                libraries.Add(new PartLibrarySummary
                {
                    Id = id,
                    Name = name,
                    Description = item["description"]?.Value<string>(),
                    LibraryType = ParseLibraryType(item["library_type"]?.Value<string>()),
                    ItemCount = itemCount,
                    CanContribute = IsActiveLibrary(item["status"]?.Value<string>()),
                    IsPublic = ParseBoolean(item["is_public"]?.Value<string>()),
                    Status = item["status"]?.Value<string>(),
                    DefaultRevisionPolicy = item["default_revision_policy"]?.Value<string>()
                });
            }

            return libraries
                .OrderBy(l => l.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
        {
            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            request = request ?? new PartLibrarySearchRequest();
            var allEntries = await LoadEntrySummariesAsync(request.LibraryId, cancellationToken).ConfigureAwait(false);

            IEnumerable<PartLibraryEntrySummary> query = allEntries;
            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                query = query.Where(entry =>
                    Contains(entry.PartNumber, request.SearchText) ||
                    Contains(entry.PartName, request.SearchText) ||
                    Contains(entry.LibraryName, request.SearchText));
            }

            if (!string.IsNullOrWhiteSpace(request.TypeFilter))
                query = query.Where(entry => string.Equals(entry.PartType, request.TypeFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.StateFilter))
                query = query.Where(entry =>
                    string.Equals(entry.LifecycleState, request.StateFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.RevisionFilter))
                query = query.Where(entry => string.Equals(entry.RevisionPolicy.ToString(), NormalizePolicyFilter(request.RevisionFilter), StringComparison.OrdinalIgnoreCase));

            var ordered = query
                .OrderBy(entry => entry.PartNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var page = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PartLibrarySearchResponse
            {
                Entries = page,
                TotalCount = ordered.Count,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entryItem = await GetEntryRelationshipAsync(entryId, cancellationToken).ConfigureAwait(false);
            var usageSnapshot = await LoadUsageCountsAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await MapEntryDetailsAsync(entryItem, usageSnapshot, cancellationToken).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (IsEntryResolutionFailure(ex))
            {
                var summary = await CreateDiagnosticSummaryAsync(entryItem, ex, usageSnapshot, cancellationToken).ConfigureAwait(false);
                return new PartLibraryEntryDetails
                {
                    EntryId = summary.EntryId,
                    LibraryId = summary.LibraryId,
                    LibraryName = summary.LibraryName,
                    PartId = summary.PartId,
                    PartConfigId = summary.PartConfigId,
                    PartNumber = summary.PartNumber,
                    PartName = summary.PartName,
                    PartType = summary.PartType,
                    Revision = summary.Revision,
                    LifecycleState = summary.LifecycleState,
                    EntryLifecycleState = summary.EntryLifecycleState,
                    RevisionPolicy = summary.RevisionPolicy,
                    EntryStatus = summary.EntryStatus,
                    CadStatus = summary.CadStatus,
                    UsageCount = summary.UsageCount,
                    Description = entryItem["note"]?.Value<string>() ?? summary.PartName,
                    Category = entryItem["category"]?.Value<string>(),
                    Tags = entryItem["tags"]?.Value<string>(),
                    HasNewerReleasedRevision = false,
                    ResolutionFailed = true,
                    ResolutionError = summary.ResolutionError,
                    CanAddToProject = false
                };
            }
        }

        public async Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.LibraryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "LibraryId is required.");
            if (string.IsNullOrWhiteSpace(request.PartId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "PartId is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            // 1. Check Library is not Archived (D-03)
            var library = await GetLibraryAsync(request.LibraryId, cancellationToken).ConfigureAwait(false);
            var libraryStatus = library["status"]?.Value<string>();
            if (string.Equals(libraryStatus, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase))
            {
                return new AddPartToLibraryResult
                {
                    Success = false,
                    ErrorMessage = "Cannot add Parts to an archived Library."
                };
            }

            // 2. Get Part and config_id
            var partItem = await GetPartAsync(request.PartId, cancellationToken).ConfigureAwait(false);
            var partConfigId = partItem["config_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(partConfigId))
            {
                return new AddPartToLibraryResult
                {
                    Success = false,
                    ErrorMessage = "Selected Part does not have a readable config_id."
                };
            }

            // 3. Pre-resolve based on RevisionPolicy (Fix 7)
            string resolvedPartId = null;
            string resolvedRevision = null;
            if (request.RevisionPolicy == LibraryRevisionPolicy.LatestReleased)
            {
                var resolved = await ResolveLatestReleasedPartStrictAsync(partConfigId, request.PartId, cancellationToken).ConfigureAwait(false);
                resolvedPartId = resolved["id"]?.Value<string>();
                resolvedRevision = resolved["major_rev"]?.Value<string>();
            }
            else if (request.RevisionPolicy == LibraryRevisionPolicy.LatestCurrent)
            {
                var resolved = await ResolveCurrentPartStrictAsync(partConfigId, request.PartId, cancellationToken).ConfigureAwait(false);
                resolvedPartId = resolved["id"]?.Value<string>();
                resolvedRevision = resolved["major_rev"]?.Value<string>();
            }
            else if (request.RevisionPolicy == LibraryRevisionPolicy.Pinned)
            {
                var resolved = await GetPartAsync(request.PartId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(resolved["config_id"]?.Value<string>()))
                {
                    return new AddPartToLibraryResult
                    {
                        Success = false,
                        ErrorMessage = "Selected Part does not have a readable config_id for Pinned policy."
                    };
                }
                if (string.IsNullOrWhiteSpace(resolved["major_rev"]?.Value<string>()))
                {
                    return new AddPartToLibraryResult
                    {
                        Success = false,
                        ErrorMessage = "Selected Part does not have a readable major_rev for Pinned policy."
                    };
                }
                resolvedPartId = resolved["id"]?.Value<string>();
                resolvedRevision = resolved["major_rev"]?.Value<string>();
            }

            // 4. Check D-02 active duplicate (LibraryId + part_config_id only)
            var duplicateId = await FindDuplicateEntryIdD02Async(
                request.LibraryId,
                partConfigId,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(duplicateId))
            {
                return new AddPartToLibraryResult
                {
                    Success = true,
                    EntryId = duplicateId,
                    AlreadyExists = true
                };
            }

            // 5. Try server Method (if available)
            var serverMethodResult = await TryAddPartViaServerMethodAsync(
                request,
                partConfigId,
                cancellationToken).ConfigureAwait(false);
            if (serverMethodResult != null)
            {
                return serverMethodResult;
            }

            // 6. Direct AML fallback
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"add\">" +
                "<source_id>" + Escape(request.LibraryId) + "</source_id>" +
                "<related_id>" + Escape(resolvedPartId ?? request.PartId) + "</related_id>" +
                "<part_config_id>" + Escape(partConfigId) + "</part_config_id>" +
                "<revision_policy>" + Escape(request.RevisionPolicy.ToString()) + "</revision_policy>" +
                "<entry_status>" + PartLibrarySchemaNames.EntryStatusDraft + "</entry_status>" +
                "<category>" + Escape(request.Category) + "</category>" +
                "<tags>" + Escape(request.Tags) + "</tags>" +
                "<note>" + Escape(request.Note) + "</note>" +
                "<source_project>" + Escape(request.SourceProject) + "</source_project>" +
                "<source_commit>" + Escape(request.SourceCommit) + "</source_commit>";

            if (request.RevisionPolicy == LibraryRevisionPolicy.Pinned)
            {
                aml += "<pinned_part_id>" + Escape(resolvedPartId ?? request.PartId) + "</pinned_part_id>" +
                       "<pinned_revision>" + Escape(resolvedRevision ?? string.Empty) + "</pinned_revision>";
            }

            aml += "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "add",
                PartLibrarySchemaNames.EntryRelationshipType,
                null,
                cancellationToken).ConfigureAwait(false);

            return new AddPartToLibraryResult
            {
                Success = true,
                EntryId = result["id"]?.Value<string>()
            };
        }

        public async Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);
            await _aml.ApplyItemAsync(
                PartLibrarySchemaNames.EntryRelationshipType,
                entryId,
                "delete",
                null,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken)
        {
            var result = await MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest
                {
                    EntryId = entryId,
                    TargetLibraryId = targetLibraryId
                },
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                throw new ArasOperationException(
                    result.ErrorCode ?? ArasErrorCode.UnexpectedServerError,
                    result.ErrorMessage ?? "Move Entry failed.");
            }
        }

        public async Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(
            MoveLibraryEntryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.EntryId))
                return new MoveLibraryEntryResult
                {
                    Success = false,
                    ErrorCode = ArasErrorCode.ValidationFailed,
                    ErrorMessage = "EntryId is required."
                };
            if (string.IsNullOrWhiteSpace(request.TargetLibraryId))
                return new MoveLibraryEntryResult
                {
                    Success = false,
                    EntryId = request.EntryId,
                    ErrorCode = ArasErrorCode.ValidationFailed,
                    ErrorMessage = "TargetLibraryId is required."
                };

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var sourceEntry = await GetEntryRelationshipAsync(request.EntryId, cancellationToken).ConfigureAwait(false);
                var sourceEntryId = sourceEntry["id"]?.Value<string>();
                var sourceLibraryId = sourceEntry["source_id"]?.Value<string>();
                var partConfigId = sourceEntry["part_config_id"]?.Value<string>();
                var revisionPolicy = sourceEntry["revision_policy"]?.Value<string>();
                var pinnedPartId = sourceEntry["pinned_part_id"]?.Value<string>();
                var pinnedRevision = sourceEntry["pinned_revision"]?.Value<string>();
                var entryStatus = sourceEntry["entry_status"]?.Value<string>() ?? sourceEntry["state"]?.Value<string>();
                var lifecycleState = sourceEntry["state"]?.Value<string>();
                var category = sourceEntry["category"]?.Value<string>();
                var tags = sourceEntry["tags"]?.Value<string>();
                var note = sourceEntry["note"]?.Value<string>();
                var sourceProject = sourceEntry["source_project"]?.Value<string>();
                var sourceCommit = sourceEntry["source_commit"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(sourceEntryId))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = request.EntryId,
                        ErrorCode = ArasErrorCode.PartNotFound,
                        ErrorMessage = "Entry was not found."
                    };
                }

                if (string.IsNullOrWhiteSpace(sourceLibraryId))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Source Library is missing on the Entry."
                    };
                }

                if (string.IsNullOrWhiteSpace(partConfigId))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Entry is missing part_config_id."
                    };
                }

                if (string.IsNullOrWhiteSpace(entryStatus) || string.IsNullOrWhiteSpace(lifecycleState))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Entry lifecycle state cannot be preserved safely."
                    };
                }

                if (string.Equals(sourceLibraryId, request.TargetLibraryId, StringComparison.OrdinalIgnoreCase))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = true,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        PreservedEntryStatus = entryStatus,
                        PreservedLifecycleState = lifecycleState
                    };
                }

                var targetLibrary = await GetLibraryAsync(request.TargetLibraryId, cancellationToken).ConfigureAwait(false);
                var targetLibraryStatus = targetLibrary["status"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(targetLibrary["id"]?.Value<string>()))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        ErrorCode = ArasErrorCode.PartNotFound,
                        ErrorMessage = "Target Library was not found."
                    };
                }

                if (string.Equals(targetLibraryStatus, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Target Library is archived."
                    };
                }

                var sourceLibrary = await GetLibraryAsync(sourceLibraryId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(sourceLibrary["id"]?.Value<string>()))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        ErrorCode = ArasErrorCode.PartNotFound,
                        ErrorMessage = "Source Library was not found."
                    };
                }

                var duplicateId = await FindDuplicateEntryIdD02Async(request.TargetLibraryId, partConfigId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(duplicateId))
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Target Library already contains an active Entry for the same part_config_id."
                    };
                }

                var editAml =
                    "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"edit\" id=\"" + Escape(sourceEntryId) + "\">" +
                    "<source_id>" + Escape(request.TargetLibraryId) + "</source_id>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(
                    editAml,
                    "edit",
                    PartLibrarySchemaNames.EntryRelationshipType,
                    sourceEntryId,
                    cancellationToken).ConfigureAwait(false);

                var refreshed = await GetEntryRelationshipAsync(sourceEntryId, cancellationToken).ConfigureAwait(false);
                var refreshedSourceId = refreshed["source_id"]?.Value<string>();
                var refreshedConfigId = refreshed["part_config_id"]?.Value<string>();
                var refreshedRevisionPolicy = refreshed["revision_policy"]?.Value<string>();
                var refreshedPinnedPartId = refreshed["pinned_part_id"]?.Value<string>();
                var refreshedPinnedRevision = refreshed["pinned_revision"]?.Value<string>();
                var refreshedEntryStatus = refreshed["entry_status"]?.Value<string>() ?? refreshed["state"]?.Value<string>();
                var refreshedLifecycleState = refreshed["state"]?.Value<string>();
                var refreshedCategory = refreshed["category"]?.Value<string>();
                var refreshedTags = refreshed["tags"]?.Value<string>();
                var refreshedNote = refreshed["note"]?.Value<string>();
                var refreshedSourceProject = refreshed["source_project"]?.Value<string>();
                var refreshedSourceCommit = refreshed["source_commit"]?.Value<string>();

                var preserved =
                    string.Equals(refreshedSourceId, request.TargetLibraryId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedConfigId, partConfigId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedRevisionPolicy, revisionPolicy, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedPinnedPartId, pinnedPartId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedPinnedRevision, pinnedRevision, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedEntryStatus, entryStatus, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedLifecycleState, lifecycleState, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedCategory, category, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedTags, tags, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedNote, note, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedSourceProject, sourceProject, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(refreshedSourceCommit, sourceCommit, StringComparison.OrdinalIgnoreCase);

                if (!preserved)
                {
                    return new MoveLibraryEntryResult
                    {
                        Success = false,
                        EntryId = sourceEntryId,
                        SourceLibraryId = sourceLibraryId,
                        TargetLibraryId = request.TargetLibraryId,
                        PreservedEntryStatus = refreshedEntryStatus,
                        PreservedLifecycleState = refreshedLifecycleState,
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        ErrorMessage = "Move verification failed. Metadata was not preserved exactly."
                    };
                }

                return new MoveLibraryEntryResult
                {
                    Success = true,
                    EntryId = sourceEntryId,
                    SourceLibraryId = sourceLibraryId,
                    TargetLibraryId = request.TargetLibraryId,
                    PreservedEntryStatus = refreshedEntryStatus,
                    PreservedLifecycleState = refreshedLifecycleState
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.AuthInvalid ||
                                                     ex.ErrorCode == ArasErrorCode.AuthExpired ||
                                                     ex.ErrorCode == ArasErrorCode.PermissionDenied ||
                                                     ex.ErrorCode == ArasErrorCode.ServerUnavailable)
            {
                return new MoveLibraryEntryResult
                {
                    Success = false,
                    EntryId = request.EntryId,
                    TargetLibraryId = request.TargetLibraryId,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
            catch (ArasOperationException ex)
            {
                return new MoveLibraryEntryResult
                {
                    Success = false,
                    EntryId = request.EntryId,
                    TargetLibraryId = request.TargetLibraryId,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move Entry failed unexpectedly for Entry '{EntryId}'.", request.EntryId);
                return new MoveLibraryEntryResult
                {
                    Success = false,
                    EntryId = request.EntryId,
                    TargetLibraryId = request.TargetLibraryId,
                    ErrorCode = ArasErrorCode.UnexpectedServerError,
                    ErrorMessage = "Move Entry failed unexpectedly."
                };
            }
        }

        public async Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entry = await GetEntryRelationshipAsync(entryId, cancellationToken).ConfigureAwait(false);
            return await ResolveWithPolicyAsync(entry, policy, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entry = await GetEntryRelationshipAsync(entryId, cancellationToken).ConfigureAwait(false);
            var storedPolicy = ParseRevisionPolicy(entry["revision_policy"]?.Value<string>());
            return await ResolveWithPolicyAsync(entry, storedPolicy, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ResolveLibraryPartResult> ResolveWithPolicyAsync(JObject entry, LibraryRevisionPolicy policy, CancellationToken ct)
        {
            var entryId = entry["id"]?.Value<string>();
            var pinnedPartId = entry["pinned_part_id"]?.Value<string>();
            var relatedPartId = entry["related_id"]?.Value<string>();
            var configId = entry["part_config_id"]?.Value<string>();

            JObject resolvedPart;
            if (policy == LibraryRevisionPolicy.Pinned)
            {
                resolvedPart = await ResolvePinnedPartAsync(pinnedPartId, configId, ct).ConfigureAwait(false);
            }
            else if (policy == LibraryRevisionPolicy.LatestReleased)
            {
                resolvedPart = await ResolveLatestReleasedPartStrictAsync(configId, relatedPartId, ct).ConfigureAwait(false);
            }
            else
            {
                resolvedPart = await ResolveCurrentPartStrictAsync(configId, relatedPartId, ct).ConfigureAwait(false);
            }

            var cadInfo = await GetPrimaryCadInfoAsync(resolvedPart["id"]?.Value<string>(), ct).ConfigureAwait(false);
            var latestReleased = await GetLatestReleasedPartAsync(configId, relatedPartId, ct).ConfigureAwait(false);
            return new ResolveLibraryPartResult
            {
                EntryId = entryId,
                ResolvedPartId = resolvedPart["id"]?.Value<string>(),
                ResolvedPartConfigId = resolvedPart["config_id"]?.Value<string>(),
                ResolvedRevision = resolvedPart["major_rev"]?.Value<string>(),
                LifecycleState = resolvedPart["state"]?.Value<string>(),
                CadStatus = cadInfo.Status,
                HasNewerReleasedRevision = latestReleased != null &&
                                          !string.Equals(latestReleased["id"]?.Value<string>(), resolvedPart["id"]?.Value<string>(), StringComparison.OrdinalIgnoreCase)
            };
        }

        public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken)
        {
            return PromoteEntryAsync(entryId, PartLibrarySchemaNames.EntryLifecyclePublishedState, cancellationToken);
        }

        public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken)
        {
            return PromoteEntryAsync(entryId, PartLibrarySchemaNames.EntryLifecycleDeprecatedState, cancellationToken);
        }

        public async Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(partId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Part ID is required.");

            EnsureAuthenticated();

            var list = new List<PartWhereUsedItem>();

            // 1. Part BOM parents
            var bomAml =
                "<Item type=\"Part BOM\" action=\"get\" select=\"quantity,source_id,related_id\">" +
                "<related_id>" + Escape(partId) + "</related_id>" +
                "</Item>";

            try
            {
                var bomResult = await _aml.ApplyAmlAsync(
                    bomAml,
                    "get",
                    "Part BOM",
                    null,
                    cancellationToken).ConfigureAwait(false);

                foreach (var rel in EnumerateItems(bomResult))
                {
                    var parentId = rel["source_id"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(parentId))
                        continue;

                    var parent = await GetPartAsync(parentId, cancellationToken).ConfigureAwait(false);
                    list.Add(new PartWhereUsedItem
                    {
                        ParentPartId = parentId,
                        ParentPartNumber = parent["item_number"]?.Value<string>(),
                        ParentPartName = parent["name"]?.Value<string>(),
                        ParentRevision = parent["major_rev"]?.Value<string>(),
                        ParentState = parent["state"]?.Value<string>(),
                        Quantity = ParseInt(rel["quantity"]?.Value<string>(), 1),
                        Source = WhereUsedSource.Bom
                    });
                }
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex))
            {
                _logger.LogWarning(ex, "Failed to query Part BOM for part {PartId}.", partId);
            }

            // 2. Library usage records
            try
            {
                var usageAml =
                    "<Item type=\"" + PartLibrarySchemaNames.UsageItemType + "\" action=\"get\" " +
                    "select=\"id,library_entry_id,part_id,project_code,parent_part_id,quantity,used_by,commit_id,action_type,created_on\">" +
                    "<part_id>" + Escape(partId) + "</part_id>" +
                    "</Item>";

                var usageResult = await _aml.ApplyAmlAsync(
                    usageAml,
                    "get",
                    PartLibrarySchemaNames.UsageItemType,
                    null,
                    cancellationToken).ConfigureAwait(false);

                foreach (var item in EnumerateItems(usageResult))
                {
                    var parentId = item["parent_part_id"]?.Value<string>();
                    string parentNumber = null;
                    string parentName = null;
                    string parentRev = null;
                    string parentState = null;

                    if (!string.IsNullOrWhiteSpace(parentId))
                    {
                        try
                        {
                            var parentPart = await GetPartAsync(parentId, cancellationToken).ConfigureAwait(false);
                            parentNumber = parentPart["item_number"]?.Value<string>();
                            parentName = parentPart["name"]?.Value<string>();
                            parentRev = parentPart["major_rev"]?.Value<string>();
                            parentState = parentPart["state"]?.Value<string>();
                        }
                        catch (ArasOperationException)
                        {
                            _logger.LogDebug("Could not resolve parent part {ParentPartId} for usage record.", parentId);
                        }
                    }

                    list.Add(new PartWhereUsedItem
                    {
                        ParentPartId = parentId,
                        ParentPartNumber = parentNumber,
                        ParentPartName = parentName,
                        ParentRevision = parentRev,
                        ParentState = parentState,
                        Quantity = ParseInt(item["quantity"]?.Value<string>(), 1),
                        Source = WhereUsedSource.LibraryUsage,
                        ProjectCode = item["project_code"]?.Value<string>(),
                        UsedBy = item["used_by"]?.Value<string>(),
                        CommitId = item["commit_id"]?.Value<string>(),
                        ActionType = item["action_type"]?.Value<string>(),
                        CreatedOn = ParseDate(item["created_on"]?.Value<string>())
                    });
                }
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex))
            {
                _logger.LogWarning(ex, "Failed to query Library usage records for part {PartId}.", partId);
            }

            return list;
        }

        public async Task<LibraryMutationResult> CreateLibraryAsync(
            CreatePartLibraryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Name?.Trim()))
                return new LibraryMutationResult { Success = false, ErrorMessage = "Library name is required." };

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var duplicate = await FindLibraryByNameAsync(request.Name.Trim(), null, cancellationToken).ConfigureAwait(false);
            if (duplicate != null)
                return new LibraryMutationResult
                {
                    Success = false,
                    ErrorMessage = "A Library named '" + request.Name.Trim() + "' already exists."
                };

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.LibraryItemType + "\" action=\"add\">" +
                "<name>" + Escape(request.Name.Trim()) + "</name>" +
                "<description>" + Escape(request.Description) + "</description>" +
                "<library_type>" + Escape(request.LibraryType.ToString()) + "</library_type>" +
                "<status>" + PartLibrarySchemaNames.LibraryStatusActive + "</status>" +
                "<default_revision_policy>" + Escape(request.DefaultRevisionPolicy ?? "LatestCurrent") + "</default_revision_policy>" +
                "<is_public>" + (request.IsPublic ? "1" : "0") + "</is_public>" +
                "</Item>";

            try
            {
                var result = await _aml.ApplyAmlAsync(
                    aml,
                    "add",
                    PartLibrarySchemaNames.LibraryItemType,
                    null,
                    cancellationToken).ConfigureAwait(false);

                var libraryId = result["id"]?.Value<string>();
                return new LibraryMutationResult
                {
                    Success = !string.IsNullOrWhiteSpace(libraryId),
                    LibraryId = libraryId,
                    ErrorMessage = string.IsNullOrWhiteSpace(libraryId) ? "Library creation did not return an ID." : null
                };
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex) && ex.ErrorCode != ArasErrorCode.ServerUnavailable)
            {
                _logger.LogError(ex, "Failed to create library '{Name}'.", request.Name);
                return new LibraryMutationResult
                {
                    Success = false,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<LibraryMutationResult> UpdateLibraryAsync(
            UpdatePartLibraryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.LibraryId))
                return new LibraryMutationResult { Success = false, ErrorMessage = "Library ID is required." };

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.Name?.Trim()))
            {
                var duplicate = await FindLibraryByNameAsync(request.Name.Trim(), request.LibraryId, cancellationToken).ConfigureAwait(false);
                if (duplicate != null)
                    return new LibraryMutationResult
                    {
                        Success = false,
                        LibraryId = request.LibraryId,
                        ErrorMessage = "A Library named '" + request.Name.Trim() + "' already exists."
                    };
            }

            var xml = new System.Text.StringBuilder();
            xml.Append("<Item type=\"").Append(PartLibrarySchemaNames.LibraryItemType)
               .Append("\" action=\"edit\" id=\"").Append(Escape(request.LibraryId)).Append("\">");

            if (!string.IsNullOrWhiteSpace(request.Name))
                xml.Append("<name>").Append(Escape(request.Name.Trim())).Append("</name>");

            if (request.Description != null)
                xml.Append("<description>").Append(Escape(request.Description)).Append("</description>");

            xml.Append("<library_type>").Append(Escape(request.LibraryType.ToString())).Append("</library_type>");

            if (!string.IsNullOrWhiteSpace(request.DefaultRevisionPolicy))
                xml.Append("<default_revision_policy>").Append(Escape(request.DefaultRevisionPolicy)).Append("</default_revision_policy>");

            xml.Append("<is_public>").Append(request.IsPublic ? "1" : "0").Append("</is_public>");
            xml.Append("</Item>");

            try
            {
                await _aml.ApplyAmlAsync(
                    xml.ToString(),
                    "edit",
                    PartLibrarySchemaNames.LibraryItemType,
                    request.LibraryId,
                    cancellationToken).ConfigureAwait(false);

                return new LibraryMutationResult
                {
                    Success = true,
                    LibraryId = request.LibraryId
                };
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex) && ex.ErrorCode != ArasErrorCode.ServerUnavailable)
            {
                _logger.LogError(ex, "Failed to update library '{LibraryId}'.", request.LibraryId);
                return new LibraryMutationResult
                {
                    Success = false,
                    LibraryId = request.LibraryId,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<LibraryMutationResult> ArchiveLibraryAsync(
            string libraryId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
                return new LibraryMutationResult { Success = false, ErrorMessage = "Library ID is required." };

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.LibraryItemType + "\" action=\"edit\" id=\"" + Escape(libraryId) + "\">" +
                "<status>" + PartLibrarySchemaNames.LibraryStatusArchived + "</status>" +
                "</Item>";

            try
            {
                await _aml.ApplyAmlAsync(
                    aml,
                    "edit",
                    PartLibrarySchemaNames.LibraryItemType,
                    libraryId,
                    cancellationToken).ConfigureAwait(false);

                return new LibraryMutationResult
                {
                    Success = true,
                    LibraryId = libraryId
                };
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex) && ex.ErrorCode != ArasErrorCode.ServerUnavailable)
            {
                _logger.LogError(ex, "Failed to archive library '{LibraryId}'.", libraryId);
                return new LibraryMutationResult
                {
                    Success = false,
                    LibraryId = libraryId,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PartPickerSearchResponse> SearchPartsAsync(
            PartPickerSearchRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            EnsureAuthenticated();

            var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
            if (pageSize > 100) pageSize = 100;
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;

            var hasFilters = !string.IsNullOrWhiteSpace(request.Keyword)
                || !string.IsNullOrWhiteSpace(request.PartType)
                || !string.IsNullOrWhiteSpace(request.LifecycleState)
                || !string.IsNullOrWhiteSpace(request.MajorRev)
                || request.CurrentOnly == true;

            var aml = new System.Text.StringBuilder();
            aml.Append("<Item type=\"Part\" action=\"get\" select=\"id,config_id,item_number,name,classification,major_rev,generation,state,is_current,created_on\"")
                .Append(" pagesize=\"").Append(pageSize).Append("\"")
                .Append(" page=\"").Append(pageNumber).Append("\">");

            if (hasFilters)
            {
                aml.Append("<AND>");

                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    aml.Append("<OR>");
                    aml.Append("<item_number condition=\"like\">")
                       .Append(Escape(request.Keyword)).Append("%</item_number>");
                    aml.Append("<name condition=\"like\">")
                       .Append(Escape(request.Keyword)).Append("%</name>");
                    aml.Append("</OR>");
                }

                if (!string.IsNullOrWhiteSpace(request.PartType))
                    aml.Append("<classification>").Append(Escape(request.PartType)).Append("</classification>");

                if (!string.IsNullOrWhiteSpace(request.LifecycleState))
                    aml.Append("<state>").Append(Escape(request.LifecycleState)).Append("</state>");

                if (!string.IsNullOrWhiteSpace(request.MajorRev))
                    aml.Append("<major_rev>").Append(Escape(request.MajorRev)).Append("</major_rev>");

                if (request.CurrentOnly == true)
                    aml.Append("<is_current>1</is_current>");

                aml.Append("</AND>");
            }

            aml.Append("</Item>");

            try
            {
                var result = await _aml.ApplyAmlAsync(
                    aml.ToString(),
                    "get",
                    "Part",
                    null,
                    cancellationToken).ConfigureAwait(false);

                var items = EnumerateItems(result)
                    .Select(MapPartSearchItem)
                    .ToList();

                return new PartPickerSearchResponse
                {
                    Items = items,
                    TotalCount = items.Count,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Part search failed for keyword '{Keyword}'.", request.Keyword);
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "Part search failed unexpectedly.");
            }
        }

        public async Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(
            PartRevisionHistoryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
            if (pageSize > 100)
                pageSize = 100;
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;

            var configId = request.PartConfigId;
            if (string.IsNullOrWhiteSpace(configId))
            {
                if (string.IsNullOrWhiteSpace(request.PartId))
                {
                    throw new ArasOperationException(
                        ArasErrorCode.ValidationFailed,
                        "PartConfigId or PartId is required for revision history.");
                }

                var part = await GetPartAsync(request.PartId, cancellationToken).ConfigureAwait(false);
                configId = part["config_id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(configId))
                {
                    throw new ArasOperationException(
                        ArasErrorCode.ValidationFailed,
                        "PartId does not resolve to a readable config_id.");
                }
            }

            var totalCount = await CountPartRevisionsAsync(configId, cancellationToken).ConfigureAwait(false);
            var aml =
                "<Item type=\"Part\" action=\"get\" " +
                "select=\"id,config_id,item_number,name,classification,major_rev,generation,state,is_current,modified_on,created_on\" " +
                "orderBy=\"generation desc,modified_on desc,major_rev desc\" " +
                "pagesize=\"" + pageSize + "\" " +
                "page=\"" + pageNumber + "\">" +
                "<config_id>" + Escape(configId) + "</config_id>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                "Part",
                null,
                cancellationToken).ConfigureAwait(false);

            var items = EnumerateItems(result)
                .Select(MapPartRevisionHistoryItem)
                .Where(item => item != null)
                .OrderBy(item => item, new PartRevisionHistoryItemComparer())
                .ToList();

            return new PartRevisionHistoryResponse
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PartPreview> GetPartPreviewAsync(
            string partId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(partId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Part ID is required.");

            EnsureAuthenticated();

            try
            {
                var part = await GetPartAsync(partId, cancellationToken).ConfigureAwait(false);
                var configId = part["config_id"]?.Value<string>();
                var cadInfo = await GetPrimaryCadInfoAsync(partId, cancellationToken).ConfigureAwait(false);
                var state = part["state"]?.Value<string>();
                var isObsolete = PartLifecyclePolicy.IsPartObsolete(state);
                var hasCad = !string.Equals(cadInfo.Status, "No CAD", StringComparison.OrdinalIgnoreCase);

                return new PartPreview
                {
                    ConfigId = configId,
                    PartId = part["id"]?.Value<string>(),
                    Revision = part["major_rev"]?.Value<string>(),
                    LifecycleState = state,
                    Generation = part["generation"]?.Value<string>(),
                    CadStatus = cadInfo.Status,
                    IsEligibleForReuse = !isObsolete && hasCad,
                    IneligibilityReason = isObsolete
                        ? "Part is in state '" + state + "' and cannot be reused."
                        : !hasCad
                            ? "Part has no associated CAD file."
                            : null
                };
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get preview for Part '{PartId}'.", partId);
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "Failed to retrieve Part preview.");
            }
        }

        public async Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(
            string libraryId,
            string partConfigId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library ID is required.");
            if (string.IsNullOrWhiteSpace(partConfigId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Part config ID is required for duplicate check.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entries = await LoadEntrySummariesAsync(libraryId, cancellationToken).ConfigureAwait(false);
            var match = entries.FirstOrDefault(entry =>
                string.Equals(entry.PartConfigId, partConfigId, StringComparison.OrdinalIgnoreCase) &&
                !entry.IsDeprecated);

            return new DuplicateEntryCheckResult
            {
                IsDuplicate = match != null,
                ExistingEntryId = match?.EntryId,
                ExistingEntryStatus = match?.EntryStatus ?? LibraryEntryStatus.Draft
            };
        }

        public async Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LibraryEntryId))
                return new RecordLibraryUsageResult { Success = false };

            EnsureAuthenticated();

            return await TryRecordUsageViaServerMethodAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private static string ComputeIdempotencyKey(LibraryUsageRequest request)
        {
            var raw = string.Join("|",
                request.LibraryEntryId ?? "",
                request.ProjectCode ?? "",
                request.ParentPartId ?? "",
                request.CommitId ?? "",
                request.ActionType ?? "",
                request.PartId ?? "");
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task<RecordLibraryUsageResult> TryRecordUsageViaServerMethodAsync(LibraryUsageRequest request, CancellationToken ct)
        {
            var idempotencyKey = request.IdempotencyKey ?? ComputeIdempotencyKey(request);
            var parameters = new Dictionary<string, string>
            {
                ["library_entry_id"] = request.LibraryEntryId,
                ["part_id"] = request.PartId ?? string.Empty,
                ["project_code"] = request.ProjectCode ?? string.Empty,
                ["parent_part_id"] = request.ParentPartId ?? string.Empty,
                ["quantity"] = request.Quantity.ToString(),
                ["used_by"] = request.UsedBy ?? string.Empty,
                ["commit_id"] = request.CommitId ?? string.Empty,
                ["action_type"] = request.ActionType ?? string.Empty,
                [PartLibrarySchemaNames.UsageIdempotencyKeyProperty] = idempotencyKey
            };

            try
            {
                var result = await _aml.ApplyMethodAsync(
                    PartLibrarySchemaNames.RecordPartLibraryUsageMethodName,
                    parameters,
                    ct).ConfigureAwait(false);

                if (result != null)
                {
                    var usageId = result["usage_id"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(usageId))
                    {
                        _logger.LogWarning(
                            "Usage Method returned a response without a readable usage_id for entry {EntryId}.",
                            request.LibraryEntryId);

                        throw new ArasOperationException(
                            ArasErrorCode.UnexpectedServerError,
                            "Usage Method returned an invalid response.");
                    }

                    var usageCount = ParseInt(result["usage_count"]?.Value<string>(), 0);
                    _logger.LogInformation(
                        "Part Library usage recorded via server method for entry {EntryId}. " +
                        "Usage count: {UsageCount}.",
                        request.LibraryEntryId, usageCount);

                    return new RecordLibraryUsageResult
                    {
                        Success = true,
                        AlreadyExists = ParseBoolean(result["already_exists"]?.Value<string>()),
                        TrackingUnavailable = false,
                        UsageId = usageId,
                        UsageCount = usageCount,
                        LastUsedOn = ParseDate(result["last_used_on"]?.Value<string>()),
                        IdempotencyKey = result["idempotency_key"]?.Value<string>() ?? idempotencyKey
                    };
                }

                // Null response: do not report success
                _logger.LogWarning(
                    "Usage Method returned null for entry {EntryId}. Throwing unexpected error.",
                    request.LibraryEntryId);

                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "Usage Method returned an invalid response.");
            }
            catch (ArasOperationException ex) when (CanFallbackToDirectAdd(ex, PartLibrarySchemaNames.RecordPartLibraryUsageMethodName))
            {
                _logger.LogWarning(
                    "Part Library usage tracking unavailable: server method '{MethodName}' is not deployed. " +
                    "Usage Items were not created.",
                    PartLibrarySchemaNames.RecordPartLibraryUsageMethodName);

                return new RecordLibraryUsageResult
                {
                    Success = true,
                    TrackingUnavailable = true,
                    WarningMessage = "Usage tracking is not available. The server method '" +
                                     PartLibrarySchemaNames.RecordPartLibraryUsageMethodName + "' is not deployed."
                };
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling server method '{MethodName}' for entry {EntryId}.",
                    PartLibrarySchemaNames.RecordPartLibraryUsageMethodName, request.LibraryEntryId);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _http?.Dispose();
            _http = null;
            _aml = null;
            _disposed = true;
        }

        private void EnsureAuthenticated()
        {
            if (_aml == null)
            {
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "HttpPartLibraryClient is not authenticated. Call SetSession() after login.");
            }
        }

        private async Task EnsureSchemaAvailableAsync(CancellationToken ct)
        {
            if (_schemaAvailable == true)
                return;

            var hasLibrary = await ItemTypeExistsAsync(PartLibrarySchemaNames.LibraryItemType, ct).ConfigureAwait(false);
            var hasEntry = await ItemTypeExistsAsync(PartLibrarySchemaNames.EntryRelationshipType, ct).ConfigureAwait(false);
            _schemaAvailable = hasLibrary && hasEntry;

            if (_schemaAvailable != true)
            {
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "Part Library schema is not configured on Aras live. Create 'idea_PartLibrary' and 'idea_PartLibraryEntry' first.");
            }
        }

        private async Task<bool> ItemTypeExistsAsync(string itemTypeName, CancellationToken ct)
        {
            try
            {
                var aml =
                    "<Item type=\"ItemType\" action=\"get\" select=\"id,name\">" +
                    "<name>" + Escape(itemTypeName) + "</name>" +
                    "</Item>";

                var result = await _aml.ApplyAmlAsync(aml, "get", "ItemType", null, ct).ConfigureAwait(false);
                return EnumerateItems(result).Any();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArasOperationException ex) when (!IsAuthOrPermissionFailure(ex))
            {
                _logger.LogDebug(ex, "Failed to verify ItemType {ItemTypeName}.", itemTypeName);
                return false;
            }
            catch (Exception ex)
            {
                if (IsAuthOrPermissionFailure(ex))
                    throw;

                _logger.LogWarning(ex, "Failed to verify ItemType {ItemTypeName}.", itemTypeName);
                return false;
            }
        }

        private async Task<int> CountEntriesAsync(string libraryId, CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"get\" select=\"id\">" +
                "<source_id>" + Escape(libraryId) + "</source_id>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                PartLibrarySchemaNames.EntryRelationshipType,
                null,
                ct).ConfigureAwait(false);

            return EnumerateTopLevelEntryRelationships(result).Count();
        }

        private async Task<List<PartLibraryEntrySummary>> LoadEntrySummariesAsync(string libraryId, CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"get\" " +
                "select=\"id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,pinned_revision,entry_status,state,category,tags,note,source_project,source_commit,usage_count,last_used_on\">" +
                (string.IsNullOrWhiteSpace(libraryId)
                    ? string.Empty
                    : "<source_id>" + Escape(libraryId) + "</source_id>") +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                PartLibrarySchemaNames.EntryRelationshipType,
                null,
                ct).ConfigureAwait(false);

            var usageCounts = await LoadUsageCountsAsync(ct).ConfigureAwait(false);

            var summaries = new List<PartLibraryEntrySummary>();
            foreach (var rel in EnumerateTopLevelEntryRelationships(result))
            {
                try
                {
                    var summary = await MapEntrySummaryAsync(rel, usageCounts, ct).ConfigureAwait(false);
                    if (summary != null)
                        summaries.Add(summary);
                }
                catch (ArasOperationException ex) when (IsEntryResolutionFailure(ex))
                {
                    summaries.Add(await CreateDiagnosticSummaryAsync(rel, ex, usageCounts, ct).ConfigureAwait(false));
                }
            }

            return summaries;
        }

        private async Task<int> CountPartRevisionsAsync(string configId, CancellationToken ct)
        {
            var aml =
                "<Item type=\"Part\" action=\"get\" select=\"id\">" +
                "<config_id>" + Escape(configId) + "</config_id>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                "Part",
                null,
                ct).ConfigureAwait(false);

            return EnumerateItems(result)
                .Count(item =>
                {
                    var itemType = item["type"]?.Value<string>();
                    return string.IsNullOrWhiteSpace(itemType) ||
                           string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase);
                });
        }

        private async Task<UsageCountSnapshot> LoadUsageCountsAsync(CancellationToken ct)
        {
            try
            {
                var usageAml =
                    "<Item type=\"" + PartLibrarySchemaNames.UsageItemType + "\" action=\"get\" " +
                    "select=\"id,library_entry_id\" />";

                var usageResult = await _aml.ApplyAmlAsync(
                    usageAml,
                    "get",
                    PartLibrarySchemaNames.UsageItemType,
                    null,
                    ct).ConfigureAwait(false);

                var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in EnumerateItems(usageResult))
                {
                    var entryId = item["library_entry_id"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(entryId))
                        continue;

                    if (groups.TryGetValue(entryId, out var count))
                        groups[entryId] = count + 1;
                    else
                        groups[entryId] = 1;
                }

                return new UsageCountSnapshot
                {
                    IsAuthoritative = true,
                    Counts = groups
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArasOperationException ex)
            {
                if (!IsMissingUsageItemTypeError(ex))
                    throw;

                _logger.LogWarning(ex,
                    "Usage ItemType '{ItemType}' is not deployed. Using cached usage_count as compatibility fallback.",
                    PartLibrarySchemaNames.UsageItemType);
                return new UsageCountSnapshot
                {
                    IsAuthoritative = false,
                    Counts = new Dictionary<string, int>(0)
                };
            }
        }

        private async Task<PartLibraryEntrySummary> MapEntrySummaryAsync(
            JObject relationship,
            UsageCountSnapshot usageSnapshot,
            CancellationToken ct)
        {
            var partId = relationship["related_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(partId))
            {
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "Library Entry is malformed: related_id is missing.");
            }

            var policy = ParseRevisionPolicy(relationship["revision_policy"]?.Value<string>());
            var configId = relationship["part_config_id"]?.Value<string>();
            var pinnedPartId = relationship["pinned_part_id"]?.Value<string>();

            var libraryId = relationship["source_id"]?.Value<string>();
            var library = await TryGetLibraryForEntryAsync(libraryId, ct).ConfigureAwait(false);

            JObject resolvedPart;
            if (policy == LibraryRevisionPolicy.Pinned)
            {
                resolvedPart = await ResolvePinnedPartAsync(pinnedPartId, configId, ct).ConfigureAwait(false);
            }
            else if (policy == LibraryRevisionPolicy.LatestReleased)
            {
                resolvedPart = await ResolveLatestReleasedPartStrictAsync(configId, partId, ct).ConfigureAwait(false);
            }
            else
            {
                resolvedPart = await ResolveCurrentPartStrictAsync(configId, partId, ct).ConfigureAwait(false);
            }

            var lifecycleState = relationship["state"]?.Value<string>();
            var effectiveStatus = GetEffectiveEntryStatus(lifecycleState, relationship["entry_status"]?.Value<string>());
            var entryId = relationship["id"]?.Value<string>();

            var cad = await GetPrimaryCadInfoAsync(resolvedPart["id"]?.Value<string>(), ct).ConfigureAwait(false);
            var latestReleased = await GetLatestReleasedPartAsync(configId, resolvedPart["id"]?.Value<string>(), ct).ConfigureAwait(false);

            return new PartLibraryEntrySummary
            {
                EntryId = entryId,
                LibraryId = libraryId,
                LibraryName = library?["name"]?.Value<string>() ?? GetUnavailableLibraryName(libraryId),
                PartId = resolvedPart["id"]?.Value<string>(),
                PartConfigId = resolvedPart["config_id"]?.Value<string>() ?? configId,
                PartNumber = resolvedPart["item_number"]?.Value<string>(),
                PartName = resolvedPart["name"]?.Value<string>(),
                PartType = resolvedPart["classification"]?.Value<string>(),
                Revision = resolvedPart["major_rev"]?.Value<string>(),
                LifecycleState = resolvedPart["state"]?.Value<string>(),
                EntryLifecycleState = lifecycleState ?? relationship["entry_status"]?.Value<string>(),
                RevisionPolicy = policy,
                EntryStatus = effectiveStatus,
                CadStatus = cad.Status,
                UsageCount = GetUsageCountForEntry(usageSnapshot, entryId, relationship["usage_count"]?.Value<string>()),
                HasNewerReleasedRevision = latestReleased != null &&
                                          !string.Equals(latestReleased["id"]?.Value<string>(), resolvedPart["id"]?.Value<string>(), StringComparison.OrdinalIgnoreCase),
                IsDeprecated = effectiveStatus == LibraryEntryStatus.Deprecated,
                ResolutionFailed = false,
                ResolutionError = null,
                CanAddToProject = effectiveStatus != LibraryEntryStatus.Deprecated,
                LastUsedOn = ParseDate(relationship["last_used_on"]?.Value<string>())
            };
        }

        private async Task<PartLibraryEntryDetails> MapEntryDetailsAsync(
            JObject relationship,
            UsageCountSnapshot usageSnapshot,
            CancellationToken ct)
        {
            var summary = await MapEntrySummaryAsync(relationship, usageSnapshot, ct).ConfigureAwait(false);
            if (summary == null)
                return null;

            var cad = await GetPrimaryCadInfoAsync(summary.PartId, ct).ConfigureAwait(false);
            return new PartLibraryEntryDetails
            {
                EntryId = summary.EntryId,
                LibraryId = summary.LibraryId,
                LibraryName = summary.LibraryName,
                PartId = summary.PartId,
                PartConfigId = summary.PartConfigId,
                PartNumber = summary.PartNumber,
                PartName = summary.PartName,
                PartType = summary.PartType,
                Revision = summary.Revision,
                LifecycleState = summary.LifecycleState,
                EntryLifecycleState = summary.EntryLifecycleState,
                RevisionPolicy = summary.RevisionPolicy,
                EntryStatus = summary.EntryStatus,
                CadStatus = summary.CadStatus,
                PrimaryCadId = cad.CadId,
                PrimaryCadFileName = cad.FileName,
                PrimaryCadState = cad.State,
                LockedBy = cad.LockedBy,
                UsageCount = summary.UsageCount,
                Description = relationship["note"]?.Value<string>() ?? summary.PartName,
                Category = relationship["category"]?.Value<string>(),
                Tags = relationship["tags"]?.Value<string>(),
                HasNewerReleasedRevision = summary.HasNewerReleasedRevision,
                ResolutionFailed = summary.ResolutionFailed,
                ResolutionError = summary.ResolutionError,
                CanAddToProject = summary.CanAddToProject
            };
        }

        private async Task<PartLibraryEntrySummary> CreateDiagnosticSummaryAsync(
            JObject relationship,
            Exception ex,
            UsageCountSnapshot usageSnapshot,
            CancellationToken ct)
        {
            var relatedPartId = relationship["related_id"]?.Value<string>();
            var libraryId = relationship["source_id"]?.Value<string>();
            var library = await TryGetLibraryForEntryAsync(libraryId, ct).ConfigureAwait(false);
            var libraryName = library?["name"]?.Value<string>() ?? GetUnavailableLibraryName(libraryId);

            string partNumber = null;
            string partName = null;
            string partType = null;
            string partState = null;
            var partNotFound = false;
            if (!string.IsNullOrWhiteSpace(relatedPartId))
            {
                try
                {
                    var part = await GetPartAsync(relatedPartId, ct).ConfigureAwait(false);
                    partNumber = part["item_number"]?.Value<string>();
                    partName = part["name"]?.Value<string>();
                    partType = part["classification"]?.Value<string>();
                    partState = part["state"]?.Value<string>();
                }
                catch (ArasOperationException partEx) when (IsEntryResolutionFailure(partEx))
                {
                    partNotFound = partEx.ErrorCode == ArasErrorCode.PartNotFound;
                }
            }

            var entryLifecycleState = relationship["state"]?.Value<string>() ?? relationship["entry_status"]?.Value<string>();
            var entryStatus = GetEffectiveEntryStatus(relationship["state"]?.Value<string>(), relationship["entry_status"]?.Value<string>());
            var resolutionError = BuildResolutionError(ex, partState, partNotFound);

            // Append Library resolution message when library was unavailable
            if (libraryName == "(Unavailable Library)" && !string.IsNullOrWhiteSpace(libraryId))
            {
                var libMsg = "Library '" + libraryId + "' could not be resolved.";
                resolutionError = string.IsNullOrWhiteSpace(resolutionError)
                    ? libMsg
                    : resolutionError + " " + libMsg;
            }

            var entryId = relationship["id"]?.Value<string>();
            var diagnosticPartNumber = string.IsNullOrWhiteSpace(partNumber)
                ? "(Invalid Library Entry)"
                : partNumber;
            var diagnosticPartName = string.IsNullOrWhiteSpace(partName)
                ? BuildDiagnosticEntryName(entryId)
                : partName;

            return new PartLibraryEntrySummary
            {
                EntryId = entryId,
                LibraryId = libraryId,
                LibraryName = libraryName,
                PartId = relatedPartId,
                PartConfigId = relationship["part_config_id"]?.Value<string>(),
                PartNumber = diagnosticPartNumber,
                PartName = diagnosticPartName,
                PartType = partType,
                Revision = relationship["pinned_revision"]?.Value<string>(),
                LifecycleState = partState,
                EntryLifecycleState = entryLifecycleState,
                RevisionPolicy = ParseRevisionPolicy(relationship["revision_policy"]?.Value<string>()),
                EntryStatus = entryStatus,
                CadStatus = "Unavailable",
                UsageCount = GetUsageCountForEntry(usageSnapshot, relationship["id"]?.Value<string>(), relationship["usage_count"]?.Value<string>()),
                HasNewerReleasedRevision = false,
                IsDeprecated = entryStatus == LibraryEntryStatus.Deprecated,
                ResolutionFailed = true,
                ResolutionError = resolutionError,
                CanAddToProject = false,
                LastUsedOn = ParseDate(relationship["last_used_on"]?.Value<string>())
            };
        }

        private static string BuildResolutionError(Exception ex, string partState, bool partNotFound)
        {
            if (partNotFound)
                return "Part was not found. It may have been deleted or moved.";

            return SanitizeForEntry(ex);
        }

        private static string BuildDiagnosticEntryName(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return "Unknown Entry";

            var safeId = entryId.Trim();
            var suffix = safeId.Length <= 8
                ? safeId
                : safeId.Substring(safeId.Length - 8);
            return "Entry " + suffix;
        }

        private static int GetUsageCountForEntry(
            UsageCountSnapshot usageSnapshot,
            string entryId,
            string cachedUsageCount)
        {
            if (usageSnapshot != null && usageSnapshot.IsAuthoritative)
            {
                if (string.IsNullOrWhiteSpace(entryId))
                    return 0;

                int groupedCount;
                if (usageSnapshot.Counts != null &&
                    usageSnapshot.Counts.TryGetValue(entryId, out groupedCount))
                {
                    return groupedCount;
                }

                return 0;
            }

            return ParseInt(cachedUsageCount, 0);
        }

        private async Task<JObject> GetEntryRelationshipAsync(string entryId, CancellationToken ct)
        {
            var result = await _aml.ApplyItemAsync(
                PartLibrarySchemaNames.EntryRelationshipType,
                entryId,
                "get",
                "id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,pinned_revision,entry_status,state,category,tags,note,source_project,source_commit,usage_count,last_used_on",
                ct).ConfigureAwait(false);

            return result;
        }

        private async Task<JObject> GetLibraryAsync(string libraryId, CancellationToken ct)
        {
            return await _aml.ApplyItemAsync(
                PartLibrarySchemaNames.LibraryItemType,
                libraryId,
                "get",
                "id,name,description,library_type,status,default_revision_policy,is_public",
                ct).ConfigureAwait(false);
        }

        private async Task<JObject> TryGetLibraryForEntryAsync(string libraryId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
                return null;

            try
            {
                return await GetLibraryAsync(libraryId, ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (IsAuthOrPermissionFailure(ex) || ex.ErrorCode == ArasErrorCode.ServerUnavailable)
            {
                throw;
            }
            catch (ArasOperationException ex)
            {
                _logger.LogWarning(ex,
                    "Library {LibraryId} could not be resolved while loading Part Library entries.",
                    libraryId);
                return null;
            }
            catch (Exception ex)
            {
                if (IsAuthOrPermissionFailure(ex))
                    throw;

                _logger.LogWarning(ex,
                    "Library {LibraryId} could not be resolved while loading Part Library entries.",
                    libraryId);
                return null;
            }
        }

        private static string GetUnavailableLibraryName(string libraryId)
        {
            return string.IsNullOrWhiteSpace(libraryId) ? null : "(Unavailable Library)";
        }

        private async Task<JObject> GetPartAsync(string partId, CancellationToken ct)
        {
            return await _aml.ApplyItemAsync(
                "Part",
                partId,
                "get",
                "id,config_id,item_number,name,classification,major_rev,state,generation",
                ct).ConfigureAwait(false);
        }

        private async Task<JObject> ResolveLatestReleasedPartAsync(string configId, string fallbackPartId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(configId))
            {
                var latestReleased = await GetLatestReleasedPartAsync(configId, fallbackPartId, ct).ConfigureAwait(false);
                if (latestReleased != null)
                    return latestReleased;
            }

            return await GetPartAsync(fallbackPartId, ct).ConfigureAwait(false);
        }

        private async Task<JObject> GetLatestReleasedPartAsync(string configId, string fallbackPartId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return string.IsNullOrWhiteSpace(fallbackPartId) ? null : await GetPartAsync(fallbackPartId, ct).ConfigureAwait(false);

            var releasedAml =
                "<Item type=\"Part\" action=\"get\" select=\"id,config_id,item_number,name,classification,major_rev,state,generation\">" +
                "<config_id>" + Escape(configId) + "</config_id>" +
                "<state>" + PartLibrarySchemaNames.PartReleasedState + "</state>" +
                "</Item>";

            var released = await _aml.ApplyAmlAsync(releasedAml, "get", "Part", null, ct).ConfigureAwait(false);
            var releasedItems = EnumerateItems(released).ToList();
            if (releasedItems.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(fallbackPartId))
                    return null;

                return await GetPartAsync(fallbackPartId, ct).ConfigureAwait(false);
            }

            return SelectLatestPartVersion(releasedItems);
        }

        private async Task<JObject> ResolvePinnedPartAsync(string pinnedPartId, string configId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(pinnedPartId))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    "Pinned revision is not available: pinned_part_id is missing on the Entry.");
            }

            var part = await GetPartAsync(pinnedPartId, ct).ConfigureAwait(false);
            return ValidateReusablePart(part, configId, "Pinned Part");
        }

        private async Task<JObject> ResolveLatestReleasedPartStrictAsync(string configId, string fallbackPartId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    "Cannot resolve LatestReleased: Entry has no part_config_id.");
            }

            var releasedAml =
                "<Item type=\"Part\" action=\"get\" select=\"id,config_id,item_number,name,classification,major_rev,state,generation\">" +
                "<config_id>" + Escape(configId) + "</config_id>" +
                "<state>" + PartLibrarySchemaNames.PartReleasedState + "</state>" +
                "</Item>";

            var released = await _aml.ApplyAmlAsync(releasedAml, "get", "Part", null, ct).ConfigureAwait(false);
            var releasedItems = EnumerateItems(released).ToList();
            if (releasedItems.Count == 0)
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    $"No released revision is available for Part configuration '{configId}'.");
            }

            var best = SelectLatestPartVersion(releasedItems);
            return ValidateReusablePart(best, configId, "LatestReleased Part");
        }

        private async Task<JObject> ResolveCurrentPartStrictAsync(string configId, string fallbackPartId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(configId))
            {
                var currentAml =
                    "<Item type=\"Part\" action=\"get\" select=\"id,config_id,item_number,name,classification,major_rev,state,generation\">" +
                    "<config_id>" + Escape(configId) + "</config_id>" +
                    "<is_current>1</is_current>" +
                    "</Item>";

                var current = await _aml.ApplyAmlAsync(currentAml, "get", "Part", null, ct).ConfigureAwait(false);
                var currentItem = EnumerateItems(current).FirstOrDefault();
                if (currentItem != null)
                    return ValidateReusablePart(currentItem, configId, "Current Part");
            }

            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    "Cannot resolve LatestCurrent: Entry has no part_config_id.");
            }

            throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                $"No current revision is available for Part configuration '{configId}'.");
        }

        public async Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(
            UpdateLibraryRevisionPolicyRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.EntryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "EntryId is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entry = await GetEntryRelationshipAsync(request.EntryId, cancellationToken).ConfigureAwait(false);
            var configId = entry["part_config_id"]?.Value<string>();

            if (request.RevisionPolicy == LibraryRevisionPolicy.Pinned)
            {
                if (string.IsNullOrWhiteSpace(request.PinnedPartId))
                {
                    throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                        "PinnedPartId is required when updating to Pinned policy.");
                }

                var pinnedPart = await ResolvePinnedPartAsync(request.PinnedPartId, configId, cancellationToken).ConfigureAwait(false);
                var pinnedConfigId = pinnedPart["config_id"]?.Value<string>();
                var pinnedRev = pinnedPart["major_rev"]?.Value<string>();
                var editAml =
                    "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"edit\" id=\"" + Escape(request.EntryId) + "\">" +
                    "<revision_policy>Pinned</revision_policy>" +
                    "<pinned_part_id>" + Escape(request.PinnedPartId) + "</pinned_part_id>" +
                    "<pinned_revision>" + Escape(pinnedRev) + "</pinned_revision>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(editAml, "edit", PartLibrarySchemaNames.EntryRelationshipType, request.EntryId, cancellationToken).ConfigureAwait(false);

                return new UpdateLibraryRevisionPolicyResult
                {
                    Success = true,
                    EntryId = request.EntryId,
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    ResolvedPartId = pinnedPart["id"]?.Value<string>(),
                    ResolvedPartConfigId = pinnedConfigId,
                    ResolvedRevision = pinnedRev
                };
            }
            else
            {
                var policyStr = request.RevisionPolicy == LibraryRevisionPolicy.LatestReleased ? "LatestReleased" : "LatestCurrent";
                JObject resolvedPart;
                if (request.RevisionPolicy == LibraryRevisionPolicy.LatestReleased)
                {
                    resolvedPart = await ResolveLatestReleasedPartStrictAsync(configId, entry["related_id"]?.Value<string>(), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    resolvedPart = await ResolveCurrentPartStrictAsync(configId, entry["related_id"]?.Value<string>(), cancellationToken).ConfigureAwait(false);
                }

                var editAml =
                    "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"edit\" id=\"" + Escape(request.EntryId) + "\">" +
                    "<revision_policy>" + policyStr + "</revision_policy>" +
                    "<pinned_part_id is_null=\"1\" />" +
                    "<pinned_revision is_null=\"1\" />" +
                    "</Item>";

                await _aml.ApplyAmlAsync(editAml, "edit", PartLibrarySchemaNames.EntryRelationshipType, request.EntryId, cancellationToken).ConfigureAwait(false);
                return new UpdateLibraryRevisionPolicyResult
                {
                    Success = true,
                    EntryId = request.EntryId,
                    RevisionPolicy = request.RevisionPolicy,
                    ResolvedPartId = resolvedPart["id"]?.Value<string>(),
                    ResolvedPartConfigId = resolvedPart["config_id"]?.Value<string>(),
                    ResolvedRevision = resolvedPart["major_rev"]?.Value<string>()
                };
            }
        }

        // D-02: Unique on Library ID + part_config_id, case-insensitive.
        // Active statuses: Draft, PendingReview, Published. Deprecated is not active.
        private async Task<string> FindDuplicateEntryIdD02Async(
            string libraryId,
            string partConfigId,
            CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"get\" " +
                "select=\"id,part_config_id,entry_status,state\">" +
                "<source_id>" + Escape(libraryId) + "</source_id>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                PartLibrarySchemaNames.EntryRelationshipType,
                null,
                ct).ConfigureAwait(false);

            var match = EnumerateTopLevelEntryRelationships(result)
                .FirstOrDefault(entry =>
                {
                    var entryConfigId = entry["part_config_id"]?.Value<string>();
                    if (!string.Equals(entryConfigId, partConfigId, StringComparison.OrdinalIgnoreCase))
                        return false;

                    var entryStatus = entry["entry_status"]?.Value<string>() ?? entry["state"]?.Value<string>();
                    return IsActiveEntryStatus(entryStatus);
                });

            return match?["id"]?.Value<string>();
        }

        private async Task<JObject> FindLibraryByNameAsync(
            string name,
            string excludeLibraryId,
            CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.LibraryItemType + "\" action=\"get\" " +
                "select=\"id,name\">" +
                "<name condition=\"eq\">" + Escape(name) + "</name>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                PartLibrarySchemaNames.LibraryItemType,
                null,
                ct).ConfigureAwait(false);

            foreach (var lib in EnumerateItems(result))
            {
                var libId = lib["id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(excludeLibraryId) ||
                    !string.Equals(libId, excludeLibraryId, StringComparison.OrdinalIgnoreCase))
                {
                    return lib;
                }
            }

            return null;
        }

        private async Task<AddPartToLibraryResult> TryAddPartViaServerMethodAsync(
            AddPartToLibraryRequest request,
            string partConfigId,
            CancellationToken ct)
        {
            var parameters = new Dictionary<string, string>
            {
                ["library_id"] = request.LibraryId,
                ["part_id"] = request.PartId,
                ["part_config_id"] = partConfigId,
                ["revision_policy"] = request.RevisionPolicy.ToString(),
                ["category"] = request.Category ?? string.Empty,
                ["tags"] = request.Tags ?? string.Empty,
                ["note"] = request.Note ?? string.Empty,
                ["source_project"] = request.SourceProject ?? string.Empty,
                ["source_commit"] = request.SourceCommit ?? string.Empty
            };

            if (request.RevisionPolicy == LibraryRevisionPolicy.Pinned)
            {
                parameters["pinned_part_id"] = request.PartId;
            }

            try
            {
                var result = await _aml.ApplyMethodAsync(PartLibrarySchemaNames.AddPartToLibraryMethodName, parameters, ct).ConfigureAwait(false);
                var entryId = result["entry_id"]?.Value<string>() ?? result["id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(entryId))
                {
                    return null;
                }

                return new AddPartToLibraryResult
                {
                    Success = true,
                    EntryId = entryId,
                    AlreadyExists = ParseBoolean(result["already_exists"]?.Value<string>())
                };
            }
            catch (ArasOperationException ex) when (CanFallbackToDirectAdd(ex, PartLibrarySchemaNames.AddPartToLibraryMethodName))
            {
                _logger.LogInformation(ex, "Server method {MethodName} is not available. Falling back to direct AML add.", PartLibrarySchemaNames.AddPartToLibraryMethodName);
                return null;
            }
        }

        private async Task PromoteEntryAsync(string entryId, string targetState, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");
            if (string.IsNullOrWhiteSpace(targetState))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Target state is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(ct).ConfigureAwait(false);

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"promote\" id=\"" + Escape(entryId) + "\">" +
                "<state>" + Escape(targetState) + "</state>" +
                "</Item>";

            await _aml.ApplyAmlAsync(
                aml,
                "promote",
                PartLibrarySchemaNames.EntryRelationshipType,
                entryId,
                ct).ConfigureAwait(false);

            var refreshed = await GetEntryRelationshipAsync(entryId, ct).ConfigureAwait(false);
            var actualState = refreshed["state"]?.Value<string>() ?? refreshed["entry_status"]?.Value<string>();
            if (!string.Equals(actualState, targetState, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "Lifecycle transition was not confirmed by Aras. Expected '" + targetState + "' but actual state is '" + (actualState ?? "(empty)") + "'.");
            }
        }

        private async Task<PrimaryCadInfo> GetPrimaryCadInfoAsync(string partId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partId))
                return PrimaryCadInfo.Empty;

            try
            {
                var relAml =
                    "<Item type=\"Part CAD\" action=\"get\" select=\"related_id\">" +
                    "<source_id>" + Escape(partId) + "</source_id>" +
                    "</Item>";

                var relResult = await _aml.ApplyAmlAsync(relAml, "get", "Part CAD", null, ct).ConfigureAwait(false);
                var rel = EnumerateItems(relResult).FirstOrDefault();
                var cadId = rel?["related_id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(cadId))
                    return new PrimaryCadInfo { Status = "No CAD" };

                var cad = await _aml.ApplyItemAsync(
                    "CAD",
                    cadId,
                    "get",
                    "id,item_number,name,state,locked_by_id,native_file",
                    ct).ConfigureAwait(false);

                return new PrimaryCadInfo
                {
                    CadId = cad["id"]?.Value<string>(),
                    FileName = cad["name"]?.Value<string>(),
                    State = cad["state"]?.Value<string>(),
                    LockedBy = cad["locked_by_id"]?.Value<string>(),
                    Status = string.IsNullOrWhiteSpace(cad["native_file"]?.Value<string>()) ? "No CAD" : "Available"
                };
            }
            catch (ArasOperationException ex) when (IsAuthOrPermissionFailure(ex))
            {
                throw;
            }
            catch (ArasOperationException ex)
            {
                _logger.LogDebug(ex, "Primary CAD info lookup failed for Part {PartId}.", partId);
                return new PrimaryCadInfo { Status = "No CAD" };
            }
            catch (Exception ex)
            {
                if (IsAuthOrPermissionFailure(ex))
                    throw;

                _logger.LogDebug(ex, "Primary CAD info lookup failed for Part {PartId}.", partId);
                return new PrimaryCadInfo { Status = "No CAD" };
            }
        }

        private static IEnumerable<JObject> EnumerateItems(JObject result)
        {
            if (result == null)
                yield break;

            if (result["Items"] is JArray items)
            {
                foreach (var token in items.OfType<JObject>())
                    yield return token;
                yield break;
            }

            if (result.HasValues)
                yield return result;
        }

        private IEnumerable<JObject> EnumerateTopLevelEntryRelationships(JObject result)
        {
            var seenEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in EnumerateItems(result))
            {
                var itemType = item["type"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(itemType) &&
                    !string.Equals(
                        itemType,
                        PartLibrarySchemaNames.EntryRelationshipType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entryId = item["id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(entryId))
                {
                    _logger.LogWarning(
                        "Ignored a top-level {EntryType} result because its relationship ID is blank.",
                        PartLibrarySchemaNames.EntryRelationshipType);
                    continue;
                }

                if (!seenEntryIds.Add(entryId))
                {
                    _logger.LogWarning(
                        "Ignored duplicate {EntryType} relationship ID {EntryId}.",
                        PartLibrarySchemaNames.EntryRelationshipType,
                        entryId);
                    continue;
                }

                yield return item;
            }
        }

        private static string NormalizePolicyFilter(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty);
        }

        private static bool IsActiveLibrary(string status)
        {
            return !string.Equals(status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase);
        }

        private static LibraryType ParseLibraryType(string value)
        {
            return Enum.TryParse(value, true, out LibraryType parsed) ? parsed : LibraryType.Team;
        }

        private static LibraryRevisionPolicy ParseRevisionPolicy(string value)
        {
            return Enum.TryParse(value, true, out LibraryRevisionPolicy parsed) ? parsed : LibraryRevisionPolicy.Pinned;
        }

        private static LibraryEntryStatus ParseEntryStatus(string value)
        {
            return Enum.TryParse(value, true, out LibraryEntryStatus parsed) ? parsed : LibraryEntryStatus.Draft;
        }

        private static bool IsActiveEntryStatus(string value)
        {
            return string.Equals(value, PartLibrarySchemaNames.EntryStatusDraft, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, PartLibrarySchemaNames.EntryStatusPendingReview, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, PartLibrarySchemaNames.EntryStatusPublished, StringComparison.OrdinalIgnoreCase);
        }

        private static LibraryEntryStatus GetEffectiveEntryStatus(string lifecycleState, string entryStatus)
        {
            if (!string.IsNullOrWhiteSpace(lifecycleState))
            {
                if (string.Equals(lifecycleState, PartLibrarySchemaNames.EntryLifecycleDraftState, StringComparison.OrdinalIgnoreCase))
                    return LibraryEntryStatus.Draft;
                if (string.Equals(lifecycleState, PartLibrarySchemaNames.EntryLifecyclePendingReviewState, StringComparison.OrdinalIgnoreCase))
                    return LibraryEntryStatus.PendingReview;
                if (string.Equals(lifecycleState, PartLibrarySchemaNames.EntryLifecyclePublishedState, StringComparison.OrdinalIgnoreCase))
                    return LibraryEntryStatus.Published;
                if (string.Equals(lifecycleState, PartLibrarySchemaNames.EntryLifecycleDeprecatedState, StringComparison.OrdinalIgnoreCase))
                    return LibraryEntryStatus.Deprecated;
            }

            return ParseEntryStatus(entryStatus);
        }

        private static bool ParseBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static DateTime? ParseDate(string value)
        {
            return DateTime.TryParse(value, out var parsed) ? parsed : (DateTime?)null;
        }

        private static bool Contains(string value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAuthOrPermissionFailure(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is ArasOperationException arasEx)
            {
                if (arasEx.ErrorCode == ArasErrorCode.AuthInvalid ||
                    arasEx.ErrorCode == ArasErrorCode.AuthExpired ||
                    arasEx.ErrorCode == ArasErrorCode.PermissionDenied)
                {
                    return true;
                }
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("unauthor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("not authorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEntryResolutionFailure(ArasOperationException ex)
        {
            if (ex == null)
                return false;

            return ex.ErrorCode == ArasErrorCode.ValidationFailed ||
                   ex.ErrorCode == ArasErrorCode.PartNotFound ||
                   ex.ErrorCode == ArasErrorCode.CadNotFound;
        }

        private static bool IsMissingUsageItemTypeError(Exception ex)
        {
            if (ex == null)
                return false;

            var message = ex.Message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var lower = message.ToLowerInvariant();
            var hasItemTypeName =
                lower.Contains("idea_partlibraryusage") ||
                lower.Contains("usage itemtype") ||
                lower.Contains("usage item type");

            if (!hasItemTypeName)
                return false;

            return lower.Contains("not deployed") ||
                   lower.Contains("does not exist") ||
                   lower.Contains("doesn't exist") ||
                   lower.Contains("not defined") ||
                   lower.Contains("undeployed");
        }

        private static string SanitizeForEntry(Exception ex)
        {
            if (ex == null)
                return "Library Entry resolution failed.";

            var message = ex.Message ?? "Library Entry resolution failed.";
            message = message.Replace("SOAP-ENV:", string.Empty).Trim();
            var lower = message.ToLowerInvariant();
            if (lower.Contains("bearer ") || lower.Contains("authorization:") || lower.Contains("access token"))
                return "Library Entry resolution failed.";

            return message;
        }

        private static bool CanFallbackToDirectAdd(Exception ex, string methodName)
        {
            if (ex == null)
                return false;

            if (IsAuthOrPermissionFailure(ex))
                return false;

            var message = ex.Message ?? string.Empty;
            return message.IndexOf(methodName ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   message.IndexOf("Method", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static JObject ValidateReusablePart(JObject part, string expectedConfigId, string subject)
        {
            if (part == null)
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, subject + " was not returned by Aras.");

            var partId = part["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(partId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, subject + " does not have a readable ID.");

            var configId = part["config_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(configId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, subject + " does not have a readable config_id.");

            if (!string.IsNullOrWhiteSpace(expectedConfigId) &&
                !string.Equals(configId, expectedConfigId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    subject + " config_id '" + configId + "' does not match Entry config_id '" + expectedConfigId + "'.");
            }

            var revision = part["major_rev"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(revision))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, subject + " does not have a readable major_rev.");

            var state = part["state"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    subject + " does not have a readable lifecycle state.");
            }

            if (PartLifecyclePolicy.IsPartObsolete(state))
            {
                throw new ArasOperationException(ArasErrorCode.ValidationFailed,
                    subject + " is in state '" + state + "' and cannot be reused.");
            }

            return part;
        }

        private static JObject SelectLatestPartVersion(IReadOnlyList<JObject> parts)
        {
            if (parts == null || parts.Count == 0)
                return null;

            return parts
                .OrderByDescending(part => ParseInt(part["generation"]?.Value<string>(), 0))
                .ThenByDescending(part => part["major_rev"]?.Value<string>() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static PartPickerSearchResultItem MapPartSearchItem(JObject item)
        {
            return new PartPickerSearchResultItem
            {
                PartId = item["id"]?.Value<string>(),
                ConfigId = item["config_id"]?.Value<string>(),
                PartNumber = item["item_number"]?.Value<string>(),
                Name = item["name"]?.Value<string>(),
                PartType = item["classification"]?.Value<string>(),
                MajorRev = item["major_rev"]?.Value<string>(),
                Generation = item["generation"]?.Value<string>(),
                LifecycleState = item["state"]?.Value<string>(),
                IsCurrent = ParseBoolean(item["is_current"]?.Value<string>()),
                IsReleased = string.Equals(item["state"]?.Value<string>(), PartLibrarySchemaNames.PartReleasedState, StringComparison.OrdinalIgnoreCase),
                CadStatus = "Unknown",
                ModifiedOn = item["created_on"]?.Value<string>()
            };
        }

        private static PartRevisionHistoryItem MapPartRevisionHistoryItem(JObject item)
        {
            if (item == null)
                return null;

            var itemType = item["type"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(itemType) &&
                !string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var partId = item["id"]?.Value<string>();
            var configId = item["config_id"]?.Value<string>();
            var majorRev = item["major_rev"]?.Value<string>();
            var lifecycleState = item["state"]?.Value<string>();
            var canPinReason = GetRevisionHistoryPinReason(partId, configId, majorRev, lifecycleState);

            return new PartRevisionHistoryItem
            {
                PartId = partId,
                ConfigId = configId,
                PartNumber = item["item_number"]?.Value<string>(),
                Name = item["name"]?.Value<string>(),
                PartType = item["classification"]?.Value<string>(),
                MajorRev = majorRev,
                Generation = item["generation"]?.Value<string>(),
                LifecycleState = lifecycleState,
                IsCurrent = ParseBoolean(item["is_current"]?.Value<string>()),
                IsReleased = string.Equals(lifecycleState, PartLibrarySchemaNames.PartReleasedState, StringComparison.OrdinalIgnoreCase),
                IsObsolete = PartLifecyclePolicy.IsPartObsolete(lifecycleState),
                CadStatus = "Unknown",
                ModifiedOn = ParseDate(item["modified_on"]?.Value<string>()),
                CreatedOn = ParseDate(item["created_on"]?.Value<string>()),
                CanPin = string.IsNullOrWhiteSpace(canPinReason),
                CannotPinReason = canPinReason
            };
        }

        private static string GetRevisionHistoryPinReason(string partId, string configId, string majorRev, string lifecycleState)
        {
            if (string.IsNullOrWhiteSpace(partId))
                return "Part ID is missing.";
            if (string.IsNullOrWhiteSpace(configId))
                return "config_id is missing.";
            if (string.IsNullOrWhiteSpace(majorRev))
                return "major_rev is missing.";
            if (string.IsNullOrWhiteSpace(lifecycleState))
                return "Lifecycle state is unreadable.";
            if (PartLifecyclePolicy.IsPartObsolete(lifecycleState))
                return "Part is obsolete and cannot be pinned.";

            return null;
        }

        private sealed class PartRevisionHistoryItemComparer : IComparer<PartRevisionHistoryItem>
        {
            public int Compare(PartRevisionHistoryItem x, PartRevisionHistoryItem y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x == null)
                    return 1;
                if (y == null)
                    return -1;

                var xGenerationNumeric = TryGetInt(x.Generation, out var xGeneration);
                var yGenerationNumeric = TryGetInt(y.Generation, out var yGeneration);
                if (xGenerationNumeric && yGenerationNumeric && xGeneration != yGeneration)
                    return yGeneration.CompareTo(xGeneration);

                var xDate = x.ModifiedOn ?? x.CreatedOn;
                var yDate = y.ModifiedOn ?? y.CreatedOn;
                if (xDate.HasValue || yDate.HasValue)
                {
                    var dateCompare = DateTime.Compare(yDate ?? DateTime.MinValue, xDate ?? DateTime.MinValue);
                    if (dateCompare != 0)
                        return dateCompare;
                }

                return string.Compare(y.MajorRev ?? string.Empty, x.MajorRev ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            private static bool TryGetInt(string value, out int result)
            {
                return int.TryParse(value, out result);
            }
        }

        private static string Escape(string value)
        {
            return System.Security.SecurityElement.Escape(value ?? string.Empty);
        }

        private sealed class PrimaryCadInfo
        {
            public static readonly PrimaryCadInfo Empty = new PrimaryCadInfo { Status = "No CAD" };

            public string CadId { get; set; }
            public string FileName { get; set; }
            public string State { get; set; }
            public string LockedBy { get; set; }
            public string Status { get; set; }
        }

        private sealed class UsageCountSnapshot
        {
            public bool IsAuthoritative { get; set; }

            public IReadOnlyDictionary<string, int> Counts { get; set; }
        }
    }
}
