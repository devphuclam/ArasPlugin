# IT Handoff Guide

## What to Send to the Tester

Send the release zip file:

```text
IdeaCadConnector-v0.3.0-rc1.zip
```

Include a brief message with:

- The extraction destination (see INSTALLATION-HARDENING.md)
- The Aras server URL and database name
- The tester's role identity (TPTKC/TNTKC/NVTKC/NVLCR/PM/Khách hàng)
- A link to the UAT checklist (docs/UAT-CHECKLIST.md)

Do **not** send:

- Passwords or tokens
- Active `IdeaCadConnector.environment.json` with real server values
- Source code
- Build logs

## What Not to Send

- Source folders (`src/`, `tests/`, `tools/` source)
- Build artifacts (`bin/`, `obj/`)
- `.vs/` solution cache
- `TestResults/`
- Personal developer configuration
- Active config files
- Any file containing real passwords or tokens

## Required Aras Preparation

Before the tester starts, confirm:

1. Server method `idea_GetPrimaryIronCadForPart` is deployed to the target database.
2. The tester's Aras account has:
   - Execute Method permission for `idea_GetPrimaryIronCadForPart`
   - Get permission on `Part`, `CAD`, `Part CAD`, `File`
   - Get permission on `idea_PartLibrary`, `idea_PartLibraryEntry`
3. The tester's Aras username matches one of the expected role aliases:
   - TPTKC, TNTKC, NVTKC, NVLCR, PM, or Customer/KhachHang
4. The Part Library Items are populated with test data (Libraries and Entries).

## Required Machine Preparation

1. Windows 10+ or Windows Server 2016+ (x64)
2. .NET Framework 4.8 Runtime installed
3. Network access to the Aras Innovator server URL
4. Default browser configured
5. IronCAD installed if Open in IronCAD will be tested
6. Sufficient disk space (~200 MB)

## Smoke Test Steps

After the tester extracts the zip, ask them to:

1. Run `app\IdeaCadConnector.Desktop.exe`.
2. Log in with their Aras credentials.
3. Open the Part Library screen.
4. Confirm the Library list loads.
5. Select an Entry and confirm tabs load.
6. Test Open in Aras for a Part, Entry, Library, and CAD.
7. Run the validation script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\validate-release-package.ps1 -PackagePath .\IdeaCadConnector-v0.3.0-rc1
   ```
8. Fill in the UAT result template (INTERNAL-UAT-RESULT-TEMPLATE.md).

## Rollback Steps

If a deployment issue is found:

1. Ask the tester to stop the app.
2. Delete the extracted release folder.
3. Revert to the previous release folder or zip.
4. Restore any user config file from backup.
5. Verify the previous version still works.

If the issue is Aras-side (missing method, permission error):

1. Fix the Aras configuration.
2. Ask tester to re-test without re-extracting the app.

## Escalation Path

| Issue | Contact |
|---|---|
| App crash or bug | Developer (via issue tracker) |
| Missing DLL or packaging issue | Developer |
| Aras permission or method issue | Aras administrator |
| Network or machine issue | IT support |
| UAT result questions | Project owner |
