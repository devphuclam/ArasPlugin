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
        private ArasAmlClient _aml;
        private string _database;
        private bool? _schemaAvailable;
        private bool _disposed;

        public HttpPartLibraryClient(ArasClientOptions options, ILogger<HttpPartLibraryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
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

        public async Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(CancellationToken cancellationToken)
        {
            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.LibraryItemType + "\" action=\"get\" " +
                "select=\"id,name,description,library_type,status,default_revision_policy,is_public\">" +
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
                    IsPublic = ParseBoolean(item["is_public"]?.Value<string>())
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
                query = query.Where(entry => string.Equals(entry.LifecycleState, request.StateFilter, StringComparison.OrdinalIgnoreCase));

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
            return await MapEntryDetailsAsync(entryItem, cancellationToken).ConfigureAwait(false);
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

            var partConfigId = request.PartConfigId;
            if (string.IsNullOrWhiteSpace(partConfigId))
            {
                var partItem = await GetPartAsync(request.PartId, cancellationToken).ConfigureAwait(false);
                partConfigId = partItem["config_id"]?.Value<string>();
            }

            var serverMethodResult = await TryAddPartViaServerMethodAsync(
                request,
                partConfigId,
                cancellationToken).ConfigureAwait(false);
            if (serverMethodResult != null)
            {
                return serverMethodResult;
            }

            var duplicateId = await FindDuplicateEntryIdAsync(
                request.LibraryId,
                partConfigId,
                request.RevisionPolicy,
                request.PartId,
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

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"add\">" +
                "<source_id>" + Escape(request.LibraryId) + "</source_id>" +
                "<related_id>" + Escape(request.PartId) + "</related_id>" +
                "<part_config_id>" + Escape(partConfigId) + "</part_config_id>" +
                "<revision_policy>" + Escape(request.RevisionPolicy.ToString()) + "</revision_policy>" +
                "<entry_status>" + PartLibrarySchemaNames.EntryStatusDraft + "</entry_status>" +
                "<category>" + Escape(request.Category) + "</category>" +
                "<tags>" + Escape(request.Tags) + "</tags>" +
                "<note>" + Escape(request.Note) + "</note>" +
                "<source_project>" + Escape(request.SourceProject) + "</source_project>" +
                "<source_commit>" + Escape(request.SourceCommit) + "</source_commit>" +
                (request.RevisionPolicy == LibraryRevisionPolicy.Pinned
                    ? "<pinned_part_id>" + Escape(request.PartId) + "</pinned_part_id>"
                    : string.Empty) +
                "</Item>";

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
            if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(targetLibraryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "EntryId and targetLibraryId are required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"edit\" id=\"" + Escape(entryId) + "\">" +
                "<source_id>" + Escape(targetLibraryId) + "</source_id>" +
                "</Item>";

            await _aml.ApplyAmlAsync(
                aml,
                "edit",
                PartLibrarySchemaNames.EntryRelationshipType,
                entryId,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Library entry ID is required.");

            EnsureAuthenticated();
            await EnsureSchemaAvailableAsync(cancellationToken).ConfigureAwait(false);

            var entry = await GetEntryRelationshipAsync(entryId, cancellationToken).ConfigureAwait(false);
            var requestedPolicy = policy;
            if (!Enum.TryParse(entry["revision_policy"]?.Value<string>(), true, out requestedPolicy))
                requestedPolicy = policy;

            var pinnedPartId = entry["pinned_part_id"]?.Value<string>();
            var relatedPartId = entry["related_id"]?.Value<string>();
            var configId = entry["part_config_id"]?.Value<string>();

            JObject resolvedPart;
            if (requestedPolicy == LibraryRevisionPolicy.Pinned && !string.IsNullOrWhiteSpace(pinnedPartId))
            {
                resolvedPart = await GetPartAsync(pinnedPartId, cancellationToken).ConfigureAwait(false);
            }
            else if (requestedPolicy == LibraryRevisionPolicy.LatestReleased)
            {
                resolvedPart = await ResolveLatestReleasedPartAsync(configId, relatedPartId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                resolvedPart = await ResolveCurrentPartAsync(configId, relatedPartId, cancellationToken).ConfigureAwait(false);
            }

            var cadInfo = await GetPrimaryCadInfoAsync(resolvedPart["id"]?.Value<string>(), cancellationToken).ConfigureAwait(false);
            var latestReleased = await GetLatestReleasedPartAsync(configId, relatedPartId, cancellationToken).ConfigureAwait(false);
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

            var aml =
                "<Item type=\"Part BOM\" action=\"get\" select=\"quantity,source_id,related_id\">" +
                "<related_id>" + Escape(partId) + "</related_id>" +
                "</Item>";

            var result = await _aml.ApplyAmlAsync(
                aml,
                "get",
                "Part BOM",
                null,
                cancellationToken).ConfigureAwait(false);

            var list = new List<PartWhereUsedItem>();
            foreach (var rel in EnumerateItems(result))
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
                    Quantity = ParseInt(rel["quantity"]?.Value<string>(), 1)
                });
            }

            return list;
        }

        public async Task RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LibraryEntryId))
                return;

            EnsureAuthenticated();
            var hasUsageSchema = await ItemTypeExistsAsync(PartLibrarySchemaNames.UsageItemType, cancellationToken).ConfigureAwait(false);
            if (!hasUsageSchema)
            {
                _logger.LogWarning(
                    "Part Library usage record skipped: ItemType '{ItemType}' is not deployed on the Aras server. " +
                    "Create it manually to enable usage tracking.",
                    PartLibrarySchemaNames.UsageItemType);
                return;
            }

            var usageCreated = false;
            var usedByValue = (request.UsedBy ?? "unknown").Trim();

            if (usedByValue.Length > 0)
            {
                usageCreated = await TryCreateUsageItemAsync(request, usedByValue, cancellationToken).ConfigureAwait(false);
            }

            if (!usageCreated && usedByValue.Length > 0)
            {
                usageCreated = await TryCreateUsageItemWithoutUsedByAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (!usageCreated)
            {
                usageCreated = await TryCreateUsageItemAsync(request, null, cancellationToken).ConfigureAwait(false);
            }

            if (usageCreated)
            {
                await TryUpdateEntryLastUsedOnAsync(request.LibraryEntryId, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<bool> TryCreateUsageItemAsync(LibraryUsageRequest request, string usedBy, CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.UsageItemType + "\" action=\"add\">" +
                "<library_entry_id>" + Escape(request.LibraryEntryId) + "</library_entry_id>" +
                "<part_id>" + Escape(request.PartId) + "</part_id>" +
                "<project_code>" + Escape(request.ProjectCode) + "</project_code>" +
                "<parent_part_id>" + Escape(request.ParentPartId) + "</parent_part_id>" +
                "<quantity>" + request.Quantity + "</quantity>" +
                (usedBy != null
                    ? "<used_by>" + Escape(usedBy) + "</used_by>"
                    : string.Empty) +
                "<commit_id>" + Escape(request.CommitId) + "</commit_id>" +
                "<action_type>" + Escape(request.ActionType) + "</action_type>" +
                "</Item>";

            try
            {
                await _aml.ApplyAmlAsync(
                    aml,
                    "add",
                    PartLibrarySchemaNames.UsageItemType,
                    null,
                    ct).ConfigureAwait(false);
                return true;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.ValidationFailed && usedBy != null)
            {
                _logger.LogWarning(
                    "Part Library usage record: 'used_by' property may not be deployed on '{ItemType}'. " +
                    "Configure 'used_by' as a string property on the ItemType.",
                    PartLibrarySchemaNames.UsageItemType);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Part Library usage record failed for entry {EntryId}.", request.LibraryEntryId);
                return false;
            }
        }

        private async Task<bool> TryCreateUsageItemWithoutUsedByAsync(LibraryUsageRequest request, CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.UsageItemType + "\" action=\"add\">" +
                "<library_entry_id>" + Escape(request.LibraryEntryId) + "</library_entry_id>" +
                "<part_id>" + Escape(request.PartId) + "</part_id>" +
                "<project_code>" + Escape(request.ProjectCode) + "</project_code>" +
                "<parent_part_id>" + Escape(request.ParentPartId) + "</parent_part_id>" +
                "<quantity>" + request.Quantity + "</quantity>" +
                "<commit_id>" + Escape(request.CommitId) + "</commit_id>" +
                "<action_type>" + Escape(request.ActionType) + "</action_type>" +
                "</Item>";

            try
            {
                await _aml.ApplyAmlAsync(
                    aml,
                    "add",
                    PartLibrarySchemaNames.UsageItemType,
                    null,
                    ct).ConfigureAwait(false);
                _logger.LogWarning(
                    "Part Library usage record succeeded without 'used_by' for entry {EntryId}. " +
                    "Configure 'used_by' as a string property on the ItemType.",
                    PartLibrarySchemaNames.UsageItemType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Part Library usage record failed for entry {EntryId} (retry without used_by).", request.LibraryEntryId);
                return false;
            }
        }

        private async Task TryUpdateEntryLastUsedOnAsync(string entryId, CancellationToken ct)
        {
            try
            {
                var entryUpdateAml =
                    "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"edit\" id=\"" + Escape(entryId) + "\">" +
                    "<last_used_on>" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") + "</last_used_on>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(
                    entryUpdateAml,
                    "edit",
                    PartLibrarySchemaNames.EntryRelationshipType,
                    entryId,
                    ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.ValidationFailed)
            {
                _logger.LogWarning(
                    "Part Library entry last_used_on update skipped: property may not be deployed on '{ItemType}'. " +
                    "Configure 'last_used_on' (date) on the ItemType. " +
                    "usage_count requires a server-side Method or Event to increment atomically.",
                    PartLibrarySchemaNames.EntryRelationshipType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Part Library entry last_used_on update failed for entry {EntryId}.", entryId);
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

            return EnumerateItems(result).Count();
        }

        private async Task<List<PartLibraryEntrySummary>> LoadEntrySummariesAsync(string libraryId, CancellationToken ct)
        {
            var aml =
                "<Item type=\"" + PartLibrarySchemaNames.EntryRelationshipType + "\" action=\"get\" " +
                "select=\"id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,pinned_revision,entry_status,category,tags,note,source_project,source_commit,usage_count,last_used_on\">" +
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

            var summaries = new List<PartLibraryEntrySummary>();
            foreach (var rel in EnumerateItems(result))
            {
                var summary = await MapEntrySummaryAsync(rel, ct).ConfigureAwait(false);
                if (summary != null)
                    summaries.Add(summary);
            }

            return summaries;
        }

        private async Task<PartLibraryEntrySummary> MapEntrySummaryAsync(JObject relationship, CancellationToken ct)
        {
            var partId = relationship["related_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(partId))
                return null;

            var part = await GetPartAsync(partId, ct).ConfigureAwait(false);
            var libraryId = relationship["source_id"]?.Value<string>();
            var library = !string.IsNullOrWhiteSpace(libraryId)
                ? await GetLibraryAsync(libraryId, ct).ConfigureAwait(false)
                : null;
            var cad = await GetPrimaryCadInfoAsync(partId, ct).ConfigureAwait(false);
            var latestReleased = await GetLatestReleasedPartAsync(part["config_id"]?.Value<string>(), partId, ct).ConfigureAwait(false);

            return new PartLibraryEntrySummary
            {
                EntryId = relationship["id"]?.Value<string>(),
                LibraryId = libraryId,
                LibraryName = library?["name"]?.Value<string>(),
                PartId = part["id"]?.Value<string>(),
                PartConfigId = part["config_id"]?.Value<string>() ?? relationship["part_config_id"]?.Value<string>(),
                PartNumber = part["item_number"]?.Value<string>(),
                PartName = part["name"]?.Value<string>(),
                PartType = part["classification"]?.Value<string>(),
                Revision = part["major_rev"]?.Value<string>(),
                LifecycleState = part["state"]?.Value<string>(),
                RevisionPolicy = ParseRevisionPolicy(relationship["revision_policy"]?.Value<string>()),
                EntryStatus = ParseEntryStatus(relationship["entry_status"]?.Value<string>()),
                CadStatus = cad.Status,
                UsageCount = ParseInt(relationship["usage_count"]?.Value<string>(), 0),
                HasNewerReleasedRevision = latestReleased != null &&
                                          !string.Equals(latestReleased["id"]?.Value<string>(), part["id"]?.Value<string>(), StringComparison.OrdinalIgnoreCase),
                IsDeprecated = string.Equals(
                    relationship["entry_status"]?.Value<string>(),
                    PartLibrarySchemaNames.EntryStatusDeprecated,
                    StringComparison.OrdinalIgnoreCase),
                LastUsedOn = ParseDate(relationship["last_used_on"]?.Value<string>())
            };
        }

        private async Task<PartLibraryEntryDetails> MapEntryDetailsAsync(JObject relationship, CancellationToken ct)
        {
            var summary = await MapEntrySummaryAsync(relationship, ct).ConfigureAwait(false);
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
                HasNewerReleasedRevision = summary.HasNewerReleasedRevision
            };
        }

        private async Task<JObject> GetEntryRelationshipAsync(string entryId, CancellationToken ct)
        {
            var result = await _aml.ApplyItemAsync(
                PartLibrarySchemaNames.EntryRelationshipType,
                entryId,
                "get",
                "id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,pinned_revision,entry_status,category,tags,note,source_project,source_commit,usage_count,last_used_on",
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

        private async Task<JObject> GetPartAsync(string partId, CancellationToken ct)
        {
            return await _aml.ApplyItemAsync(
                "Part",
                partId,
                "get",
                "id,config_id,item_number,name,classification,major_rev,state,generation",
                ct).ConfigureAwait(false);
        }

        private async Task<JObject> ResolveCurrentPartAsync(string configId, string fallbackPartId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(configId))
            {
                var latestReleased = await GetLatestReleasedPartAsync(configId, fallbackPartId, ct).ConfigureAwait(false);
                if (latestReleased != null)
                    return latestReleased;

                var currentAml =
                    "<Item type=\"Part\" action=\"get\" select=\"id,config_id,item_number,name,classification,major_rev,state,generation\">" +
                    "<config_id>" + Escape(configId) + "</config_id>" +
                    "<is_current>1</is_current>" +
                    "</Item>";

                var current = await _aml.ApplyAmlAsync(currentAml, "get", "Part", null, ct).ConfigureAwait(false);
                var currentItem = EnumerateItems(current).FirstOrDefault();
                if (currentItem != null)
                    return currentItem;
            }

            return await GetPartAsync(fallbackPartId, ct).ConfigureAwait(false);
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

        private async Task<string> FindDuplicateEntryIdAsync(
            string libraryId,
            string partConfigId,
            LibraryRevisionPolicy policy,
            string pinnedPartId,
            CancellationToken ct)
        {
            var entries = await LoadEntrySummariesAsync(libraryId, ct).ConfigureAwait(false);
            var match = entries.FirstOrDefault(entry =>
                string.Equals(entry.PartConfigId, partConfigId, StringComparison.OrdinalIgnoreCase) &&
                entry.RevisionPolicy == policy &&
                (policy != LibraryRevisionPolicy.Pinned ||
                 string.Equals(entry.PartId, pinnedPartId, StringComparison.OrdinalIgnoreCase)));

            return match?.EntryId;
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
            catch (ArasOperationException ex) when (CanFallbackToDirectAdd(ex))
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

        private static bool CanFallbackToDirectAdd(Exception ex)
        {
            if (ex == null)
                return false;

            if (IsAuthOrPermissionFailure(ex))
                return false;

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("Method", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0);
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
    }
}
