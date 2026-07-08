# UAT Checklist

## Package Smoke

- [ ] Zip extracts without errors.
- [ ] `VERSION.txt` exists and matches the expected release candidate.
- [ ] `checksums/SHA256SUMS.txt` exists.
- [ ] `app/IdeaCadConnector.Desktop.exe` exists.
- [ ] `aras/server-methods/idea_GetPrimaryIronCadForPart.cs` exists.
- [ ] `docs/INSTALL.md`, `CONFIGURATION.md`, `ROLLBACK.md`, `UAT-CHECKLIST.md`, and `RELEASE-NOTES.md` exist.

## App Smoke

- [ ] App launches from extracted `app/` folder.
- [ ] User can sign in to target Aras.
- [ ] PDM Projects screen opens.
- [ ] Part Library screen opens.
- [ ] Library list loads for an authorized user.
- [ ] Part Picker opens.
- [ ] Detail tabs load: CAD, BOM, Revisions, Where Used.
- [ ] Open in Aras opens the expected browser URL.
- [ ] Download CAD behavior is tested where Vault permissions allow.
- [ ] Open in IronCAD behavior is tested where IronCAD is installed.

## Role Smoke

- [ ] `TPTKC` manager behavior is acceptable.
- [ ] `TNTKC` reviewer behavior is acceptable.
- [ ] `NVTKC` contributor behavior is acceptable.
- [ ] `NVLCR` assembly/viewer behavior is acceptable where in scope.
- [ ] `PM` project viewer behavior is acceptable.
- [ ] Customer/unknown user behavior remains conservative and read-only.

## Package Result

- [ ] Ready for wider internal UAT.
- [ ] Blocked; blocker recorded in release notes or issue tracker.
