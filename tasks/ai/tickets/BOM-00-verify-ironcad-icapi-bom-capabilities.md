# BOM-00 — Verify IronCAD ICAPI BOM capabilities

Status: Implemented diagnostic spike; runtime study verification blocked on `2026-07-13`.

## Scope

This ticket adds a provider-neutral, pure tree analyzer and a read-only IronCAD ICAPI diagnostic seam. It does not create a production BOM manifest, alter Naming Policy, modify Aras, externalize the supplied study, or implement Push/Pull.

## Evidence status

- Baseline: `origin/main` at `ee23a38`.
- IronCAD executable: `27.0.26.19811`.
- `interop.ICApiIronCAD.dll`: assembly/file version `27.0.0.0`.
- Pure analyzer: implemented and unit-tested.
- IronCAD reader/probe: implemented and solution-compiled.
- Runtime study probe: `BLOCKED_RUNTIME_VERIFICATION`; no active `IronCAD.Application` COM object was available.
- Study aggregate counts: not recorded because runtime verification did not run.
- Original study: unchanged and not committed.

## Read-only guarantee

The probe calls only `ActiveDoc`, document metadata, `GetTopElement`, element read properties, `GetStateStatus`, `GetCustomPropManager`/`Count`, `GetExternallyLinkedInfo`, `ModelLinkPath`, `GetChildrenZArray`, `IZArray.Count`, and `IZArray.Get`. It does not call `Save`, `SaveAs`, `SaveAsCopy`, BOM export, `ChangeSource`, `Unlink`, custom-property setters, rename operations, Aras calls, or geometry operations.

## Acceptance

- [x] Provider-neutral source node and analyzer are free of COM/ICAPI references.
- [x] Tree analyzer covers recursion, parent/depth, deterministic provider order, null children, cycles, duplicate runtime IDs, technical/unknown nodes, repeated definitions, per-parent quantities, and unknown identity.
- [x] Sanitized aggregate evidence excludes raw names and absolute paths.
- [x] Local diagnostic output uses `FileMode.CreateNew` and cannot overwrite an existing report.
- [x] IronCAD reader is isolated to `IdeaCadConnector.IronCAD`.
- [x] Invocation is DEBUG-only, developer-only, requires an explicit output folder, and has no ribbon/UI registration.
- [x] Debug solution build passed.
- [x] Focused analyzer tests passed: 14 passed, 0 failed, 0 skipped.
- [ ] Runtime study verification; blocked until IronCAD add-in and disposable study copy are active.
- [ ] Externalization, relink, and persistence manual procedure; intentionally deferred and not automated in BOM-00.

## Follow-up

Resume the manual read-only study probe before BOM-01. Keep identity, reuse, quantity, externalization, relink, and persistence as explicit unknowns until observed. Do not begin BOM-01 from reflection evidence alone.
