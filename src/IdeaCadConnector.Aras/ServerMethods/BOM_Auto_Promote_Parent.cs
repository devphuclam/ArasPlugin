Innovator inn = this.getInnovator();

// OnAfterPromote v2: context empty, idlist attribute has the ID(s)
string idlist = this.getAttribute("idlist", "");
if (idlist == "") return this;

string childId = idlist.Split(',')[0].Trim();

// Fetch child Part from DB
Item childPart = inn.newItem("Part", "get");
childPart.setAttribute("select", "state,keyed_name");
childPart.setID(childId);
childPart = childPart.apply();
if (childPart.isError()) return this;

string childState = childPart.getProperty("state", "");

if (childState == "" || childState == "Khoi tao") return this;
if (childState == "Loai bo" || childState == "Obsolete" || childState == "Superseded") return this;

// Find parent BOMs
Item bomParents = inn.newItem("Part BOM", "get");
bomParents.setAttribute("select", "source_id(id)");
bomParents.setProperty("related_id", childId);
bomParents = bomParents.apply();
if (bomParents.isError()) return this;

int bomCount = bomParents.getItemCount();
if (bomCount == 0 && bomParents.getID() != null && bomParents.getID() != "") {
    bomCount = 1; // single result case
}

for (int i = 0; i < bomCount; i++) {
    Item bom = (bomParents.getItemCount() > 0) ? bomParents.getItemByIndex(i) : bomParents;
    string parentId = bom.getProperty("source_id", "");
    if (parentId == "") continue;

    Item parentPart = inn.newItem("Part", "get");
    parentPart.setAttribute("select", "state,locked_by_id");
    parentPart.setID(parentId);
    parentPart = parentPart.apply();
    if (parentPart.isError()) continue;

    string parentState = parentPart.getProperty("state", "");
    string parentLocked = parentPart.getProperty("locked_by_id", "");

    if (parentState == childState || parentLocked != "") continue;
    if (parentState == "Loai bo" || parentState == "Obsolete" || parentState == "Superseded") continue;

    // Check siblings
    Item siblings = inn.newItem("Part BOM", "get");
    siblings.setAttribute("select", "related_id(id)");
    siblings.setProperty("source_id", parentId);
    siblings = siblings.apply();
    if (siblings.isError()) continue;

    bool allPromoted = true;
    int sibCount = siblings.getItemCount();
    if (sibCount == 0 && siblings.getID() != null && siblings.getID() != "") {
        sibCount = 1;
    }

    for (int j = 0; j < sibCount; j++) {
        Item sibBom = (siblings.getItemCount() > 0) ? siblings.getItemByIndex(j) : siblings;
        string sibId = sibBom.getProperty("related_id", "");
        if (sibId == "" || sibId == childId) continue;

        Item sibPart = inn.newItem("Part", "get");
        sibPart.setAttribute("select", "state,locked_by_id");
        sibPart.setID(sibId);
        sibPart = sibPart.apply();
        if (sibPart.isError()) continue;

        string sibState = sibPart.getProperty("state", "");
        string sibLocked = sibPart.getProperty("locked_by_id", "");

        if (sibState == "Loai bo" || sibState == "Obsolete" || sibState == "Superseded") continue;
        if (sibState == childState || sibLocked != "") continue;

        allPromoted = false;
        break;
    }

    if (allPromoted) {
        parentPart.promote(childState, "Auto sync BOM");
    }
}

return this;