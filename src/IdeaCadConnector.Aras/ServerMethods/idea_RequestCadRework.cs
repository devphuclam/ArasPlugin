/*
 * Method:      idea_RequestCadRework
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 * METHOD_VERSION: 2026-06-29-G
 */

Innovator inn = this.getInnovator();

string cadId = this.getProperty("cad_id", "");
string comment = this.getProperty("comment", "Request CAD Rework");

if (string.IsNullOrEmpty(cadId))
{
    return inn.newError("VALIDATION_FAILED: cad_id is required");
}

const string ReviewState = "In Review";
const string ReworkState = "Thiet ke chi tiet";
const string CadSelect = "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

Item LoadCad()
{
    Item x = inn.newItem("CAD", "get");
    x.setID(cadId);
    x.setAttribute("select", CadSelect);
    return x.apply();
}

Item SyncPartFromCad()
{
    Item sync = inn.newItem("Method", "Sync_Part_From_CAD");
    sync.setProperty("cad_id", cadId);
    return sync.apply();
}

bool ContainsText(string source, string value)
{
    return !string.IsNullOrEmpty(source) &&
           source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
}

bool IsState(Item item, string expected)
{
    return item != null &&
           string.Equals(item.getProperty("state", ""), expected, System.StringComparison.OrdinalIgnoreCase);
}

Item cad = LoadCad();
if (cad.isError() || cad.getItemCount() != 1)
{
    return inn.newError("CAD_NOT_FOUND: " + cadId);
}

if (!IsState(cad, ReviewState))
{
    return inn.newError("INVALID_STATE: CAD must be in '" + ReviewState + "' but is '" + cad.getProperty("state", "") + "'");
}

try
{
    Item directPromote = cad.promote(ReworkState, comment);
    if (directPromote != null && directPromote.isError())
    {
        return inn.newError("PROMOTE_FAILED [METHOD_VERSION:2026-06-29-G]: " + directPromote.getErrorString());
    }

    Item directResult = LoadCad();
    if (directResult.isError() || directResult.getItemCount() != 1)
    {
        return inn.newError("CAD_REFRESH_FAILED [METHOD_VERSION:2026-06-29-G]: " + cadId);
    }

    Item syncResult = SyncPartFromCad();
    if (syncResult != null && syncResult.isError())
    {
        return inn.newError("SYNC_PART_FROM_CAD_FAILED [METHOD_VERSION:2026-06-29-G]: " + syncResult.getErrorString());
    }

    return directResult;
}
catch (System.Exception promoteEx)
{
    Item afterFirstAttempt = LoadCad();
    if (!afterFirstAttempt.isError() && afterFirstAttempt.getItemCount() == 1 && IsState(afterFirstAttempt, ReworkState))
    {
        Item syncResult = SyncPartFromCad();
        if (syncResult != null && syncResult.isError())
        {
            return inn.newError("SYNC_PART_FROM_CAD_FAILED [METHOD_VERSION:2026-06-29-G]: " + syncResult.getErrorString());
        }

        return afterFirstAttempt;
    }

    string promoteError = promoteEx.ToString();
    bool looksLocked =
        ContainsText(promoteError, "ItemsLockedException") ||
        ContainsText(promoteError, "ItemLockedException") ||
        ContainsText(promoteError, "locked");

    if (!looksLocked)
    {
        return inn.newError("PROMOTE_THROWN [METHOD_VERSION:2026-06-29-G]: " + promoteEx.Message);
    }
}

try
{
    Item unlockItem = inn.newItem("CAD", "unlock");
    unlockItem.setID(cadId);
    Item unlockResult = unlockItem.apply();

    if (unlockResult != null && unlockResult.isError())
    {
        string unlockError = unlockResult.getErrorString() ?? "";
        bool canIgnoreUnlockError =
            ContainsText(unlockError, "ItemsNotLockedException") ||
            ContainsText(unlockError, "ItemIsNotLockedException") ||
            ContainsText(unlockError, "not locked");

        if (!canIgnoreUnlockError)
        {
            return inn.newError("UNLOCK_FAILED [METHOD_VERSION:2026-06-29-G]: " + unlockError);
        }
    }
}
catch (System.Exception unlockEx)
{
    string unlockError = unlockEx.ToString();
    bool canIgnoreUnlockError =
        ContainsText(unlockError, "ItemsNotLockedException") ||
        ContainsText(unlockError, "ItemIsNotLockedException") ||
        ContainsText(unlockError, "not locked");

    if (!canIgnoreUnlockError)
    {
        return inn.newError("UNLOCK_THROWN [METHOD_VERSION:2026-06-29-G]: " + unlockEx.Message);
    }
}

cad = LoadCad();
if (cad.isError() || cad.getItemCount() != 1)
{
    return inn.newError("CAD_REFRESH_FAILED [METHOD_VERSION:2026-06-29-G]: " + cadId);
}

if (IsState(cad, ReworkState))
{
    Item syncResult = SyncPartFromCad();
    if (syncResult != null && syncResult.isError())
    {
        return inn.newError("SYNC_PART_FROM_CAD_FAILED [METHOD_VERSION:2026-06-29-G]: " + syncResult.getErrorString());
    }

    return cad;
}

if (!IsState(cad, ReviewState))
{
    return inn.newError("INVALID_STATE_AFTER_RELOAD [METHOD_VERSION:2026-06-29-G]: CAD is now '" + cad.getProperty("state", "") + "'");
}

try
{
    Item retryPromote = cad.promote(ReworkState, comment);
    if (retryPromote != null && retryPromote.isError())
    {
        return inn.newError("PROMOTE_RETRY_FAILED [METHOD_VERSION:2026-06-29-G]: " + retryPromote.getErrorString());
    }
}
catch (System.Exception retryEx)
{
    Item afterRetry = LoadCad();
    if (!afterRetry.isError() && afterRetry.getItemCount() == 1 && IsState(afterRetry, ReworkState))
    {
        Item syncResult = SyncPartFromCad();
        if (syncResult != null && syncResult.isError())
        {
            return inn.newError("SYNC_PART_FROM_CAD_FAILED [METHOD_VERSION:2026-06-29-G]: " + syncResult.getErrorString());
        }

        return afterRetry;
    }

    return inn.newError("PROMOTE_RETRY_THROWN [METHOD_VERSION:2026-06-29-G]: " + retryEx.Message);
}

Item finalResult = LoadCad();
if (finalResult.isError() || finalResult.getItemCount() != 1)
{
    return inn.newError("CAD_REFRESH_FAILED [METHOD_VERSION:2026-06-29-G]: " + cadId);
}

Item syncFinalResult = SyncPartFromCad();
if (syncFinalResult != null && syncFinalResult.isError())
{
    return inn.newError("SYNC_PART_FROM_CAD_FAILED [METHOD_VERSION:2026-06-29-G]: " + syncFinalResult.getErrorString());
}

return finalResult;