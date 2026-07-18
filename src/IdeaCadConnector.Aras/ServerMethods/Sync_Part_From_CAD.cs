/*
 * Method:      Sync_Part_From_CAD
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 * METHOD_VERSION: 2026-06-29-CLEAN
 */

Innovator inn = this.getInnovator();

string cadId = this.getProperty("cad_id", "");

if (string.IsNullOrEmpty(cadId))
{
    string cadIdList = this.getAttribute("idlist");
    if (!string.IsNullOrEmpty(cadIdList))
    {
        cadId = cadIdList.Split(',')[0].Trim();
    }
}

if (string.IsNullOrEmpty(cadId))
{
    cadId = this.getID();
}

if (string.IsNullOrEmpty(cadId))
{
    return inn.newError("SYNC_PART_FROM_CAD: missing CAD id");
}

bool ContainsText(string source, string value)
{
    return !string.IsNullOrEmpty(source) &&
           source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
}

Item LoadCad()
{
    Item x = inn.newItem("CAD", "get");
    x.setID(cadId);
    x.setAttribute("select", "id,state");
    return x.apply();
}

Item LoadPart(string partId)
{
    Item x = inn.newItem("Part", "get");
    x.setID(partId);
    x.setAttribute("select", "id,state,locked_by_id,generation");
    return x.apply();
}

Item TryUnlockPart(string partId, string label)
{
    try
    {
        Item unlockPart = inn.newItem("Part", "unlock");
        unlockPart.setID(partId);
        Item unlockResult = unlockPart.apply();

        if (unlockResult != null && unlockResult.isError())
        {
            string unlockError = unlockResult.getErrorString() ?? "";
            bool canIgnore =
                ContainsText(unlockError, "ItemsNotLockedException") ||
                ContainsText(unlockError, "ItemIsNotLockedException") ||
                ContainsText(unlockError, "not locked");

            if (!canIgnore)
            {
                return inn.newError("SYNC_PART_FROM_CAD: unlock failed [" + label + "]: " + unlockError);
            }
        }

        return unlockResult;
    }
    catch (System.Exception ex)
    {
        string unlockError = ex.ToString();
        bool canIgnore =
            ContainsText(unlockError, "ItemsNotLockedException") ||
            ContainsText(unlockError, "ItemIsNotLockedException") ||
            ContainsText(unlockError, "not locked");

        if (!canIgnore)
        {
            return inn.newError("SYNC_PART_FROM_CAD: unlock threw [" + label + "]: " + ex.Message);
        }

        return null;
    }
}

Item PromotePart(Item partItem, string targetState, string message, string label)
{
    try
    {
        Item promoteResult = partItem.promote(targetState, message);
        if (promoteResult != null && promoteResult.isError())
        {
            return inn.newError("SYNC_PART_FROM_CAD: promote failed [" + label + "]: " + promoteResult.getErrorString());
        }

        return promoteResult;
    }
    catch (System.Exception ex)
    {
        return inn.newError("SYNC_PART_FROM_CAD: promote threw [" + label + "]: " + ex.Message);
    }
}

Item cadResult = LoadCad();
if (cadResult.isError() || cadResult.getItemCount() == 0)
{
    return inn.newError("SYNC_PART_FROM_CAD: CAD not found: " + cadId);
}

Item cadItem = cadResult.getItemByIndex(0);
string cadState = cadItem.getProperty("state", "");

if (cadState == "Khoi tao" || cadState == "Loai bo")
{
    return this;
}

Item partCads = inn.newItem("Part CAD", "get");
partCads.setProperty("related_id", cadId);
partCads.setAttribute("select", "source_id");
Item partCadResult = partCads.apply();

if (partCadResult.isError() || partCadResult.getItemCount() == 0)
{
    return inn.newError("SYNC_PART_FROM_CAD: no Part CAD link for CAD: " + cadId);
}

string partId = partCadResult.getItemByIndex(0).getProperty("source_id", "");
if (string.IsNullOrEmpty(partId))
{
    return inn.newError("SYNC_PART_FROM_CAD: missing Part id for CAD: " + cadId);
}

Item partResult = LoadPart(partId);
if (partResult.isError() || partResult.getItemCount() == 0)
{
    return inn.newError("SYNC_PART_FROM_CAD: Part not found: " + partId);
}

Item partItem = partResult.getItemByIndex(0);
string partState = partItem.getProperty("state", "");
string lockedById = partItem.getProperty("locked_by_id", "");

if (partState == cadState)
{
    return this;
}

if (!string.IsNullOrEmpty(lockedById))
{
    Item unlockResult = TryUnlockPart(partId, "current-part");
    if (unlockResult != null && unlockResult.isError())
    {
        return unlockResult;
    }

    partResult = LoadPart(partId);
    if (partResult.isError() || partResult.getItemCount() == 0)
    {
        return inn.newError("SYNC_PART_FROM_CAD: Part refresh failed after unlock. PartId=" + partId);
    }

    partItem = partResult.getItemByIndex(0);
    partState = partItem.getProperty("state", "");
    lockedById = partItem.getProperty("locked_by_id", "");

    if (!string.IsNullOrEmpty(lockedById))
    {
        return inn.newError("SYNC_PART_FROM_CAD: Part is still locked after unlock attempt. PartId=" + partId);
    }

    if (partState == cadState)
    {
        return this;
    }
}

if (partState == "In Change" || cadState == "In Change")
{
    return this;
}

string syncMessage = "Auto sync tu CAD: " + cadId;

if (partState == "Khoi tao")
{
    Item promoteResult = PromotePart(partItem, cadState, syncMessage, "from-khoi-tao");
    if (promoteResult != null && promoteResult.isError())
    {
        return promoteResult;
    }

    return this;
}

if (cadState == "Thiet ke chi tiet" && partState != "Thiet ke chi tiet")
{
    Item newPartResult;
    try
    {
        Item newPart = inn.newItem("Part", "version");
        newPart.setID(partId);
        newPartResult = newPart.apply();
    }
    catch (System.Exception versionEx)
    {
        return inn.newError("SYNC_PART_FROM_CAD: Part version threw: " + versionEx.Message);
    }

    if (newPartResult == null || newPartResult.isError() || newPartResult.getItemCount() == 0)
    {
        return inn.newError("SYNC_PART_FROM_CAD: Part version failed: " + (newPartResult == null ? "null result" : newPartResult.getErrorString()));
    }

    Item versionedPart = newPartResult.getItemByIndex(0);
    string versionedPartId = versionedPart.getID();

    Item versionedPartResult = LoadPart(versionedPartId);
    if (versionedPartResult.isError() || versionedPartResult.getItemCount() == 0)
    {
        return inn.newError("SYNC_PART_FROM_CAD: reload versioned Part failed: " + versionedPartId);
    }

    Item versionedPartItem = versionedPartResult.getItemByIndex(0);
    string versionedLockedById = versionedPartItem.getProperty("locked_by_id", "");

    if (!string.IsNullOrEmpty(versionedLockedById))
    {
        Item unlockVersionedResult = TryUnlockPart(versionedPartId, "versioned-part");
        if (unlockVersionedResult != null && unlockVersionedResult.isError())
        {
            return unlockVersionedResult;
        }

        versionedPartResult = LoadPart(versionedPartId);
        if (versionedPartResult.isError() || versionedPartResult.getItemCount() == 0)
        {
            return inn.newError("SYNC_PART_FROM_CAD: reload versioned Part after unlock failed: " + versionedPartId);
        }

        versionedPartItem = versionedPartResult.getItemByIndex(0);
        versionedLockedById = versionedPartItem.getProperty("locked_by_id", "");

        if (!string.IsNullOrEmpty(versionedLockedById))
        {
            return inn.newError("SYNC_PART_FROM_CAD: versioned Part is still locked after unlock attempt: " + versionedPartId);
        }
    }

    Item promoteVersionedResult = PromotePart(versionedPartItem, cadState, syncMessage, "versioned-part");
    if (promoteVersionedResult != null && promoteVersionedResult.isError())
    {
        return promoteVersionedResult;
    }

    return this;
}

Item finalPromoteResult = PromotePart(partItem, cadState, syncMessage, "final");
if (finalPromoteResult != null && finalPromoteResult.isError())
{
    return finalPromoteResult;
}

return this;