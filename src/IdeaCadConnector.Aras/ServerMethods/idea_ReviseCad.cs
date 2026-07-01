/*
 * Method:      idea_ReviseCad
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Create a new major revision (version) of a Released CAD and its linked
 *   Part. Both keep their original item_number. The method then ensures the
 *   new Part points to the new CAD through the "Part CAD" relationship.
 *
 * Business rules
 *   - Source CAD must be in "Released" state.
 *   - Source Part and source CAD must already be linked via "Part CAD".
 *   - Aras "version" action is used on both Part and CAD.
 *   - The new CAD is forced to "Khoi tao" when possible.
 *   - Any inherited "Part CAD" links on the new Part are removed before
 *     linking the new CAD, so the revised Part does not keep pointing at the
 *     old CAD revision.
 *   - The original records remain unchanged.
 *
 * Input (SOAP ApplyMethod / AML)
 *   <Item type="Method" action="idea_ReviseCad">
 *     <cad_id>{current CAD id}</cad_id>
 *     <part_id>{current Part id}</part_id>
 *     <part_number>{current Part number (informational)}</part_number>
 *     <cad_number>{current CAD number (informational)}</cad_number>
 *     <reason>{optional revision reason}</reason>
 *   </Item>
 *
 * Output
 *   Refreshed new CAD item with extra properties:
 *   - new_part_id
 *   - new_cad_id
 *   - new_revision
 *   - new_lifecycle_state
 *
 * Errors
 *   VALIDATION_FAILED
 *   CAD_NOT_FOUND
 *   PART_NOT_FOUND
 *   CAD_NOT_RELEASED
 *   PART_CAD_MISMATCH
 *   VERSION_PART_FAILED
 *   VERSION_CAD_FAILED
 *   CLEAR_INHERITED_LINK_FAILED
 *   SET_DRAFT_FAILED
 *   LINK_FAILED
 *   RESULT_REFRESH_FAILED
 */

Innovator inn = this.getInnovator();

string cadId = this.getProperty("cad_id", "");
string partId = this.getProperty("part_id", "");
string reason = this.getProperty("reason", "Start New Revision");

if (string.IsNullOrEmpty(cadId))
    return inn.newError("VALIDATION_FAILED: cad_id is required");
if (string.IsNullOrEmpty(partId))
    return inn.newError("VALIDATION_FAILED: part_id is required");

const string ReleasedState = "Released";
const string DraftState = "Khoi tao";
const string CadSelect = "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

Item LoadCad(string id)
{
    Item x = inn.newItem("CAD", "get");
    x.setID(id);
    x.setAttribute("select", CadSelect);
    return x.apply();
}

bool EnsurePartCadLinkExists(string sourcePartId, string relatedCadId)
{
    Item rel = inn.newItem("Part CAD", "get");
    rel.setProperty("source_id", sourcePartId);
    rel.setProperty("related_id", relatedCadId);
    rel.setAttribute("select", "id");
    rel = rel.apply();
    return !rel.isError() && rel.getItemCount() > 0;
}

Item DeleteInheritedPartCadLinks(string sourcePartId)
{
    Item rels = inn.newItem("Part CAD", "get");
    rels.setProperty("source_id", sourcePartId);
    rels.setAttribute("select", "id,related_id");
    rels = rels.apply();
    if (rels.isError())
        return rels;

    for (int i = 0; i < rels.getItemCount(); i++)
    {
        string relId = rels.getItemByIndex(i).getID();
        if (string.IsNullOrEmpty(relId))
            continue;

        Item del = inn.newItem("Part CAD", "delete");
        del.setID(relId);
        del = del.apply();
        if (del.isError())
            return del;
    }

    return rels;
}

Item ForceCadDraft(string revisedCadId)
{
    Item current = LoadCad(revisedCadId);
    if (current.isError() || current.getItemCount() != 1)
        return current;

    string currentState = current.getProperty("state", "");
    if (currentState == DraftState)
        return current;

    Item draftCad = inn.newItem("CAD", "edit");
    draftCad.setID(revisedCadId);
    draftCad.setProperty("state", DraftState);
    draftCad = draftCad.apply();
    if (draftCad.isError())
        return draftCad;

    return LoadCad(revisedCadId);
}

// ---- 1. Load and validate source CAD -------------------------------------
Item cad = LoadCad(cadId);
if (cad.isError() || cad.getItemCount() != 1)
    return inn.newError("CAD_NOT_FOUND: " + cadId);

string cadState = cad.getProperty("state", "");
if (cadState != ReleasedState)
    return inn.newError("CAD_NOT_RELEASED: CAD state is '" + cadState + "' but must be '" + ReleasedState + "'");

// ---- 2. Load source Part --------------------------------------------------
Item part = inn.newItem("Part", "get");
part.setID(partId);
part.setAttribute("select", "id,item_number,name,major_rev,state");
part = part.apply();
if (part.isError() || part.getItemCount() != 1)
    return inn.newError("PART_NOT_FOUND: " + partId);

// ---- 3. Ensure caller did not pass mismatched Part/CAD -------------------
if (!EnsurePartCadLinkExists(partId, cadId))
    return inn.newError("PART_CAD_MISMATCH: Part '" + partId + "' is not linked to CAD '" + cadId + "'");

// ---- 4. Version Part ------------------------------------------------------
Item versionedPart = inn.newItem("Part", "version");
versionedPart.setID(partId);
versionedPart = versionedPart.apply();
if (versionedPart.isError())
    return inn.newError("VERSION_PART_FAILED: " + versionedPart.getErrorString());

string newPartId = versionedPart.getID();
if (string.IsNullOrEmpty(newPartId))
    return inn.newError("VERSION_PART_FAILED: Version action did not return a new Part id");

// ---- 5. Version CAD -------------------------------------------------------
Item versionedCad = inn.newItem("CAD", "version");
versionedCad.setID(cadId);
versionedCad = versionedCad.apply();
if (versionedCad.isError())
    return inn.newError("VERSION_CAD_FAILED: " + versionedCad.getErrorString());

string newCadId = versionedCad.getID();
if (string.IsNullOrEmpty(newCadId))
    return inn.newError("VERSION_CAD_FAILED: Version action did not return a new CAD id");

// ---- 6. Remove inherited Part CAD links from the new Part ----------------
Item clearResult = DeleteInheritedPartCadLinks(newPartId);
if (clearResult.isError())
    return inn.newError("CLEAR_INHERITED_LINK_FAILED: " + clearResult.getErrorString());

// ---- 7. Force new CAD to Draft when needed -------------------------------
Item refreshedCad = ForceCadDraft(newCadId);
if (refreshedCad.isError() || refreshedCad.getItemCount() != 1)
    return inn.newError("SET_DRAFT_FAILED: " + (refreshedCad.isError() ? refreshedCad.getErrorString() : newCadId));

// ---- 8. Link new Part -> new CAD -----------------------------------------
Item rel = inn.newItem("Part CAD", "add");
rel.setProperty("source_id", newPartId);
rel.setProperty("related_id", newCadId);
rel = rel.apply();
if (rel.isError())
    return inn.newError("LINK_FAILED: " + rel.getErrorString());

// ---- 9. Return refreshed new CAD with result properties -------------------
Item result = LoadCad(newCadId);
if (result.isError() || result.getItemCount() != 1)
    return inn.newError("RESULT_REFRESH_FAILED: " + newCadId);

result.setProperty("new_part_id", newPartId);
result.setProperty("new_cad_id", newCadId);
result.setProperty("new_revision", result.getProperty("major_rev", ""));
result.setProperty("new_lifecycle_state", result.getProperty("state", DraftState));
return result;
