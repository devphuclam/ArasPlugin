/*
 * Method: idea_RecordPartLibraryUsage
 * Type: Server-side C#
 *
 * Deployment:
 * - Method name: idea_RecordPartLibraryUsage
 * - Invoked from desktop connector via ApplyMethod
 * - Requires permission to get/add idea_PartLibraryUsage and edit idea_PartLibraryEntry
 *
 * Idempotency:
 * - The desktop sends an idempotency_key (SHA-256 hash of usage parameters).
 * - Before inserting a new Usage record, this Method queries by idempotency_key.
 * - If a matching Usage already exists, the Method returns that record's data
 *   and sets already_exists = 1. No duplicate Usage Item is added.
 * - A uniqueness constraint on idempotency_key is recommended when supported
 *   by the live Aras environment.
 *
 * Concurrency strategy:
 * - idea_PartLibraryUsage is the source of truth.
 * - usage_count returned by this Method is computed from Usage records after insert.
 * - last_used_on is updated on idea_PartLibraryEntry.
 * - usage_count on the Entry may be updated as a cache, but callers must not treat
 *   it as a guaranteed atomic counter under concurrent requests.
 */

Innovator inn = this.getInnovator();

string libraryEntryId = (this.getProperty("library_entry_id", "") ?? "").Trim();
string partId = (this.getProperty("part_id", "") ?? "").Trim();
string projectCode = (this.getProperty("project_code", "") ?? "").Trim();
string parentPartId = (this.getProperty("parent_part_id", "") ?? "").Trim();
string quantityRaw = (this.getProperty("quantity", "1") ?? "1").Trim();
string usedBy = (this.getProperty("used_by", "") ?? "").Trim();
string commitId = (this.getProperty("commit_id", "") ?? "").Trim();
string actionType = (this.getProperty("action_type", "") ?? "").Trim();
string idempotencyKey = (this.getProperty("idempotency_key", "") ?? "").Trim();

if (string.IsNullOrEmpty(libraryEntryId))
    return inn.newError("library_entry_id is required.");

if (string.IsNullOrEmpty(partId))
    return inn.newError("part_id is required.");

if (string.IsNullOrEmpty(idempotencyKey))
    return inn.newError("idempotency_key is required.");

if (idempotencyKey.Length > 128)
    return inn.newError("idempotency_key is too long.");

int quantity = 0;
if (!int.TryParse(quantityRaw, out quantity))
    return inn.newError("quantity must be an integer.");

if (quantity <= 0)
    return inn.newError("quantity must be greater than zero.");

// Inline normalization of actionType
string normalizedActionType = null;
if (string.IsNullOrEmpty(actionType))
{
    normalizedActionType = "ReusedFromLibrary";
}
else
{
    string trimmedAction = actionType.Trim();
    if (string.Equals(trimmedAction, "ReusedFromLibrary", System.StringComparison.OrdinalIgnoreCase))
        normalizedActionType = "ReusedFromLibrary";
    else if (string.Equals(trimmedAction, "AddedToProject", System.StringComparison.OrdinalIgnoreCase))
        normalizedActionType = "AddedToProject";
    else if (string.Equals(trimmedAction, "UpdatedInProject", System.StringComparison.OrdinalIgnoreCase))
        normalizedActionType = "UpdatedInProject";
}

if (normalizedActionType == null)
    return inn.newError("action_type is not supported.");

// Validate Entry exists
Item entry = inn.newItem("idea_PartLibraryEntry", "get");
entry.setID(libraryEntryId);
entry.setAttribute("select", "id,state,entry_status,part_config_id");
entry = entry.apply();

if (entry.isError() || entry.getItemCount() != 1)
    return inn.newError("Library Entry was not found.");

string entryState = entry.getProperty("state", "");
string entryStatus = entry.getProperty("entry_status", "");
if (string.Equals(entryState, "Deprecated", System.StringComparison.OrdinalIgnoreCase) ||
    string.Equals(entryStatus, "Deprecated", System.StringComparison.OrdinalIgnoreCase))
{
    return inn.newError("Deprecated Library Entry cannot record usage.");
}

string entryConfigId = entry.getProperty("part_config_id", "");
if (string.IsNullOrEmpty(entryConfigId))
    return inn.newError("Library Entry does not have a readable part_config_id.");

// Validate Part exists and config_id matches Entry
Item part = inn.newItem("Part", "get");
part.setID(partId);
part.setAttribute("select", "id,config_id");
part = part.apply();

if (part.isError() || part.getItemCount() != 1)
    return inn.newError("Part was not found.");

string partConfigId = part.getProperty("config_id", "");
if (string.IsNullOrEmpty(partConfigId))
    return inn.newError("Part does not have a readable config_id.");

if (!string.Equals(partConfigId, entryConfigId, System.StringComparison.OrdinalIgnoreCase))
{
    return inn.newError("Part config_id does not match Library Entry part_config_id.");
}

// Validate parent_part_id exists when supplied
if (!string.IsNullOrEmpty(parentPartId))
{
    Item parentPart = inn.newItem("Part", "get");
    parentPart.setID(parentPartId);
    parentPart.setAttribute("select", "id");
    parentPart = parentPart.apply();

    if (parentPart.isError() || parentPart.getItemCount() != 1)
        return inn.newError("parent_part_id does not reference an existing Part.");
}

// Query existing Usage by idempotency_key BEFORE attempting to add
Item existingUsage = inn.newItem("idea_PartLibraryUsage", "get");
existingUsage.setProperty("idempotency_key", idempotencyKey);
existingUsage.setAttribute("select", "id,library_entry_id,part_id,idempotency_key,created_on");
existingUsage.setAttribute("maxRecords", "1");
existingUsage = existingUsage.apply();

string existingUsageErrorCode = "";
try { existingUsageErrorCode = existingUsage.getErrorCode() + ""; } catch { existingUsageErrorCode = ""; }

if (existingUsage.isError() && existingUsageErrorCode != "0")
    return existingUsage;

if (existingUsage.getItemCount() >= 1)
{
    // Existing Usage found - return already_exists without creating a duplicate
    Item existingUsageItem = existingUsage.getItemByIndex(0);
    string existingUsageId = existingUsageItem.getProperty("id", "");
    string existingEntryId = existingUsageItem.getProperty("library_entry_id", "");
    string existingPartId = existingUsageItem.getProperty("part_id", "");
    string existingKey = existingUsageItem.getProperty("idempotency_key", "");

    if (!string.Equals(existingEntryId, libraryEntryId, System.StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(existingPartId, partId, System.StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(existingKey, idempotencyKey, System.StringComparison.Ordinal))
    {
        return inn.newError("Existing Usage record does not match the submitted idempotency key context.");
    }

    // Calculate authoritative count for this entry
    Item countQuery = inn.newItem("idea_PartLibraryUsage", "get");
    countQuery.setProperty("library_entry_id", libraryEntryId);
countQuery.setAttribute("select", "id");
countQuery = countQuery.apply();
string countQueryErrorCode = "";
try { countQueryErrorCode = countQuery.getErrorCode() + ""; } catch { countQueryErrorCode = ""; }

if (countQuery.isError() && countQueryErrorCode != "0")
    return countQuery;

    int authoritativeCount = countQuery.getItemCount();

    string existingLastUsedOn = existingUsageItem.getProperty("created_on", "");

    Item result = inn.newResult("");
    result.setProperty("usage_id", existingUsageId);
    result.setProperty("usage_count", authoritativeCount.ToString());
    result.setProperty("last_used_on", existingLastUsedOn);
    result.setProperty("already_exists", "1");
    result.setProperty("idempotency_key", idempotencyKey);
    return result;
}

// No existing Usage found - create one
Item usage = inn.newItem("idea_PartLibraryUsage", "add");
usage.setProperty("library_entry_id", libraryEntryId);
usage.setProperty("part_id", partId);
usage.setProperty("quantity", quantity.ToString());
usage.setProperty("action_type", normalizedActionType);
usage.setProperty("idempotency_key", idempotencyKey);

if (!string.IsNullOrEmpty(projectCode))
    usage.setProperty("project_code", projectCode);
if (!string.IsNullOrEmpty(parentPartId))
    usage.setProperty("parent_part_id", parentPartId);
if (!string.IsNullOrEmpty(usedBy))
    usage.setProperty("used_by", usedBy);
if (!string.IsNullOrEmpty(commitId))
    usage.setProperty("commit_id", commitId);

usage = usage.apply();
if (usage.isError())
    return usage;

// Calculate authoritative usage count
Item usageCountQuery = inn.newItem("idea_PartLibraryUsage", "get");
usageCountQuery.setProperty("library_entry_id", libraryEntryId);
usageCountQuery.setAttribute("select", "id");
usageCountQuery = usageCountQuery.apply();
string usageCountQueryErrorCode = "";
try { usageCountQueryErrorCode = usageCountQuery.getErrorCode() + ""; } catch { usageCountQueryErrorCode = ""; }

if (usageCountQuery.isError() && usageCountQueryErrorCode != "0")
    return usageCountQuery;

int usageCount = usageCountQuery.getItemCount();

// Update Entry last_used_on and usage_count cache
string currentDate = inn.getCurrentDate();
Item updateEntry = inn.newItem("idea_PartLibraryEntry", "edit");
updateEntry.setID(libraryEntryId);
updateEntry.setProperty("last_used_on", currentDate);
updateEntry.setProperty("usage_count", usageCount.ToString());
updateEntry = updateEntry.apply();
if (updateEntry.isError())
    return updateEntry;

// Return result
Item result = inn.newResult("");
result.setProperty("usage_id", usage.getID());
result.setProperty("usage_count", usageCount.ToString());
result.setProperty("last_used_on", currentDate);
result.setProperty("already_exists", "0");
result.setProperty("idempotency_key", idempotencyKey);
return result;
