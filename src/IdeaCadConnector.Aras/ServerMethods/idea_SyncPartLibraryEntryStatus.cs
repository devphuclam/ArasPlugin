/*
 * Method: idea_SyncPartLibraryEntryStatus
 * Type: Server-side C#
 *
 * Deployment:
 * - ItemType: idea_PartLibraryEntry
 * - Server Event: OnAfterPromote
 * - Method: idea_SyncPartLibraryEntryStatus
 *
 * Purpose:
 * - Read the actual lifecycle state from the promoted Entry.
 * - Copy that value into entry_status.
 * - The desktop client still prefers lifecycle state even before this Method is deployed.
 */

Innovator inn = this.getInnovator();
string entryId = (this.getID() ?? "").Trim();

if (string.IsNullOrEmpty(entryId))
    return inn.newError("Library Entry ID is required.");

Item currentEntry = inn.newItem("idea_PartLibraryEntry", "get");
currentEntry.setID(entryId);
currentEntry.setAttribute("select", "id,state");
currentEntry = currentEntry.apply();

if (currentEntry.isError() || currentEntry.getItemCount() != 1)
    return inn.newError("Library Entry was not found.");

string lifecycleState = currentEntry.getProperty("state", "");
if (string.IsNullOrWhiteSpace(lifecycleState))
    return inn.newError("Lifecycle state could not be resolved.");

Item updateEntry = inn.newItem("idea_PartLibraryEntry", "edit");
updateEntry.setID(entryId);
updateEntry.setProperty("entry_status", lifecycleState);
updateEntry = updateEntry.apply();

if (updateEntry.isError())
    return updateEntry;

return updateEntry;
