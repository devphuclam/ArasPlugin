/*
 * Method:      idea_EnsurePrimaryIronCadPartCad
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Ensure exactly one primary IronCAD Part CAD item exists under a given Part,
 *   then return it. Idempotent: if a matching CAD already exists it is returned
 *   unchanged; otherwise a new CAD is created and linked via the "Part CAD"
 *   relationship.
 *
 * Business rules (server-owned, do NOT duplicate in the connector):
 *   - CAD.item_number   = <Part.item_number> + "-ICS"
 *   - CAD.classification = "Mechanical/Part"
 *   - CAD.authoring_tool = "IronCAD"
 *   "Primary IronCAD Part CAD" = a CAD linked to the Part whose
 *   classification == "Mechanical/Part" AND authoring_tool == "IronCAD".
 *
 * Input (SOAP ApplyMethod / AML):
 *   { "part_id": "<32-char Part id>" }
 *
 * Output: a single CAD item with the properties the connector consumes
 *   (id, item_number, classification, authoring_tool, major_rev, state,
 *    generation, native_file, locked_by_id).
 *
 * Errors: returns inn.newError("CODE: detail") for PART_NOT_FOUND,
 *   CAD_CREATE_FAILED, PART_CAD_LINK_FAILED.
 */

Innovator inn = this.getInnovator();

// ---- 1. Read input -------------------------------------------------------
string partId = this.getProperty("part_id", "");
if (string.IsNullOrEmpty(partId))
{
    return inn.newError("VALIDATION_FAILED: part_id is required");
}

const string ClassPath      = "Mechanical/Part";
const string AuthoringTool  = "IronCAD";
const string CadSelect      = "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

// ---- 2. Load the Part ----------------------------------------------------
Item part = inn.newItem("Part", "get");
part.setID(partId);
part.setAttribute("select", "id,item_number,name,description,major_rev,state");
part = part.apply();
if (part.isError() || part.getItemCount() != 1)
{
    return inn.newError("PART_NOT_FOUND: " + partId);
}

string partNumber       = part.getProperty("item_number", "");
string expectedCadNumber = partNumber + "-ICS";

Item primaryCad = null;
Item candidateWithFile = null;

bool IsPrimaryIronCad(Item cad)
{
    return cad != null &&
        !cad.isError() &&
        cad.getProperty("classification", "") == ClassPath &&
        cad.getProperty("authoring_tool", "") == AuthoringTool;
}

bool IsLikelyIronCadCandidate(Item cad)
{
    return cad != null &&
        !cad.isError() &&
        cad.getProperty("authoring_tool", "") == AuthoringTool &&
        cad.getProperty("item_number", "") == expectedCadNumber;
}

bool HasNativeFile(Item cad)
{
    return cad != null && !string.IsNullOrEmpty(cad.getProperty("native_file", ""));
}

Item NormalizeCandidateToPrimary(Item cad)
{
    if (cad == null || cad.isError())
    {
        return cad;
    }

    Item update = inn.newItem("CAD", "edit");
    update.setID(cad.getID());
    update.setProperty("classification", ClassPath);
    update.setProperty("authoring_tool", AuthoringTool);
    update = update.apply();
    if (update.isError())
    {
        return cad;
    }

    Item refreshed = inn.newItem("CAD", "get");
    refreshed.setID(cad.getID());
    refreshed.setAttribute("select", CadSelect);
    refreshed = refreshed.apply();
    return refreshed.isError() ? cad : refreshed;
}

// ---- 3. Idempotency: look for existing primary IronCAD Part CAD ------
// First: check via Part CAD relationship
Item rels = inn.newItem("Part CAD", "get");
rels.setProperty("source_id", partId);
rels.setAttribute("select", "related_id");
rels = rels.apply();

if (!rels.isError() && rels.getItemCount() > 0)
{
    for (int i = 0; i < rels.getItemCount(); i++)
    {
        string relatedCadId = rels.getItemByIndex(i).getProperty("related_id", "");
        if (string.IsNullOrEmpty(relatedCadId))
        {
            continue;
        }

        Item cad = inn.newItem("CAD", "get");
        cad.setID(relatedCadId);
        cad.setAttribute("select", CadSelect);
        cad = cad.apply();

        if (IsPrimaryIronCad(cad) && HasNativeFile(cad))
        {
            return cad;
        }

        if (primaryCad == null && IsPrimaryIronCad(cad))
        {
            primaryCad = cad;
        }

        if (candidateWithFile == null && IsLikelyIronCadCandidate(cad) && HasNativeFile(cad))
        {
            candidateWithFile = NormalizeCandidateToPrimary(cad);
        }
    }
}

if (candidateWithFile != null)
{
    return candidateWithFile;
}

if (primaryCad != null)
{
    return primaryCad;
}

// Fallback: check if CAD with expected item_number already exists (orphaned)
Item existingCad = inn.newItem("CAD", "get");
existingCad.setProperty("item_number", expectedCadNumber);
existingCad.setProperty("authoring_tool", AuthoringTool);
existingCad.setAttribute("select", CadSelect);
existingCad = existingCad.apply();

if (!existingCad.isError() && existingCad.getItemCount() > 0)
{
    Item fallbackPrimaryCad = null;
    Item fallbackCandidateWithFile = null;
    for (int i = 0; i < existingCad.getItemCount(); i++)
    {
        Item cad = existingCad.getItemByIndex(i);
        if (IsPrimaryIronCad(cad) && HasNativeFile(cad))
        {
            fallbackCandidateWithFile = cad;
            break;
        }

        if (fallbackPrimaryCad == null && IsPrimaryIronCad(cad))
        {
            fallbackPrimaryCad = cad;
        }

        if (fallbackCandidateWithFile == null && IsLikelyIronCadCandidate(cad) && HasNativeFile(cad))
        {
            fallbackCandidateWithFile = NormalizeCandidateToPrimary(cad);
        }
    }

    Item candidate = fallbackCandidateWithFile ?? fallbackPrimaryCad;
    if (candidate != null)
    {
        existingCad = candidate;
    }

    // CAD exists but not linked to this Part — link it now
    Item linkRel = inn.newItem("Part CAD", "add");
    linkRel.setProperty("source_id", partId);
    linkRel.setProperty("related_id", existingCad.getID());
    linkRel = linkRel.apply();
    return existingCad;
}

// ---- 4. Create the CAD ---------------------------------------------------
Item newCad = inn.newItem("CAD", "add");
newCad.setProperty("item_number", expectedCadNumber);
newCad.setProperty("name", part.getProperty("name", partNumber));
newCad.setProperty("description", part.getProperty("description", ""));
newCad.setProperty("classification", ClassPath);
newCad.setProperty("authoring_tool", AuthoringTool);
newCad = newCad.apply();
if (newCad.isError())
{
    return inn.newError("CAD_CREATE_FAILED: " + newCad.getErrorString());
}

string newCadId = newCad.getID();

// ---- 5. Link Part -> CAD via the "Part CAD" relationship -----------------
Item rel = inn.newItem("Part CAD", "add");
rel.setProperty("source_id", partId);
rel.setProperty("related_id", newCadId);
rel = rel.apply();
if (rel.isError())
{
    return inn.newError("PART_CAD_LINK_FAILED: " + rel.getErrorString());
}

// ---- 6. Return the freshly created CAD with the connector's select set ---
Item result = inn.newItem("CAD", "get");
result.setID(newCadId);
result.setAttribute("select", CadSelect);
return result.apply();
