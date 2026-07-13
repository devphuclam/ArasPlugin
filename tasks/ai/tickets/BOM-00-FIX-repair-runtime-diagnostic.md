# BOM-00-FIX — Repair IronCAD BOM diagnostic and complete runtime verification

Status: `BLOCKED_ADDIN_LOAD` on `hotfix/bom-00-runtime-diagnostic-fixes`.

## Baseline

- Repository baseline: `origin/main` at `3ff125d3fa15577ece82d9beaeda3c07a32a60e1`.
- Scope is limited to the provider-neutral BOM diagnostic, IronCAD adapter, add-in loading/deployment, tests, and BOM-00 evidence.
- BOM-01, Naming Policy v2, production BOM manifest, externalization automation, Push/Pull, Aras schema, and server methods are out of scope.

## Confirmed defects to repair

- ICAPI `eZElementType` values such as `Z_ELEMENT_PART` and `Z_ELEMENT_ASSEMBLY` are not mapped to provider-neutral kinds.
- Live reader data does not populate identity candidates used by quantity analysis.
- Diagnostic output prepends plain text and is not valid JSON.
- Public aggregate warnings currently copy raw COM exception text.
- Raw output accepts repository, study, build, and application-data folders.
- Runtime output validation was not receiving active study/repository/AppData context from the ICAPI probe.
- Reader recursion has no cycle, depth, or node-count guard before the Workspace analyzer.
- Scene/root element types were being counted as assemblies.
- Every build attempted best-effort registry writes, making normal builds mutate HKCU and hide registration failures.
- IronCAD does not load the intended add-in in the runtime verification process.

## Add-in loading-chain finding

The per-user application registration, CLSID/ProgId, x64/net48 build, manifest, and SDK COM categories are present. The ProgId can instantiate the managed class from an external process, but IronCAD does not discover/load the class and no `InitSelf`/typed `_addinSite.Application` evidence exists. Official SDK guidance requires selecting the add-in once through IronCAD's Add-In Manager; that host-side selection was not observed. No legacy `IronCAD.Application` substitute is used.

Registration correction is per-user and administrator-free. Rollback:

```text
reg delete "HKCU\SOFTWARE\IronCAD\IRONCAD 27.0\Applications\IdeaCadConnector" /f
reg delete "HKCU\Software\Classes\CLSID\{B1A006AC-1386-4811-AA71-8CF55414ACEF}" /f
reg delete "HKCU\Software\Classes\IdeaCadConnector.IronCAD.AddIn" /f
```

Registration is now opt-in: normal builds do not touch HKCU; use `RegisterIronCadAddin=true` with IronCAD closed, and use `UnregisterIronCadAddin=true` for rollback. Registration commands fail on registry errors and print the target configuration/path and rollback command.

## Safety requirements

The probe remains read-only. It must not call CAD save/export/relink/write methods, custom-property setters, Aras APIs, or externalization automation. Raw reports remain outside the repository and sanitized evidence must not contain names, paths, usernames, or proprietary metadata.

## Acceptance

- [ ] All new defect regressions have RED/GREEN evidence.
- [x] RED/GREEN regression coverage passes: focused BOM diagnostics 30/30 and full Debug suite 512/512.
- [x] Debug solution build passes with zero errors; Release verification remains a required follow-up before merge.
- [ ] Add-in loading chain is documented with observed evidence and rollback steps.
- [ ] A disposable study copy is traversed only after the intended add-in and typed ICAPI seam are confirmed loaded.
- [ ] Original study hash is unchanged.
- [x] SceneRoot is counted separately and excluded from AssemblyCount; raw output is blocked when active study context is unavailable.
- [x] Final status is `BLOCKED_ADDIN_LOAD`; host-side Add-In Manager activation remains the only unobserved loading step.
