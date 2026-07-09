# Installation Hardening

## Where to Extract

Extract the release zip to a dedicated folder outside `Program Files` unless the IT policy explicitly requires admin-only access.

**Suggested location:**

```text
C:\IdeaCadConnector\IdeaCadConnector-v0.3.0-rc1\
```

This avoids permission issues with UAC, file writes, and vault cache creation.

## How to Run

```text
app\IdeaCadConnector.Desktop.exe
```

Double-click the executable or create a shortcut to `C:\IdeaCadConnector\IdeaCadConnector-v0.3.0-rc1\app\IdeaCadConnector.Desktop.exe`.

## Copy Config Template (Optional)

If you want non-default environment settings:

1. Copy `docs\templates\IdeaCadConnector.environment.template.json` to the `app\` folder.
2. Rename to `IdeaCadConnector.environment.json`.
3. Edit the values for your Aras server, database, and local paths.
4. Do **not** add passwords, tokens, or secrets.

The app works without this file using built-in defaults.

## What Not to Edit

- Do not edit `app\*.dll` or `app\*.exe` files.
- Do not edit `checksums\SHA256SUMS.txt` — it is for integrity verification.
- Do not edit `VERSION.txt` — it records the build metadata.
- Do not add passwords to `IdeaCadConnector.environment.json`.

## Verify Package Contents

Run the validation script from the extracted root:

```powershell
powershell -ExecutionPolicy Bypass -File tools\validate-release-package.ps1 -PackagePath .\IdeaCadConnector-v0.3.0-rc1
```

The script checks file structure, required files, forbidden files, and secrets.

## Rollback

To roll back to a previous version:

1. Stop the desktop app.
2. Delete the extracted release folder:
   ```text
   C:\IdeaCadConnector\IdeaCadConnector-v0.3.0-rc1\
   ```
3. Extract the previous release zip to the same parent folder.
4. Restore any user-specific `IdeaCadConnector.environment.json` from backup.

Rollback affects only the app files. User workspaces, vault cache, and Aras data are untouched.
