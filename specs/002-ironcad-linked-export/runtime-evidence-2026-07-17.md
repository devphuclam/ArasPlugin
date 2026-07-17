# Runtime Evidence and Session Handoff — 2026-07-17

## Repository State

- Repository root: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\Workspace\ArasPlugin`
- Branch: `002-ironcad-linked-export`
- HEAD at handoff: `d2d5e2ce1924ad0ca8482f89234b9123ff343585`
- Working tree: dirty and intentionally uncommitted. It contains the linked-export implementation, tests, Spec Kit artifacts, and pre-existing setup changes. No commit was created during this handoff.
- Canonical artifacts: `spec.md`, `research.md`, `plan.md`, `tasks.md`, `data-model.md`, `quickstart.md`, `contracts/README.md`, and this evidence file.

## Approved Runtime Behavior

The user accepted the implementation after opening the exported canonical root in IronCAD and confirming both visible source geometry and true external-link entries. The implementation follows the native `Assembly > Save All As External` result, then applies canonical PDM filenames and metadata.

Implementation flow:

1. Build the approved occurrence-to-definition-file map.
2. Stage the canonical root in package `cad/`.
3. Temporarily rename definitions to canonical filename stems.
4. Invoke IronCAD 2025 native command ID 53046 and select package `cad/`.
5. Verify every expected definition file exists.
6. Restore approved scene names and all six PDM properties.
7. Update and save the root using `Z_LINKS_SAVE_ALL`.
8. Write and validate the manifest and round-trip external references.

`IronCadNativeSaveAllExternalInvoker.Execute(destinationDirectory)` is the deep module at the native UI seam. It owns window discovery, `WM_COMMAND`, modal folder selection, and timeout/error behavior. `IronCadSceneNormalizationWriter` only supplies the destination and coordinates CAD/domain steps.

## DEMO Evidence

- Source: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\Demo\DEMO.ics`
- Native reference output: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\DEMO-PDM-Export`
- Accepted plugin package: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\DEMO-PDM-Export\DEMO`
- Native reference: 87 externalized definition `.ics` files.
- Accepted package: 88 `.ics` files under `cad/` (one canonical root plus 87 canonical definitions).
- Total accepted `.ics` size: 30,359,552 bytes.
- Root: `cad\DEMO__ROOT__DEMO.ics`, 2,371,584 bytes, last written 2026-07-17 15:12:32 local time.
- Manifest: `pdm-bom-manifest.json`, 67,690 bytes, last written at the same time.
- Writer progress reached `NATIVE_COMMAND_COMPLETED` and `NATIVE_SAVED_ALL`.
- Package validation succeeded and retained the package; no new failure log was produced for the accepted run.

## Rejected Approaches and Failure Evidence

- Scene reconstruction through `Pages.Add()` plus `Shapes.Add()` / `ImportFile()` created linked tree shells but blank/missing production geometry.
- Per-definition `IZPart.SaveAs()` / `IZAssembly.SaveAs()` plus reopen/externalize caused file locks/share violations and did not reliably link the root.
- `SaveAsCopy`/`SaveAs` link options alone did not convert embedded occurrences to native external definitions.
- `IZBaseApp.RunCommand((eZCommand)53046)` failed with `Invalid input arguments`; 53046 is a native MFC command/resource ID, not a valid public `eZCommand` value.
- External COM/ROT probes were unstable, could hang IronCAD, and are not part of the production flow.
- Camera fit/update attempts could not repair missing geometry because the defect was reconstruction, not camera state.

## Completed Scope

- Manifest occurrence/definition mapping and dedup model.
- Definition-file map builder and result DTO.
- Native externalization writer and canonical naming flow.
- External-reference reader, pure validator, round-trip verifier, and document lifecycle seam.
- Package cleanup/error handling changes covered by repository tests.
- Runtime DEMO confirmation of hierarchy, visible geometry, canonical physical files, and true external references.

## Open Acceptance Work

- T005: numeric transform comparison.
- T006: dedicated one-definition/three-occurrence dedup fixture.
- T007/T036/FR-013: edit a child through root and record child SHA256 before/after.
- T034–T035: explicit source-external-dependency guard regression/integration coverage.
- T040: broken-child indicator behavior.
- T041: round-trip verification under five seconds for 50 children.
- T042: external-dependency block under two seconds.
- T039: final change-scope review remains a reviewer task because the working tree includes pre-existing and feature-wide changes.

## Constraints and Prohibited Next Actions

- Do not reintroduce production scene reconstruction through `Pages.Add`, `Shapes.Add`, or `ImportFile`.
- Do not call command ID 53046 through `eZCommand`; the accepted path is native `WM_COMMAND` behind the invoker module.
- Do not lift `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` in this feature.
- Do not claim FR-013, shared-occurrence dedup, broken-link behavior, or performance criteria complete until their evidence is captured.
- Do not discard or reset the dirty working tree; preserve user changes and review scope before committing.
- The accepted invoker currently targets English IronCAD 2025 dialog captions; localization needs separate validation before changing that contract.

## Verification

Fresh verification after implementation/documentation updates:

- `dotnet build IdeaCadConnector.sln`: succeeded, 0 warnings, 0 errors (11.04 seconds).
- `dotnet test IdeaCadConnector.sln`: 674 passed, 0 failed, 0 skipped (test duration 5 seconds).
- Package/manifest filesystem check: 88 definitions, 88 `.ics` files, 0 missing manifest definition files.

Exact evidence is retained in `baseline-evidence.md`.
