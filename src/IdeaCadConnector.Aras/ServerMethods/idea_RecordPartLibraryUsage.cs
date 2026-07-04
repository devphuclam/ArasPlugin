/*
 * Method: idea_RecordPartLibraryUsage
 * Type: Server-side C#
 *
 * Deployment:
 * - Method name: idea_RecordPartLibraryUsage
 * - Invoked from desktop connector via ApplyMethod
 * - Requires permission to add idea_PartLibraryUsage and edit idea_PartLibraryEntry
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

if (string.IsNullOrEmpty(libraryEntryId))
    return inn.newError("library_entry_id is required.");

if (string.IsNullOrEmpty(partId))
    return inn.newError("part_id is required.");

int quantity = 0;
if (!int.TryParse(quantityRaw, out quantity))
    return inn.newError("quantity must be an integer.");

if (quantity <= 0)
    return inn.newError("quantity must be greater than zero.");

string NormalizeActionType(string value)
{
    if (string.IsNullOrEmpty(value))
        return "ReusedFromLibrary";

    string trimmed = value.Trim();
    string[] allowed = new string[]
    {
        "ReusedFromLibrary",
        "AddedToProject",
        "UpdatedInProject"
    };

    foreach (string candidate in allowed)
    {
        if (string.Equals(candidate, trimmed, System.StringComparison.OrdinalIgnoreCase))
            return candidate;
    }

    return null;
}

string normalizedActionType = NormalizeActionType(actionType);
if (normalizedActionType == null)
    return inn.newError("action_type is not supported.");

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

if (!string.IsNullOrEmpty(parentPartId))
{
    Item parentPart = inn.newItem("Part", "get");
    parentPart.setID(parentPartId);
    parentPart.setAttribute("select", "id");
    parentPart = parentPart.apply();

    if (parentPart.isError() || parentPart.getItemCount() != 1)
        return inn.newError("parent_part_id does not reference an existing Part.");
}

Item usage = inn.newItem("idea_PartLibraryUsage", "add");
usage.setProperty("library_entry_id", libraryEntryId);
usage.setProperty("part_id", partId);
usage.setProperty("quantity", quantity.ToString());
usage.setProperty("action_type", normalizedActionType);

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

Item usageCountQuery = inn.newItem("idea_PartLibraryUsage", "get");
usageCountQuery.setProperty("library_entry_id", libraryEntryId);
usageCountQuery.setAttribute("select", "id");
usageCountQuery = usageCountQuery.apply();
if (usageCountQuery.isError())
    return usageCountQuery;

int usageCount = usageCountQuery.getItemCount();

Item updateEntry = inn.newItem("idea_PartLibraryEntry", "edit");
updateEntry.setID(libraryEntryId);
updateEntry.setProperty("last_used_on", inn.getCurrentDate());
updateEntry.setProperty("usage_count", usageCount.ToString());
updateEntry = updateEntry.apply();
if (updateEntry.isError())
    return updateEntry;

Item result = inn.newResult("");
result.setProperty("usage_id", usage.getID());
result.setProperty("usage_count", usageCount.ToString());
result.setProperty("last_used_on", inn.getCurrentDate());
return result;
