/*
 * Method: idea_RecordPartLibraryUsage
 * Type: Server-side C#
 *
 * Deployment:
 * - Method name: idea_RecordPartLibraryUsage
 * - Invoked from desktop connector via ApplyMethod
 * - Requires permission to get/add idea_PartLibraryUsage
 * - Requires permission to get/edit idea_PartLibraryEntry
 * - Requires permission to get Part
 *
 * Idempotency:
 * - The desktop sends an idempotency_key.
 * - Before adding a Usage record, this Method queries by idempotency_key.
 * - If a matching Usage already exists, it returns already_exists = 1.
 *
 * Concurrency:
 * - Query-before-add prevents normal retry duplicates.
 * - A unique constraint on idempotency_key is recommended for complete
 *   protection against concurrent requests.
 *
 * Usage source:
 * - idea_PartLibraryUsage is the authoritative source.
 * - idea_PartLibraryEntry.usage_count is maintained only as a cache.
 */

Innovator inn = this.getInnovator();

/*
 * Read request properties.
 */
string libraryEntryId =
    (this.getProperty("library_entry_id", "") ?? "").Trim();

string partId =
    (this.getProperty("part_id", "") ?? "").Trim();

string projectCode =
    (this.getProperty("project_code", "") ?? "").Trim();

string parentPartId =
    (this.getProperty("parent_part_id", "") ?? "").Trim();

string quantityRaw =
    (this.getProperty("quantity", "1") ?? "1").Trim();

string usedBy =
    (this.getProperty("used_by", "") ?? "").Trim();

string commitId =
    (this.getProperty("commit_id", "") ?? "").Trim();

string actionType =
    (this.getProperty("action_type", "") ?? "").Trim();

string idempotencyKey =
    (this.getProperty("idempotency_key", "") ?? "").Trim();

/*
 * Validate required request values.
 */
if (string.IsNullOrEmpty(libraryEntryId))
{
    return inn.newError("library_entry_id is required.");
}

if (string.IsNullOrEmpty(partId))
{
    return inn.newError("part_id is required.");
}

if (string.IsNullOrEmpty(idempotencyKey))
{
    return inn.newError("idempotency_key is required.");
}

if (idempotencyKey.Length > 128)
{
    return inn.newError("idempotency_key is too long.");
}

/*
 * Validate quantity.
 */
int quantity = 0;

if (!int.TryParse(quantityRaw, out quantity))
{
    return inn.newError("quantity must be an integer.");
}

if (quantity <= 0)
{
    return inn.newError("quantity must be greater than zero.");
}

/*
 * Normalize and validate action_type.
 */
string normalizedActionType = null;

if (string.IsNullOrEmpty(actionType))
{
    normalizedActionType = "ReusedFromLibrary";
}
else
{
    string trimmedAction = actionType.Trim();

    if (string.Equals(
        trimmedAction,
        "ReusedFromLibrary",
        System.StringComparison.OrdinalIgnoreCase))
    {
        normalizedActionType = "ReusedFromLibrary";
    }
    else if (string.Equals(
        trimmedAction,
        "AddedToProject",
        System.StringComparison.OrdinalIgnoreCase))
    {
        normalizedActionType = "AddedToProject";
    }
    else if (string.Equals(
        trimmedAction,
        "UpdatedInProject",
        System.StringComparison.OrdinalIgnoreCase))
    {
        normalizedActionType = "UpdatedInProject";
    }
}

if (normalizedActionType == null)
{
    return inn.newError("action_type is not supported.");
}

/*
 * Validate Library Entry.
 */
Item entry = inn.newItem(
    "idea_PartLibraryEntry",
    "get");

entry.setID(libraryEntryId);
entry.setAttribute(
    "select",
    "id,state,entry_status,part_config_id");

entry = entry.apply();

if (entry.isError() || entry.getItemCount() != 1)
{
    return inn.newError("Library Entry was not found.");
}

string entryState =
    entry.getProperty("state", "");

string entryStatus =
    entry.getProperty("entry_status", "");

if (string.Equals(
        entryState,
        "Deprecated",
        System.StringComparison.OrdinalIgnoreCase) ||
    string.Equals(
        entryStatus,
        "Deprecated",
        System.StringComparison.OrdinalIgnoreCase))
{
    return inn.newError(
        "Deprecated Library Entry cannot record usage.");
}

string entryConfigId =
    entry.getProperty("part_config_id", "");

if (string.IsNullOrEmpty(entryConfigId))
{
    return inn.newError(
        "Library Entry does not have a readable part_config_id.");
}

/*
 * Validate resolved Part.
 */
Item part = inn.newItem(
    "Part",
    "get");

part.setID(partId);
part.setAttribute(
    "select",
    "id,config_id");

part = part.apply();

if (part.isError() || part.getItemCount() != 1)
{
    return inn.newError("Part was not found.");
}

string partConfigId =
    part.getProperty("config_id", "");

if (string.IsNullOrEmpty(partConfigId))
{
    return inn.newError(
        "Part does not have a readable config_id.");
}

if (!string.Equals(
        partConfigId,
        entryConfigId,
        System.StringComparison.OrdinalIgnoreCase))
{
    return inn.newError(
        "Part config_id does not match Library Entry part_config_id.");
}

/*
 * Validate parent Part when supplied.
 */
if (!string.IsNullOrEmpty(parentPartId))
{
    Item parentPart = inn.newItem(
        "Part",
        "get");

    parentPart.setID(parentPartId);
    parentPart.setAttribute(
        "select",
        "id");

    parentPart = parentPart.apply();

    if (parentPart.isError() ||
        parentPart.getItemCount() != 1)
    {
        return inn.newError(
            "parent_part_id does not reference an existing Part.");
    }
}

/*
 * Check whether the same logical Usage was already recorded.
 */
Item existingUsage = inn.newItem(
    "idea_PartLibraryUsage",
    "get");

existingUsage.setProperty(
    "idempotency_key",
    idempotencyKey);

existingUsage.setAttribute(
    "select",
    "id,library_entry_id,part_id,idempotency_key,created_on");

existingUsage.setAttribute(
    "maxRecords",
    "1");

existingUsage = existingUsage.apply();

/*
 * Aras may represent an empty get result using error code 0.
 * Error code 0 means no matching Item was found and is not treated
 * as an operational failure.
 */
string existingUsageErrorCode = "";

try
{
    existingUsageErrorCode =
        existingUsage.getErrorCode() + "";
}
catch
{
    existingUsageErrorCode = "";
}

if (existingUsage.isError() &&
    existingUsageErrorCode != "0")
{
    return existingUsage;
}

/*
 * Return the existing Usage for an idempotent retry.
 */
if (existingUsage.getItemCount() >= 1)
{
    Item existingUsageItem =
        existingUsage.getItemByIndex(0);

    string existingUsageId =
        existingUsageItem.getProperty("id", "");

    string existingEntryId =
        existingUsageItem.getProperty(
            "library_entry_id",
            "");

    string existingPartId =
        existingUsageItem.getProperty(
            "part_id",
            "");

    string existingKey =
        existingUsageItem.getProperty(
            "idempotency_key",
            "");

    if (!string.Equals(
            existingEntryId,
            libraryEntryId,
            System.StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(
            existingPartId,
            partId,
            System.StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(
            existingKey,
            idempotencyKey,
            System.StringComparison.Ordinal))
    {
        return inn.newError(
            "Existing Usage record does not match the submitted idempotency key context.");
    }

    /*
     * Calculate authoritative count for this Library Entry.
     */
    Item countQuery = inn.newItem(
        "idea_PartLibraryUsage",
        "get");

    countQuery.setProperty(
        "library_entry_id",
        libraryEntryId);

    countQuery.setAttribute(
        "select",
        "id");

    countQuery = countQuery.apply();

    string countQueryErrorCode = "";

    try
    {
        countQueryErrorCode =
            countQuery.getErrorCode() + "";
    }
    catch
    {
        countQueryErrorCode = "";
    }

    if (countQuery.isError() &&
        countQueryErrorCode != "0")
    {
        return countQuery;
    }

    int authoritativeCount =
        countQuery.getItemCount();

    string existingLastUsedOn =
        existingUsageItem.getProperty(
            "created_on",
            "");

    Item existingResult =
        inn.newResult("");

    existingResult.setProperty(
        "usage_id",
        existingUsageId);

    existingResult.setProperty(
        "usage_count",
        authoritativeCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture));

    existingResult.setProperty(
        "last_used_on",
        existingLastUsedOn);

    existingResult.setProperty(
        "already_exists",
        "1");

    existingResult.setProperty(
        "idempotency_key",
        idempotencyKey);

    return existingResult;
}

/*
 * No existing Usage was found. Add a new Usage Item.
 */
Item usage = inn.newItem(
    "idea_PartLibraryUsage",
    "add");

usage.setProperty(
    "library_entry_id",
    libraryEntryId);

usage.setProperty(
    "part_id",
    partId);

usage.setProperty(
    "quantity",
    quantity.ToString(
        System.Globalization.CultureInfo.InvariantCulture));

usage.setProperty(
    "action_type",
    normalizedActionType);

usage.setProperty(
    "idempotency_key",
    idempotencyKey);

if (!string.IsNullOrEmpty(projectCode))
{
    usage.setProperty(
        "project_code",
        projectCode);
}

if (!string.IsNullOrEmpty(parentPartId))
{
    usage.setProperty(
        "parent_part_id",
        parentPartId);
}

if (!string.IsNullOrEmpty(usedBy))
{
    usage.setProperty(
        "used_by",
        usedBy);
}

if (!string.IsNullOrEmpty(commitId))
{
    usage.setProperty(
        "commit_id",
        commitId);
}

usage = usage.apply();

if (usage.isError())
{
    return usage;
}

/*
 * Calculate authoritative count after the Usage Item was added.
 */
Item usageCountQuery = inn.newItem(
    "idea_PartLibraryUsage",
    "get");

usageCountQuery.setProperty(
    "library_entry_id",
    libraryEntryId);

usageCountQuery.setAttribute(
    "select",
    "id");

usageCountQuery = usageCountQuery.apply();

string usageCountQueryErrorCode = "";

try
{
    usageCountQueryErrorCode =
        usageCountQuery.getErrorCode() + "";
}
catch
{
    usageCountQueryErrorCode = "";
}

if (usageCountQuery.isError() &&
    usageCountQueryErrorCode != "0")
{
    return usageCountQuery;
}

int usageCount =
    usageCountQuery.getItemCount();

/*
 * Create a UTC date string compatible with the Aras Date property.
 */
string currentDate =
    System.DateTime.UtcNow.ToString(
        "yyyy-MM-ddTHH:mm:ss",
        System.Globalization.CultureInfo.InvariantCulture);

/*
 * Update Entry cache and last-used timestamp.
 */
Item updateEntry = inn.newItem(
    "idea_PartLibraryEntry",
    "edit");

updateEntry.setID(libraryEntryId);

updateEntry.setProperty(
    "last_used_on",
    currentDate);

updateEntry.setProperty(
    "usage_count",
    usageCount.ToString(
        System.Globalization.CultureInfo.InvariantCulture));

updateEntry = updateEntry.apply();

if (updateEntry.isError())
{
    return updateEntry;
}

/*
 * Return the newly created Usage result.
 */
Item newUsageResult =
    inn.newResult("");

newUsageResult.setProperty(
    "usage_id",
    usage.getID());

newUsageResult.setProperty(
    "usage_count",
    usageCount.ToString(
        System.Globalization.CultureInfo.InvariantCulture));

newUsageResult.setProperty(
    "last_used_on",
    currentDate);

newUsageResult.setProperty(
    "already_exists",
    "0");

newUsageResult.setProperty(
    "idempotency_key",
    idempotencyKey);

return newUsageResult;