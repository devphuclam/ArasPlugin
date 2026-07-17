# Quickstart: IronCAD Linked Normalized Export

**Date**: 2026-07-16; updated 2026-07-17
**Feature**: [spec.md](spec.md) | [plan.md](plan.md) | [data-model.md](data-model.md)

## Prerequisites

- Visual Studio 2022+ with .NET Framework 4.8 targeting
- IronCAD 2025 installed (ICAPI at `%ProgramFiles%\IronCAD\2025\ICAPI\Samples\C#\References\`)
- Repository build: `dotnet build IdeaCadConnector.sln` (must pass)
- Repository tests: `dotnet test IdeaCadConnector.sln` (must pass; final count is recorded in `baseline-evidence.md`)

## Phase 0 — ICAPI Runtime Test Suite

The writer route is now fixed: use IronCAD 2025's native `Save All As External` behavior on the staged canonical root. Continue using T1–T6 as the release/UAT checklist; do not substitute scene reconstruction with `Shapes.Add()` or `ImportFile()`.

### Quick reference

| Test | What it validates | Pass condition | Blocking? |
|------|-------------------|----------------|-----------|
| T1 | Hierarchy preservation | Exported tree matches source | Yes — blocks writer approach |
| T2 | Transform preservation | Positions/rotations match | Yes — blocks writer approach |
| T3 | Shared occurrence dedup | N occurrences → 1 file | Yes — core feature requirement |
| T4 | Save-through-root | Child SHA256 changes after root save | Yes — FR-013 mandatory gate |
| T5 | Custom property round-trip | All 6 PDM properties survive | Yes — FR-010 |
| T6 | External link isolation | No links outside `cad/` | Yes — FR-005 |

### Selected implementation

`IronCadSceneNormalizationWriter` stages the root and canonical names, then `IronCadNativeSaveAllExternalInvoker` invokes native command ID 53046 and selects package `cad/`. The writer verifies emitted definition files, restores PDM names/properties, and saves all links. See `research.md` for accepted DEMO evidence and rejected alternatives.

## Unit Tests

All existing tests must pass:
```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~PdmNormalization"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~PdmNormalizeExportSafety"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~PdmIronCadAdapter"
```

Expected: 0 failures, 0 skipped.

## Manual UAT (FR-013)

The production DEMO runtime already confirms that the canonical root opens with visible geometry and true external links. The SHA256 edit-through-root steps below are still mandatory before checking FR-013.

### Scenario: Multi-level assembly with linked export

1. **Setup**: Open IronCAD, create a new scene with:
   - Root assembly: `MYASM__ROOT__Main.ics`
   - Sub-assembly: itemCode=`A01`, displayName=`Sub1` (1 child part inside)
   - Part: itemCode=`P01`, displayName=`Bracket`
2. **Save** the scene
3. **Run "Chuẩn hóa & Xuất PDM"** from the IDEA PDM ribbon
4. **Verify package**:
   - `cad/` contains 3 `.ics` files: root, sub-assembly, bracket
   - Open root `.ics` → each child occurrence shows as external reference
   - Inspect `ModelLinkPath` → paths point to files inside `cad/`
5. **Verify edit-through-root**:
   - Record SHA256 of `cad/MYASM__P01__Bracket.ics`
   - Edit the bracket part through the root scene
   - Save the root scene
   - Verify SHA256 of `cad/MYASM__P01__Bracket.ics` has changed

### Scenario: Shared definition

1. **Setup**: Scene with 1 root + 1 part `Bolt` (itemCode=`P01`) placed at 3 positions
2. **Export**
3. **Verify**: `cad/` contains only 2 `.ics` files (root + 1 Bolt)
4. **Verify**: All 3 bolt occurrences reference the same file path
5. **Verify**: Edit any bolt occurrence → save root → SHA256 of the single Bolt `.ics` changes → reload root → all 3 occurrences show the edit

### Scenario: Unresolvable child detection

1. **Setup**: Scene with an occurrence pointing to a deleted file
2. **Attempt export**
3. **Expected**: Export blocked with error naming the broken component and missing file path

## Verification Checklist

- [x] Native runtime decision and rejected alternatives documented in `research.md`
- [x] FR-001: Accepted DEMO root has visible geometry and true external links
- [ ] FR-002/FR-003: Shared definitions produce 1 file for N occurrences
- [ ] FR-004: No embedded child data in root
- [x] FR-005: Accepted DEMO package passed link isolation validation and was retained
- [ ] FR-006: Unresolvable child blocks export
- [x] FR-007/FR-009: Reader/validator/verifier flow and mismatch tests are implemented
- [x] Final full repository build/tests pass: build 0 warnings/errors; 674 tests passed (see `baseline-evidence.md`)
- [ ] FR-013: UAT test evidence captured (SHA256 before/after)
