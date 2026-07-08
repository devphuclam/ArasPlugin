/*
 * Method:      idea_GetPrimaryIronCadForPart
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 * METHOD_VERSION: 2026-07-08-A
 *
 * Purpose
 *   Return the best primary IronCAD CAD item linked to a Part through the
 *   standard "Part CAD" relationship. This server-owned lookup avoids
 *   client-side ambiguity when Aras serializes related_id differently over AML.
 *
 * Input:
 *   part_id: required Part id
 *
 * Output:
 *   Refreshed CAD item with:
 *   id,item_number,name,classification,authoring_tool,generation,state,
 *   locked_by_id,native_file
 *
 * Selection priority:
 *   1. IronCAD Mechanical/Part CAD with native_file
 *   2. Any IronCAD CAD with native_file
 *   3. Any CAD with native_file
 *   4. IronCAD Mechanical/Part CAD without native_file
 *   5. Any CAD without native_file
 *
 * Errors:
 *   VALIDATION_FAILED, PART_NOT_FOUND, PART_CAD_NOT_FOUND, CAD_NOT_FOUND.
 */

Innovator inn = this.getInnovator();

string partId = this.getProperty("part_id", "");
if (string.IsNullOrEmpty(partId))
{
    return inn.newError("VALIDATION_FAILED: part_id is required");
}

const string CadSelect = "id,item_number,name,classification,authoring_tool,generation,state,locked_by_id,native_file";

Item part = inn.newItem("Part", "get");
part.setID(partId);
part.setAttribute("select", "id,item_number");
part = part.apply();
if (part.isError() || part.getItemCount() != 1)
{
    return inn.newError("PART_NOT_FOUND: " + partId);
}

bool HasNative(Item cad)
{
    return cad != null && !string.IsNullOrEmpty(cad.getProperty("native_file", ""));
}

bool IsIronCad(Item cad)
{
    return cad != null &&
           string.Equals(cad.getProperty("authoring_tool", ""), "IronCAD", System.StringComparison.OrdinalIgnoreCase);
}

bool IsMechanicalPart(Item cad)
{
    return cad != null &&
           string.Equals(cad.getProperty("classification", ""), "Mechanical/Part", System.StringComparison.OrdinalIgnoreCase);
}

Item LoadCad(string cadId)
{
    if (string.IsNullOrEmpty(cadId))
    {
        return null;
    }

    Item cad = inn.newItem("CAD", "get");
    cad.setID(cadId);
    cad.setAttribute("select", CadSelect);
    cad = cad.apply();
    if (cad.isError() || cad.getItemCount() < 1)
    {
        return null;
    }

    return cad.getItemByIndex(0);
}

Item bestIronNativePart = null;
Item bestIronNativeAny = null;
Item bestNativeAny = null;
Item bestIronPartNoNative = null;
Item bestAnyNoNative = null;

Item rels = inn.newItem("Part CAD", "get");
rels.setProperty("source_id", partId);
rels.setAttribute("select", "id,related_id");
rels = rels.apply();
if (rels.isError())
{
    return inn.newError("PART_CAD_LOOKUP_FAILED: " + rels.getErrorString());
}

if (rels.getItemCount() < 1)
{
    return inn.newError("PART_CAD_NOT_FOUND: " + partId);
}

for (int i = 0; i < rels.getItemCount(); i++)
{
    Item rel = rels.getItemByIndex(i);
    string cadId = rel.getProperty("related_id", "");
    Item cad = LoadCad(cadId);
    if (cad == null)
    {
        continue;
    }

    bool hasNative = HasNative(cad);
    bool isIron = IsIronCad(cad);
    bool isPart = IsMechanicalPart(cad);

    if (hasNative && isIron && isPart && bestIronNativePart == null)
    {
        bestIronNativePart = cad;
        continue;
    }

    if (hasNative && isIron && bestIronNativeAny == null)
    {
        bestIronNativeAny = cad;
        continue;
    }

    if (hasNative && bestNativeAny == null)
    {
        bestNativeAny = cad;
        continue;
    }

    if (isIron && isPart && bestIronPartNoNative == null)
    {
        bestIronPartNoNative = cad;
        continue;
    }

    if (bestAnyNoNative == null)
    {
        bestAnyNoNative = cad;
    }
}

Item selected =
    bestIronNativePart ??
    bestIronNativeAny ??
    bestNativeAny ??
    bestIronPartNoNative ??
    bestAnyNoNative;

if (selected == null)
{
    return inn.newError("CAD_NOT_FOUND: no readable CAD linked to Part " + partId);
}

return selected;
