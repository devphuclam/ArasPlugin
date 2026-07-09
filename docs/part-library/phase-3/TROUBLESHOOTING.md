# Troubleshooting

## App Does Not Launch

**Symptom:** Double-clicking `IdeaCadConnector.Desktop.exe` does nothing or shows a brief cursor.

**Likely cause:** Missing .NET Framework 4.8, missing dependency DLL, or corrupt extraction.

**Check:**
1. Confirm .NET Framework 4.8 is installed.
2. Run from command prompt to see error output:
   ```powershell
   .\app\IdeaCadConnector.Desktop.exe
   ```
3. Verify all files exist in `app\` folder.

**Action:** Reinstall .NET Framework 4.8, re-extract the zip.

**Blocker:** P0

## Missing DLL

**Symptom:** App fails to start with "Cannot load DLL" or "FileNotFoundException".

**Likely cause:** Incomplete extraction, antivirus quarantine, or build packaging issue.

**Check:** Compare extracted files against `checksums\SHA256SUMS.txt`.

**Action:** Re-extract the zip. If issue persists, report to developer with the full error message and checksum mismatch details.

**Blocker:** P0

## .NET Framework Missing

**Symptom:** `System.TypeInitializationException` or platform not supported error.

**Likely cause:** Machine has an older .NET version.

**Check:** Run `winver` and verify .NET 4.8:
```powershell
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
```

**Action:** Download and install .NET Framework 4.8 Runtime from Microsoft.

**Blocker:** P0

## Cannot Log In to Aras

**Symptom:** Login dialog shows error after submitting credentials.

**Likely cause:** Wrong server URL, wrong database, network issue, or invalid credentials.

**Check:**
1. Confirm Aras server URL is reachable from a browser.
2. Confirm database name is correct.
3. Confirm the user account is active in Aras.

**Action:** Contact Aras admin to verify server URL, database, and account status.

**Blocker:** P0

## Part Library Does Not Load

**Symptom:** Part Library screen shows empty list, loading spinner does not resolve, or shows error.

**Likely cause:** Missing permissions on `idea_PartLibrary` ItemType, network issue, or server method not deployed.

**Check:**
1. Check Aras login success first.
2. Verify `idea_GetPrimaryIronCadForPart` method exists in the target database.
3. Verify user has Get permission on `idea_PartLibrary`.

**Action:** Deploy the required method. Grant appropriate permissions.

**Blocker:** P0

## CAD Lookup Unavailable

**Symptom:** Entry shows "CAD lookup unavailable" status even when a CAD exists.

**Likely cause:** Method `idea_GetPrimaryIronCadForPart` not deployed, wrong database, or insufficient Execute Method permission.

**Check:**
1. Confirm the method exists and is callable by the user.
2. Verify the method name is exactly `idea_GetPrimaryIronCadForPart`.

**Action:** Deploy the method. Grant Execute Method permission.

**Blocker:** P1

## Download CAD Fails

**Symptom:** Download button does nothing or shows an error.

**Likely cause:** Missing Vault read permission, network issue, or no native file on Aras.

**Check:**
1. Confirm the entry has a CAD with a native file (check CAD tab).
2. Verify user has Get permission on the `File` ItemType.
3. Verify Vault server is accessible.

**Action:** Grant Vault/file permissions. If the entry truly has no native file, this is expected behavior.

**Blocker:** P1

## Open IronCAD Fails

**Symptom:** Open in IronCAD shows error or IronCAD does not start.

**Likely cause:** IronCAD not installed, IronCAD path not configured, or file association missing.

**Check:**
1. Confirm IronCAD is installed on the machine.
2. Check `IsIronCadAvailable` status.
3. If using a non-default path, configure `ironCadExecutablePath` in `IdeaCadConnector.environment.json`.

**Action:** Install IronCAD or configure the executable path. If IronCAD is not expected, this is a non-blocking limitation.

**Blocker:** P2 (if IronCAD is not in scope for the tester)

## Open in Aras Opens Wrong URL

**Symptom:** Browser opens an incorrect or broken URL.

**Likely cause:** Incorrect Aras base URL in config or session.

**Check:**
1. Verify the Aras server URL used during login.
2. Check that the browser opens `https://server/InnovatorServer/resource.aspx?...`.

**Action:** Log in with the correct server URL. If the issue persists, verify Open-in-Aras URL construction in `ArasOpenUrlService`.

**Blocker:** P1

## Config JSON Malformed

**Symptom:** App starts but a warning or error is logged about environment config.

**Likely cause:** The `IdeaCadConnector.environment.json` file contains invalid JSON.

**Check:**
1. Open the file in a JSON validator.
2. Look for missing commas, trailing commas, unquoted strings.

**Action:** Fix the JSON. If the file is not needed, delete it — the app works without it.

**Blocker:** P2

## Active Config Contains Secret-like Keys

**Symptom:** App logs a warning about secret-like keys in the config.

**Likely cause:** Someone accidentally added `password`, `token`, `secret`, `session`, `cookie`, or similar keys.

**Check:** Search the config file for the warned key names.

**Action:** Remove the secret-like keys from the config. Use the Aras login dialog for credentials.

**Blocker:** P2

## Permission Denied for Role

**Symptom:** A command or view shows "permission denied" or is hidden/disabled when the user expects access.

**Likely cause:** The user's Aras identity does not match the expected role alias in the role mapping.

**Check:**
1. Confirm the user's login name.
2. Check the role mapping in `LibraryAuthorizationRules` defaults.
3. If using custom config, verify `roles` section in `IdeaCadConnector.environment.json`.

**Action:** Update the role mapping config or contact Aras admin.

**Blocker:** P2 (but P1 if it blocks the tester's assigned UAT scope)

## No CAD / No Native File Expected States

**Symptom:** Entry shows "No CAD" or "No native file" status.

**Likely cause:** The Part does not have a CAD relationship, or the CAD has no native file uploaded.

**Check:**
1. Open the Part directly in Aras via browser.
2. Check the Part CAD relationship tab.
3. If CAD exists, verify it has a native file (File tab on the CAD item).

**Action:** This is expected behavior for Parts without CAD or without native files. No action needed.

**Blocker:** Not a blocker — expected state.
