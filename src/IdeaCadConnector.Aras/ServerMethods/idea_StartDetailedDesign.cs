/*
 * Method:      idea_StartDetailedDesign
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Move a CAD item from "Khoi tao" to "Thiet ke chi tiet" through a
 *   server-owned business entry point so the desktop connector does not rely
 *   on raw AML promote behavior.
 *
 * Input (SOAP ApplyMethod / AML):
 *   {
 *     "cad_id":  "<32-char CAD id>",
 *     "comment": "(optional) promote comment"
 *   }
 *
 * Output: refreshed CAD item with the connector's standard property set.
 *
 * Errors: VALIDATION_FAILED, CAD_NOT_FOUND, INVALID_STATE, PROMOTE_FAILED.
 */

Innovator inn = this.getInnovator();

string cadId = this.getProperty("cad_id", "");
string comment = this.getProperty("comment", "Start Detailed Design");

if (string.IsNullOrEmpty(cadId))
{
    return inn.newError("VALIDATION_FAILED: cad_id is required");
}

const string InitialState = "Khoi tao";
const string DetailedDesignState = "Thiet ke chi tiet";
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
if (currentState != InitialState)
{
    return inn.newError("INVALID_STATE: CAD must be in '" + InitialState + "' but is '" + currentState + "'");
}

Item promoteResult = cad.promote(DetailedDesignState, comment);
if (promoteResult.isError())
{
    return inn.newError("PROMOTE_FAILED: " + promoteResult.getErrorString());
}

Item result = inn.newItem("CAD", "get");
result.setID(cadId);
result.setAttribute("select", CadSelect);
return result.apply();