/*
 * Method:      idea_CommitCadCheckin
 * Type:        Server-side, C#
 * Owner:       IDEA CAD Connector
 *
 * Purpose
 *   Atomically complete a CAD check-in: validate lock ownership, attach the
 *   uploaded native file, sync metadata from the connector, unlock the CAD
 *   item, and return the refreshed CAD record.
 *
 * Business rules (server-owned):
 *   - Only the user who locked the CAD can check in.
 *   - native_file is updated to the uploaded file id.
 *   - metadata fields (material, mass, mass_unit) are optional sync from CAD.
 *   - After successful check-in the lock is released.
 *
 * Input (JSON/AML via SOAP ApplyMethod):
 *   {
 *     "cad_id":           "<32-char CAD id>",
 *     "uploaded_file_id": "<32-char File id from vault upload>",
 *     "material":         "(optional) material string from Inventor",
 *     "mass":             "(optional) mass value",
 *     "mass_unit":        "(optional) mass unit",
 *     "comment":          "(optional) check-in comment"
 *   }
 *
 * Output: refreshed CAD item with full property set.
 *
 * Errors: VALIDATION_FAILED, CAD_NOT_FOUND, CAD_LOCKED (wrong user),
 *   CHECKIN_UPDATE_FAILED, UNLOCK_FAILED.
 */

Innovator inn = this.getInnovator();

// ---- 1. Read input -------------------------------------------------------
string cadId          = this.getProperty("cad_id", "");
string uploadedFileId = this.getProperty("uploaded_file_id", "");
string material       = this.getProperty("material", null);
string mass           = this.getProperty("mass", null);
string massUnit       = this.getProperty("mass_unit", null);
string comment        = this.getProperty("comment", "");

if (string.IsNullOrEmpty(cadId))
    return inn.newError("VALIDATION_FAILED: cad_id is required");
if (string.IsNullOrEmpty(uploadedFileId))
    return inn.newError("VALIDATION_FAILED: uploaded_file_id is required");

const string CadSelect = "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

// ---- 2. Load CAD and validate lock ownership -----------------------------
Item cad = inn.newItem("CAD", "get");
cad.setID(cadId);
cad.setAttribute("select", "id,locked_by_id,state");
cad = cad.apply();
if (cad.isError() || cad.getItemCount() != 1)
    return inn.newError("CAD_NOT_FOUND: " + cadId);

string lockedBy = cad.getProperty("locked_by_id", "");
string callerUserId = inn.getUserID();
if (string.IsNullOrEmpty(lockedBy))
    return inn.newError("CAD_LOCKED: CAD is not locked. Checkout before check-in.");
if (lockedBy != callerUserId)
    return inn.newError("CAD_LOCKED: CAD is locked by another user (" + lockedBy + ")");

// ---- 3. Update CAD: attach native_file + sync metadata -------------------
// NOTE: material / mass / mass_unit metadata sync is disabled because the
// out-of-the-box CAD ItemType in this database does not expose those
// properties and the connector admin cannot add them. Re-enable once the
// CAD schema gains those properties.
Item update = inn.newItem("CAD", "update");
update.setID(cadId);
update.setProperty("native_file", uploadedFileId);
// if (!string.IsNullOrEmpty(material))
//     update.setProperty("material", material);
// if (!string.IsNullOrEmpty(mass))
//     update.setProperty("mass", mass);
// if (!string.IsNullOrEmpty(massUnit))
//     update.setProperty("mass_unit", massUnit);
update = update.apply();
if (update.isError())
    return inn.newError("CHECKIN_UPDATE_FAILED: " + update.getErrorString());

// ---- 4. Unlock the CAD ---------------------------------------------------
Item unlock = inn.newItem("CAD", "unlock");
unlock.setID(cadId);
unlock = unlock.apply();
if (unlock.isError())
    return inn.newError("UNLOCK_FAILED: " + unlock.getErrorString());

// ---- 5. Return refreshed CAD ---------------------------------------------
Item result = inn.newItem("CAD", "get");
result.setID(cadId);
result.setAttribute("select", CadSelect);
return result.apply();
