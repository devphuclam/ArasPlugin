/*
 * Method:      idea_ApproveCadReview
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Move a CAD item from "In Review" to "Released".
 */

Innovator inn = this.getInnovator();

string cadId = this.getProperty("cad_id", "");
string comment = this.getProperty("comment", "Approve CAD Review");

if (string.IsNullOrEmpty(cadId))
{
    return inn.newError("VALIDATION_FAILED: cad_id is required");
}

const string ReviewState = "In Review";
const string ReleasedState = "Released";
const string CadSelect = "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

Item cad = inn.newItem("CAD", "get");
cad.setID(cadId);
cad.setAttribute("select", CadSelect);
cad = cad.apply();
if (cad.isError() || cad.getItemCount() != 1)
{
    return inn.newError("CAD_NOT_FOUND: " + cadId);
}

string currentState = cad.getProperty("state", "");
if (currentState != ReviewState)
{
    return inn.newError("INVALID_STATE: CAD must be in '" + ReviewState + "' but is '" + currentState + "'");
}

string lockedById = cad.getProperty("locked_by_id", "");
if (!string.IsNullOrEmpty(lockedById))
{
    Item unlockResult = cad.unlockItem();
    if (unlockResult.isError())
    {
        return inn.newError("UNLOCK_FAILED: " + unlockResult.getErrorString());
    }

    cad = inn.newItem("CAD", "get");
    cad.setID(cadId);
    cad.setAttribute("select", CadSelect);
    cad = cad.apply();
    if (cad.isError() || cad.getItemCount() != 1)
    {
        return inn.newError("CAD_REFRESH_FAILED: " + cadId);
    }
}

Item promoteResult = cad.promote(ReleasedState, comment);
if (promoteResult.isError())
{
    return inn.newError("PROMOTE_FAILED: " + promoteResult.getErrorString());
}

Item result = inn.newItem("CAD", "get");
result.setID(cadId);
result.setAttribute("select", CadSelect);
return result.apply();