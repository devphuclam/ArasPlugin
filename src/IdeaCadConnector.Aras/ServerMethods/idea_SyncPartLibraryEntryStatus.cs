/*
 * Method: idea_SyncPartLibraryEntryStatus
 * Type: Server-side C#
 * Event: OnAfterPromote
 * Applies To: idea_PartLibraryEntry
 *
 * Reads the lifecycle state after promote and copies it
 * into the entry_status property so both are always in sync.
 *
 * Note: The desktop client displays the correct lifecycle state
 * even before this Method is deployed, by preferring the "state"
 * property over "entry_status".
 */

Innovator inn = this.getInnovator();
string entryId = this.getID();
string currentState = this.getProperty("state", "");

if (string.IsNullOrEmpty(currentState))
    return this;

Item entry = inn.newItem("idea_PartLibraryEntry", "edit");
entry.setID(entryId);
entry.setProperty("entry_status", currentState);
return entry.apply();
