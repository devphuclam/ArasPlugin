# UAT Checklist

## Package Smoke

- [x] Zip extracts without errors.
- [x] `VERSION.txt` exists and matches the expected release candidate.
- [x] `checksums/SHA256SUMS.txt` exists.
- [x] `app/IdeaCadConnector.Desktop.exe` exists.
- [x] `aras/server-methods/idea_GetPrimaryIronCadForPart.cs` exists.
- [x] `docs/INSTALL.md`, `CONFIGURATION.md`, `ROLLBACK.md`, `UAT-CHECKLIST.md`, and `RELEASE-NOTES.md` exist.

## App Smoke

- [x] App launches from extracted `app/` folder.
- [x] User can sign in to target Aras.
- [x] PDM Projects screen opens.
- [x] Part Library screen opens.
- [x] Library list loads for an authorized user.
- [x] Part Picker opens.
- [x] Detail tabs load: CAD, BOM, Revisions, Where Used.
- [x] Open in Aras opens the expected browser URL.
- [x] Download CAD behavior is tested where Vault permissions allow.
- [x] Open in IronCAD behavior is tested where IronCAD is installed.

## Role Smoke

- [x] `TPTKC` manager behavior is acceptable.
- [x] `TNTKC` reviewer behavior is acceptable.
- [x] `NVTKC` contributor behavior is acceptable.
- [x] `NVLCR` assembly/viewer behavior is acceptable where in scope.
- [x] `PM` project viewer behavior is acceptable.
- [x] Customer/unknown user behavior remains conservative and read-only.

## Sprint 3.2 Environment Config Smoke

- [x] Config template exists in package: `docs/templates/IdeaCadConnector.environment.template.json`
- [ ] Active config (`IdeaCadConnector.environment.json`) not present in package
- [ ] Config model loads without crashing when file is missing
- [ ] Config model loads from explicit `IDEA_CAD_CONNECTOR_ENV_CONFIG` path
- [ ] Config model loads from `%APPDATA%/IdeaCadConnector/` fallback
- [ ] Malformed JSON produces clear error (not crash)
- [ ] Secret-like key (`password`, `token`, `secret`) produces warning
- [ ] Role defaults work without any config file
- [ ] Path expansion works for `%LOCALAPPDATA%` and `%APPDATA%`
- [ ] `schemaVersion` other than 1 produces validation error
- [ ] Empty `ironCadExecutablePath` does not crash

## Package Result

- [x] Ready for wider internal UAT.
- [ ] Blocked; blocker recorded in release notes or issue tracker.
