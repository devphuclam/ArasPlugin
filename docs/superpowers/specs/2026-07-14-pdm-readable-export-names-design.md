# PDM Readable Export Names Design

## Goal

Make normalized IronCAD export packages easy for people to recognize while retaining deterministic names for IDEA PDM.

## Naming contract

- Final package directory: `<OutputFolder>/<ProjectCode>`.
- CAD filename: `<ProjectCode>__<ItemCode>__<DisplayName>.ics`.
- Do not include the `ASM` or `PRT` item-type token in CAD filenames.
- Keep item type in `pdm-bom-manifest.json`; IDEA PDM must continue to use manifest metadata rather than infer type from the filename.

Examples:

- `PDM-STUDYCASE/`
- `PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics`
- `PDM-STUDYCASE__A01__MAIN-BODY-BASE.ics`
- `PDM-STUDYCASE__C03__CONTROLLER.ics`

## Re-export behavior

The exporter must build and verify the complete new package in its private staging location before changing an existing final package.

After verification succeeds:

1. Delete the existing `<OutputFolder>/<ProjectCode>` directory, if present.
2. Publish the verified pending package as `<OutputFolder>/<ProjectCode>`.
3. Do not retain a backup of the previous package.

If creation or verification fails before step 1, the existing final package remains untouched. If publication fails after deletion, return the existing structured package-commit error and clean staging/pending artifacts.

## Collision safety

Removing the item-type token can make an assembly and a part resolve to the same filename. Preflight must detect duplicate canonical filenames and block export before the existing final package is deleted.

## Compatibility

- Manifest schema remains version 2.
- `itemType`, item code, display name, definitions, occurrences, and BOM edges remain unchanged.
- IDEA PDM's manifest reader requires no filename parsing changes because it reads the manifest's `fileName` field.
- Legacy non-manifest naming behavior remains unchanged.

## Verification

- Unit tests prove the new canonical filename format.
- Command-level tests prove the package directory is exactly the project code.
- Transaction tests prove an existing final package is deleted only after the new package is ready to publish.
- Debug and Release builds and the full test suite must pass.
