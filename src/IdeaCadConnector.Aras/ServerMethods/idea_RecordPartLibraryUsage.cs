/*
 * Method: idea_RecordPartLibraryUsage
 * Type: Server-side C#
 *
 * Atomically records a Library Part usage:
 * 1. Validate the Library Entry.
 * 2. Create one idea_PartLibraryUsage Item.
 * 3. Read and increment usage_count.
 * 4. Set last_used_on.
 * 5. Update idea_PartLibraryEntry.
 * 6. Return usage_id, usage_count, last_used_on.
 */

Innovator inn = this.getInnovator();

string libraryEntryId =
    (this.getProperty("library_entry_id", "") ?? "").Trim();
string partId =
    (this.getProperty("part_id", "") ?? "").Trim();
string projectCode =
    (this.getProperty("project_code", "") ?? "").Trim();
string parentPartId =
    (this.getProperty("parent_part_id", "") ?? "").Trim();
string quantityStr =
    (this.getProperty("quantity", "1") ?? "1").Trim();
string usedBy =
    (this.getProperty("used_by", "") ?? "").Trim();
string commitId =
    (this.getProperty("commit_id", "") ?? "").Trim();
string actionType =
    (this.getProperty("action_type", "") ?? "").Trim();

if (string.IsNullOrEmpty(libraryEntryId))
    return inn.newError("library_entry_id is required.");

if (string.IsNullOrEmpty(partId))
    return inn.newError("part_id is required.");

// 1. Validate the Library Entry
Item entry = inn.newItem("idea_PartLibraryEntry", "get");
entry.setID(libraryEntryId);
entry.setAttribute("select", "id,usage_count");
entry = entry.apply();

if (entry.isError() || entry.isNull())
    return inn.newError("Library Entry not found: " + libraryEntryId);

string currentUsageStr = entry.getProperty("usage_count", "0");
int currentUsage = 0;
int.TryParse(currentUsageStr, out currentUsage);

// 2. Create usage record
Item usage = inn.newItem("idea_PartLibraryUsage", "add");
usage.setProperty("library_entry_id", libraryEntryId);
usage.setProperty("part_id", partId);

if (!string.IsNullOrEmpty(projectCode))
    usage.setProperty("project_code", projectCode);

if (!string.IsNullOrEmpty(parentPartId))
    usage.setProperty("parent_part_id", parentPartId);

if (!string.IsNullOrEmpty(quantityStr))
    usage.setProperty("quantity", quantityStr);

if (!string.IsNullOrEmpty(usedBy))
    usage.setProperty("used_by", usedBy);

if (!string.IsNullOrEmpty(commitId))
    usage.setProperty("commit_id", commitId);

if (!string.IsNullOrEmpty(actionType))
    usage.setProperty("action_type", actionType);

usage = usage.apply();

if (usage.isError())
    return usage;

string usageId = usage.getID();
int newCount = currentUsage + 1;

// 3 & 4 & 5. Increment usage_count and set last_used_on
Item update = inn.newItem("idea_PartLibraryEntry", "edit");
update.setID(libraryEntryId);
update.setProperty("usage_count", newCount.ToString());
update.setProperty("last_used_on", inn.getCurrentDate());
update = update.apply();

if (update.isError())
    return update;

// 6. Return result
Item result = inn.newResult("");
result.setProperty("usage_id", usageId);
result.setProperty("usage_count", newCount.ToString());
result.setProperty("last_used_on", inn.getCurrentDate());
return result;
