# Release Notes - IdeaCadConnector v0.3.0-rc1

## Summary

This release candidate packages the IdeaCadConnector desktop app for internal UAT after Part Library Phase 2 functional/live UAT acceptance.

## Included

- Part Library completed through Phase 2 functional scope.
- Library CRUD and Aras Part Picker.
- Move Entry and Revision Browser.
- CAD, BOM, Revisions, and Where Used detail tabs.
- Live CAD lookup via `idea_GetPrimaryIronCadForPart`.
- Filters and sorting.
- Role alignment to actual organization roles:
  - `ExampleManager`
  - `ExampleReviewer`
  - `ExampleContributor`
  - `ExampleAssemblyViewer`
  - `ExampleProjectViewer`
  - Customer
- Release package script, docs, checksum, and rollback baseline.

## Known Limitations

- IronCAD open depends on installed IronCAD or valid file association.
- CAD download depends on Vault/File permissions.
- Target Aras must have `idea_GetPrimaryIronCadForPart` deployed.
- Config remains mostly manual unless Sprint 3.2 adds external configuration hardening.
- External customer visibility depends on Aras permissions.
- This is a zip package, not an MSI/ClickOnce installer.
