/*
 * Method:      idea_AddPartToLibrary
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Add an existing Aras Part to a Part Library in one server-owned step.
 *   If a matching Entry already exists, return it instead of creating a duplicate.
 *
 * Input (SOAP ApplyMethod / AML):
 *   {
 *     "library_id":     "<idea_PartLibrary id>",
 *     "part_id":        "<Part id>",
 *     "part_config_id": "(optional) config id",
 *     "revision_policy":"Pinned|LatestReleased|LatestCurrent",
 *     "pinned_part_id": "(required when policy is Pinned)",
 *     "category":      "(optional)",
 *     "tags":          "(optional)",
 *     "note":          "(optional)",
 *     "source_project":"(optional)",
 *     "source_commit": "(optional)"
 *   }
 *
 * Output
 *   Returns the existing or newly created idea_PartLibraryEntry item.
 *
 * Errors
 *   VALIDATION_FAILED, LIBRARY_NOT_FOUND, PART_NOT_FOUND, DUPLICATE_CHECK_FAILED,
 *   ADD_FAILED.
 */

Innovator inn = this.getInnovator();

string libraryId = this.getProperty("library_id", "");
string partId = this.getProperty("part_id", "");
string partConfigId = this.getProperty("part_config_id", "");
string revisionPolicy = this.getProperty("revision_policy", "Pinned");
string pinnedPartId = this.getProperty("pinned_part_id", "");
string category = this.getProperty("category", "");
string tags = this.getProperty("tags", "");
string note = this.getProperty("note", "");
string sourceProject = this.getProperty("source_project", "");
string sourceCommit = this.getProperty("source_commit", "");

if (string.IsNullOrEmpty(libraryId))
    return inn.newError("VALIDATION_FAILED: library_id is required");
if (string.IsNullOrEmpty(partId))
    return inn.newError("VALIDATION_FAILED: part_id is required");

const string LibraryType = "idea_PartLibrary";
const string EntryType = "idea_PartLibraryEntry";
const string PartType = "Part";
const string DraftStatus = "Draft";
const string PinnedPolicy = "Pinned";

Item library = inn.newItem(LibraryType, "get");
library.setID(libraryId);
library.setAttribute("select", "id,name,status,default_revision_policy");
library = library.apply();
if (library.isError() || library.getItemCount() != 1)
    return inn.newError("LIBRARY_NOT_FOUND: " + libraryId);

Item part = inn.newItem(PartType, "get");
part.setID(partId);
part.setAttribute("select", "id,config_id,item_number,name,classification,major_rev,state,generation");
part = part.apply();
if (part.isError() || part.getItemCount() != 1)
    return inn.newError("PART_NOT_FOUND: " + partId);

if (string.IsNullOrEmpty(partConfigId))
    partConfigId = part.getProperty("config_id", "");

if (string.IsNullOrEmpty(partConfigId))
    return inn.newError("VALIDATION_FAILED: part_config_id could not be resolved");

if (string.Equals(revisionPolicy, PinnedPolicy, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(pinnedPartId))
    return inn.newError("VALIDATION_FAILED: pinned_part_id is required for Pinned revision policy");

Item duplicateQuery = inn.newItem(EntryType, "get");
duplicateQuery.setProperty("source_id", libraryId);
duplicateQuery.setProperty("part_config_id", partConfigId);
duplicateQuery.setProperty("revision_policy", revisionPolicy);
if (string.Equals(revisionPolicy, PinnedPolicy, StringComparison.OrdinalIgnoreCase))
    duplicateQuery.setProperty("pinned_part_id", pinnedPartId);
duplicateQuery.setAttribute("select", "id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,entry_status,category,tags,note,source_project,source_commit,usage_count");
duplicateQuery = duplicateQuery.apply();
if (!duplicateQuery.isError() && duplicateQuery.getItemCount() > 0)
    return duplicateQuery.getItemByIndex(0);

Item addEntry = inn.newItem(EntryType, "add");
addEntry.setProperty("source_id", libraryId);
addEntry.setProperty("related_id", partId);
addEntry.setProperty("part_config_id", partConfigId);
addEntry.setProperty("revision_policy", revisionPolicy);
addEntry.setProperty("entry_status", DraftStatus);
addEntry.setProperty("category", category);
addEntry.setProperty("tags", tags);
addEntry.setProperty("note", note);
addEntry.setProperty("source_project", sourceProject);
addEntry.setProperty("source_commit", sourceCommit);
addEntry.setProperty("usage_count", "0");
if (string.Equals(revisionPolicy, PinnedPolicy, StringComparison.OrdinalIgnoreCase))
    addEntry.setProperty("pinned_part_id", pinnedPartId);

Item addResult = addEntry.apply();
if (addResult.isError())
    return inn.newError("ADD_FAILED: " + addResult.getErrorString());

Item verify = inn.newItem(EntryType, "get");
verify.setID(addResult.getID());
verify.setAttribute("select", "id,source_id,related_id,part_config_id,revision_policy,pinned_part_id,entry_status,category,tags,note,source_project,source_commit,usage_count");
verify = verify.apply();
if (verify.isError() || verify.getItemCount() != 1)
    return addResult;

return verify;
