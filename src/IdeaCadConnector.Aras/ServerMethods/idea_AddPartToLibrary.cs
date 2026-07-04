/*
 * Method: idea_AddPartToLibrary
 * Type: Server-side C#
 *
 * Adds an existing Part to idea_PartLibrary.
 * Returns the existing Entry when the same Part/config/policy
 * is already registered.
 */

Innovator inn = this.getInnovator();

const string LibraryType = "idea_PartLibrary";
const string EntryType = "idea_PartLibraryEntry";
const string PartType = "Part";

const string ActiveStatus = "Active";
const string DraftStatus = "Draft";

const string PinnedPolicy = "Pinned";
const string LatestReleasedPolicy = "LatestReleased";
const string LatestCurrentPolicy = "LatestCurrent";

string libraryId =
    (this.getProperty("library_id", "") ?? "").Trim();

string partId =
    (this.getProperty("part_id", "") ?? "").Trim();

string suppliedConfigId =
    (this.getProperty("part_config_id", "") ?? "").Trim();

string revisionPolicy =
    (this.getProperty("revision_policy", PinnedPolicy) ?? "").Trim();

string pinnedPartId =
    (this.getProperty("pinned_part_id", "") ?? "").Trim();

string category =
    this.getProperty("category", "") ?? "";

string tags =
    this.getProperty("tags", "") ?? "";

string note =
    this.getProperty("note", "") ?? "";

string sourceProject =
    this.getProperty("source_project", "") ?? "";

string sourceCommit =
    this.getProperty("source_commit", "") ?? "";

/* ---------------------------------------------------------
 * 1. Input validation
 * --------------------------------------------------------- */

if (string.IsNullOrEmpty(libraryId))
{
    return inn.newError(
        "VALIDATION_FAILED: library_id is required.");
}

if (string.IsNullOrEmpty(partId))
{
    return inn.newError(
        "VALIDATION_FAILED: part_id is required.");
}

bool isPinned =
    string.Equals(
        revisionPolicy,
        PinnedPolicy,
        StringComparison.OrdinalIgnoreCase);

bool isLatestReleased =
    string.Equals(
        revisionPolicy,
        LatestReleasedPolicy,
        StringComparison.OrdinalIgnoreCase);

bool isLatestCurrent =
    string.Equals(
        revisionPolicy,
        LatestCurrentPolicy,
        StringComparison.OrdinalIgnoreCase);

if (!isPinned && !isLatestReleased && !isLatestCurrent)
{
    return inn.newError(
        "VALIDATION_FAILED: revision_policy must be " +
        "Pinned, LatestReleased, or LatestCurrent.");
}

/* Normalize policy spelling returned to the client. */
if (isPinned)
{
    revisionPolicy = PinnedPolicy;
}
else if (isLatestReleased)
{
    revisionPolicy = LatestReleasedPolicy;
}
else
{
    revisionPolicy = LatestCurrentPolicy;
}

/* ---------------------------------------------------------
 * 2. Validate target Library
 * --------------------------------------------------------- */

Item libraryQuery = inn.newItem(LibraryType, "get");
libraryQuery.setID(libraryId);
libraryQuery.setAttribute(
    "select",
    "id,name,status,default_revision_policy");

Item libraryResult = libraryQuery.apply();

if (libraryResult.isError())
{
    string libraryError =
        libraryResult.getErrorString() ?? "";

    if (libraryError.IndexOf(
            "No items of type",
            StringComparison.OrdinalIgnoreCase) >= 0)
    {
        return inn.newError(
            "LIBRARY_NOT_FOUND: " + libraryId);
    }

    return inn.newError(
        "LIBRARY_LOOKUP_FAILED: " + libraryError);
}

if (libraryResult.getItemCount() != 1)
{
    return inn.newError(
        "LIBRARY_NOT_FOUND: " + libraryId);
}

string libraryStatus =
    libraryResult.getProperty("status", "");

if (!string.Equals(
        libraryStatus,
        ActiveStatus,
        StringComparison.OrdinalIgnoreCase))
{
    return inn.newError(
        "VALIDATION_FAILED: Target Part Library is not Active.");
}

/* ---------------------------------------------------------
 * 3. Validate exact Part
 * --------------------------------------------------------- */

Item partQuery = inn.newItem(PartType, "get");
partQuery.setID(partId);
partQuery.setAttribute(
    "select",
    "id,config_id,item_number,name,classification," +
    "major_rev,state,generation");

Item partResult = partQuery.apply();

if (partResult.isError())
{
    string partError =
        partResult.getErrorString() ?? "";

    if (partError.IndexOf(
            "No items of type",
            StringComparison.OrdinalIgnoreCase) >= 0)
    {
        return inn.newError(
            "PART_NOT_FOUND: " + partId);
    }

    return inn.newError(
        "PART_LOOKUP_FAILED: " + partError);
}

if (partResult.getItemCount() != 1)
{
    return inn.newError(
        "PART_NOT_FOUND: " + partId);
}

string actualConfigId =
    (partResult.getProperty("config_id", "") ?? "").Trim();

if (string.IsNullOrEmpty(actualConfigId))
{
    return inn.newError(
        "VALIDATION_FAILED: Part config_id could not be resolved.");
}

if (!string.IsNullOrEmpty(suppliedConfigId) &&
    !string.Equals(
        suppliedConfigId,
        actualConfigId,
        StringComparison.OrdinalIgnoreCase))
{
    return inn.newError(
        "VALIDATION_FAILED: supplied part_config_id does not " +
        "match the selected Part.");
}

string partConfigId = actualConfigId;
string partRevision =
    partResult.getProperty("major_rev", "");

/*
 * The Part selected by the desktop client is the pinned revision.
 * Normalize a missing pinned_part_id to part_id.
 */
if (isPinned)
{
    if (string.IsNullOrEmpty(pinnedPartId))
    {
        pinnedPartId = partId;
    }

    if (!string.Equals(
            pinnedPartId,
            partId,
            StringComparison.OrdinalIgnoreCase))
    {
        return inn.newError(
            "VALIDATION_FAILED: pinned_part_id must match " +
            "the selected part_id.");
    }
}

/* ---------------------------------------------------------
 * 4. Duplicate check
 * --------------------------------------------------------- */

Item duplicateQuery =
    inn.newItem(EntryType, "get");

duplicateQuery.setAttribute(
    "select",
    "id,source_id,related_id,part_config_id," +
    "revision_policy,pinned_part_id,pinned_revision," +
    "entry_status,category,tags,note,source_project," +
    "source_commit,usage_count,last_used_on");

duplicateQuery.setAttribute("maxRecords", "1");

duplicateQuery.setProperty(
    "source_id",
    libraryId);

duplicateQuery.setProperty(
    "part_config_id",
    partConfigId);

duplicateQuery.setProperty(
    "revision_policy",
    revisionPolicy);

if (isPinned)
{
    duplicateQuery.setProperty(
        "pinned_part_id",
        pinnedPartId);
}

Item duplicateResult = duplicateQuery.apply();

if (duplicateResult.isError())
{
    string duplicateError =
        duplicateResult.getErrorString() ?? "";

    bool noItems =
        duplicateError.IndexOf(
            "No items of type",
            StringComparison.OrdinalIgnoreCase) >= 0 ||
        duplicateError.IndexOf(
            "No items found",
            StringComparison.OrdinalIgnoreCase) >= 0;

    if (!noItems)
    {
        return inn.newError(
            "DUPLICATE_CHECK_FAILED: " + duplicateError);
    }
}
else if (duplicateResult.getItemCount() > 0)
{
    Item existing =
        duplicateResult.getItemByIndex(0);

    /*
     * These two response properties are consumed by
     * HttpPartLibraryClient. They are response metadata and
     * do not need to be persisted as ItemType properties.
     */
    existing.setProperty(
        "entry_id",
        existing.getID());

    existing.setProperty(
        "already_exists",
        "1");

    return existing;
}

/* ---------------------------------------------------------
 * 5. Create Entry relationship
 * --------------------------------------------------------- */

Item addEntry =
    inn.newItem(EntryType, "add");

addEntry.setProperty(
    "source_id",
    libraryId);

addEntry.setProperty(
    "related_id",
    partId);

addEntry.setProperty(
    "part_config_id",
    partConfigId);

addEntry.setProperty(
    "revision_policy",
    revisionPolicy);

addEntry.setProperty(
    "entry_status",
    DraftStatus);

addEntry.setProperty(
    "category",
    category);

addEntry.setProperty(
    "tags",
    tags);

addEntry.setProperty(
    "note",
    note);

addEntry.setProperty(
    "source_project",
    sourceProject);

addEntry.setProperty(
    "source_commit",
    sourceCommit);

addEntry.setProperty(
    "usage_count",
    "0");

if (isPinned)
{
    addEntry.setProperty(
        "pinned_part_id",
        pinnedPartId);

    addEntry.setProperty(
        "pinned_revision",
        partRevision);
}

Item addResult = addEntry.apply();

if (addResult.isError())
{
    return inn.newError(
        "ADD_FAILED: " +
        (addResult.getErrorString() ?? "Unknown error."));
}

string createdEntryId =
    addResult.getID();

if (string.IsNullOrEmpty(createdEntryId))
{
    return inn.newError(
        "ADD_FAILED: Aras did not return the created Entry ID.");
}

/*
 * Return metadata expected by the desktop client.
 */
addResult.setProperty(
    "entry_id",
    createdEntryId);

addResult.setProperty(
    "already_exists",
    "0");

return addResult;