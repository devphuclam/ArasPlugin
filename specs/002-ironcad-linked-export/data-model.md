# Data Model: IronCAD Linked Normalized Export

**Date**: 2026-07-16
**Feature**: [spec.md](spec.md)

## Core Model: IZElement ↔ Occurrence ↔ Definition File

This feature introduces and formalizes three distinct but related concepts. Understanding their relationship is critical for implementation.

### Identity Layers

| Layer | What it is | Scope | Persistence |
|-------|-----------|-------|-------------|
| **IZElement** (COM interop object) | The in-memory IronCAD scene element (`IZPart`, `IZAssembly`, `IZSceneElement`). Each element has an object identity (`object.ReferenceEquals`). | Active IronCAD session only | ❌ Gone when document closes |
| **PdmPlanItem (occurrence entry)** | A row in the normalization plan representing one scene-tree position. Has `OccurrencePath`, `NodeId`, `ParentNodeId`, etc. Linked to an `IZElement` via `SourceNode` reference. | Plan object lifetime | ✅ Persisted in `pdm-bom-manifest.json` |
| **Definition .ics file** | A `.ics` file on disk containing geometry, BOM tree, and PDM custom properties. One file per unique `IZElement` identity. | File system | ✅ Persists after export |

### Cardinality Rules

```
Scene Tree (IronCAD session)
  │
  ├── IZElement#A (Root Assembly)
  │     ├── IZElement#B (Part) ─── occurrence 0/0 ──┐
  │     ├── IZElement#C (Part) ─── occurrence 0/1 ──┤
  │     └── IZElement#C (Part) ─── occurrence 0/2 ──┘── same IZElement#C
  │
  ▼                              ▼                    ▼
Export Writer             Plan Items              Package cad/
───────────────────────────────────────────────────────────────
IZElement#A               Root item               MYASM__ROOT__Main.ics
IZElement#B (unique ref)  Child item               MYASM__P01__A.ics
IZElement#C (shared ref)  N child items (×2)       MYASM__P02__B.ics (ONLY ONE FILE)
```

**Key invariants**:
1. **N occurrences → M IZElements (M ≤ N)** — multiple occurrences may share the same `IZElement` if they are instances of the same definition
2. **M IZElements → M definition files** — each unique `IZElement` identity produces exactly one `.ics` file
3. **After export reopen**: The exported root `.ics` produces *new* `IZElement` objects (different references). The definition-identity link only exists during the export session — the round-trip verifier uses `occurrencePath` + `canonicalFileName` to validate, not `IZElement` reference equality
4. **Occurrence vs definition in manifest**: Each occurrence must record which definition file it uses via `definitionFile`. Multiple occurrences sharing the same `definitionFile` is the expected pattern

### Export Writer Algorithm (Conceptual)

```
Build definition map from snapshot.ElementIds + approved plan
Stage canonical root under package cad/ with Z_LINKS_IGNORE
Temporarily rename definitions to canonical filename stems
Invoke IronCAD 2025 native Save All As External for package cad/
Verify every expected definition file exists
Restore approved scene names and six PDM properties
Update and save root with Z_LINKS_SAVE_ALL
```

**Note**: Native externalization preserves the existing scene instead of reconstructing occurrences. The accepted DEMO runtime showed the original visible design and linked hierarchy. T005 still requires a numeric transform comparison before the transform acceptance gate is formally complete.

---

## Entities

### Exported Package

A directory containing the normalized CAD files and PDM manifest. Package layout:

```
<output-folder>/
├── cad/
│   ├── <PROJECT>__<CODE>__<DISPLAY>.ics   # root scene (external-linked)
│   ├── <PROJECT>__<CODE>__<DISPLAY>.ics   # child definition file
│   └── ...                                 # one .ics per unique IZElement
└── pdm-bom-manifest.json                   # PDM manifest
```

**Validation rules**:
- `cad/` must contain exactly one root `.ics` plus one `.ics` per unique `IZElement` identity
- Every external link in root `.ics` must resolve to a file within `cad/`
- `pdm-bom-manifest.json` must list every `.ics` file and every occurrence

---

### Component Definition (IZElement)

A unique part or assembly definition in the IronCAD scene tree, identified by `object.ReferenceEquals(IZElement, IZElement)` (⚠️ **hypothesis — see T3**).

**Identity scope**: Valid only within a single active IronCAD session. Does NOT survive save/close/reopen.

**Cardinality**: One definition → one `.ics` file. N occurrences → same `.ics` file.

**Fields (PDM custom properties written by Apply)**:
| Property | Type | Source |
|----------|------|--------|
| `PDM.NodeId` | string (GUID) | Normalization plan |
| `PDM.ItemCode` | string | Normalization plan |
| `PDM.ItemType` | string ("ASM"/"PRT") | Normalization plan |
| `PDM.DisplayName` | string | Normalization plan |
| `PDM.ProjectCode` | string | Normalization plan |
| `PDM.Revision` | string | Normalization plan |

**Validation**:
- All 6 PDM properties must be present on every element in exported files (FR-010)
- `ItemType` must be "ASM" or "PRT" per `PdmNameNormalizer.CreateCanonicalFileName`

---

### Occurrence

A specific position/instance of a component definition in the scene tree. Multiple occurrences can share the same definition.

**Key attributes** (tracked in normalization plan):
| Field | Type | Description |
|-------|------|-------------|
| `OccurrencePath` | string | Path from root, e.g. `"0/0/2"` |
| `NodeId` | string (GUID) | Unique per occurrence |
| `ParentNodeId` | string | Parent occurrence's NodeId |
| `Depth` | int | Tree depth |
| `SourceKind` | enum | `SceneRoot`, `Assembly`, `Part`, `Technical` |
| `SourceNode` | `PdmSourceNode` | In-memory source node reference (session-only). Used as key in `IDictionary<PdmSourceNode, string>` (defFileMap) to look up the definition file name for this occurrence. The corresponding `ElementId` (opaque identity token) is resolved via `IronCadSceneSnapshot.ElementIds[sourceNode]` in the IronCAD layer. |
| `DefinitionFile` | string | Canonical file name of the definition `.ics` (linked export only) |

**Validation**:
- Each occurrence has a unique `NodeId` and `OccurrencePath`
- Multiple occurrences may share the same `DefinitionFile` value (same `SourceNode`)
- The manifest must record each occurrence individually

---

### Normalization Plan (PdmNormalizationPlan)

Unchanged from current implementation. Contains:
- `ProjectCode` — normalized project identifier
- `Revision` — revision letter (default "A")
- `Root` — root `PdmPlanItem`
- `Items` — all child `PdmPlanItem` entries
- `Warnings` — duplicate codes, generic names

**No structural changes needed for linked export.** The plan already contains `OccurrencePath`, `CanonicalFileName`, and `SourceNode` (the `PdmSourceNode` reference, unique per occurrence).

The export writer groups `PdmSourceNode` instances that share the same `ElementId` (via `IronCadSceneSnapshot.ElementIds`) to determine which identity group → which `.ics` file. The resulting `IDictionary<PdmSourceNode, string>` maps each source node to its `DefinitionFile`. Multiple `PdmSourceNode` entries may map to the same `DefinitionFile` (indicating shared-definition occurrences).

---

### PDM Manifest (pdm-bom-manifest.json)

**Current**: Flat list of items — one entry per occurrence.

**Required for linked export**: Must distinguish two concepts:
1. **Definition file entries** — one per unique `.ics` file (keyed by canonical filename)
2. **Occurrence entries** — one per scene tree position, each referencing a definition file

**Proposed manifest structure**:

```json
{
  "projectCode": "MYASM",
  "revision": "A",
  "files": [
    {
      "canonicalName": "MYASM__ROOT__Main.ics",
      "isRoot": true,
      "sha256": "abc..."
    },
    {
      "canonicalName": "MYASM__P01__Bracket.ics",
      "isRoot": false,
      "sha256": "def..."
    }
  ],
  "occurrences": [
    {
      "nodeId": "...",
      "occurrencePath": "0",
      "parentNodeId": null,
      "kind": "SceneRoot",
      "itemCode": "ROOT",
      "displayName": "Main",
      "canonicalFileName": "MYASM__ROOT__Main.ics",
      "children": ["...", "..."]
    },
    {
      "nodeId": "...",
      "occurrencePath": "0/0",
      "parentNodeId": "...",
      "kind": "Part",
      "itemCode": "P01",
      "displayName": "Bracket",
      "canonicalFileName": "MYASM__P01__Bracket.ics",
      "definitionFile": "MYASM__P01__Bracket.ics",
      "children": []
    }
  ]
}
```

**Key change**: `occurrences[].definitionFile` field added to link each occurrence to its definition `.ics` file. Multiple occurrences will reference the same `definitionFile`.

**Backward compatibility**: The existing manifest format uses `items[]` with occurrence-level data. The new format must either:
- Add `definitionFile` to existing items, or
- Restructure to `files[]` + `occurrences[]` with migration of consumers (push, clone)

**Decision**: Use existing `PdmPackageManifest` (SchemaVersion=2) which already has `Definitions[]` + `Occurrences[]` structure. The only change is to `PdmManifestV2Factory.GetDefinitionId()` — currently creates one definition per occurrence path (`"def-" + item.OccurrencePath.Replace('/', '-')`), must change to deduplicate by `ElementId` (the `sourceNodeToDefFileMap` parameter, pending the Phase 0 gate). Add `definitionFile` field to `PdmManifestOccurrence` (C# property `DefinitionFile`, JSON key `"definitionFile"`) linking each occurrence to its definition `.ics` file. The `files[]` array in the proposed model above is informational — root + child files can be derived from `Definitions[]`.

---

### IronCadExternalReferenceValidationContext

A value-type struct bundling all path information the Validator needs, avoiding six individual string parameters:

| Field | Type | Description |
|-------|------|-------------|
| `DocumentDirectory` | string | Absolute directory of the opened exported-root `.ics` (for resolving relative `ReportedLinkPath`) |
| `PackageRoot` | string | Package output root directory |
| `CadRoot` | string | `cad/` subdirectory within the package |
| `SourceRoot` | string | Source workspace root (for isolation check) |
| `StagingRoot` | string | Staging workspace root (for isolation check) |

The command builds this context from the package/output paths before calling the verifier.

### External Link Record

Used by `IronCadExportPackageVerifier` during round-trip validation. The type already exists in `IronCadExportPackageVerifier.cs` (lines 16–25). The **Reader** produces raw records from the opened scene (does NOT know the plan). The **Validator** then enriches and checks them against the plan.

| Field | Type | Producer | Description |
|-------|------|----------|-------------|
| `OccurrencePath` | string | Reader | Path of the occurrence in the exported scene |
| `ReportedLinkPath` | string | Reader | Raw `ModelLinkPath` / `GetExternallyLinkedInfo` value from ICAPI |
| `ResolvedTargetPath` | string | Validator | Full resolved path on disk |
| `Exists` | bool | Validator | Target file exists at resolved path |
| `InsidePackage` | bool | Validator | Resolved path lies within the package directory |
| `PointsToSource` | bool | Validator | Link points outside the package to the source workspace |
| `CanonicalFileNameMatch` | bool | Validator | Resolved filename matches expected canonical name |

**Reader** (raw-only COM adapter): sets only `OccurrencePath` and `ReportedLinkPath`. Does NOT check file existence, resolve paths, or compare against the plan.

**Validator** (`IronCadExternalReferenceValidator`) — pure logic, no COM dependencies. Receives: raw records + `PdmNormalizationPlan` + **`IronCadExternalReferenceValidationContext`**. For each record:
- Calls `PdmExternalReferencePolicy.Evaluate(ReportedLinkPath, context.DocumentDirectory, context.CadRoot, context.SourceRoot, context.StagingRoot, expectedCanonicalName)` — the policy resolves the raw link relative to `DocumentDirectory`, returns `ResolvedTargetPath`, and applies `CadRoot` as the package `cad/` isolation root. This preserves the existing sourceRoot/stagingRoot isolation checks.
- **Exact occurrence-set validation**: exclude the root occurrence (`plan.Root` / path `"0"`) from the external-link expected set; every non-root plan child OccurrencePath must have exactly one matching record (missing → fail); every record's non-root OccurrencePath must exist in the plan (unexpected → fail); no duplicate OccurrencePath values (duplicate → fail)

The Reader emits a record even when an occurrence has no external link; in that case `ReportedLinkPath` is null or empty. The Validator reports a missing-link failure for non-root occurrences; the root occurrence (`plan.Root` / path `"0"`) is excluded from the external-link expected set.

## Relationship Diagram (Finalized)

```
┌──────────────────────────────────────────────────────────────────┐
│                     EXPORT SESSION (in memory)                    │
│                                                                   │
│  IronCAD Scene Tree                   Normalization Plan         │
│  ┌──────────────────────┐            ┌────────────────────────┐  │
│  │ IZSceneDoc (Root)    │            │ PdmPlanItem (Root)     │  │
│  │  ├─ IZElement#A(asm) │◄─SourceNode├─ child 0/0, file: X   │  │
│  │  │  ├─ IZEl#B(prt)   │◄─SourceNode├─ child 0/0/0, file: Y │  │
│  │  │  └─ IZEl#C(prt)   │◄─SourceNode├─ child 0/0/1, file: Z │  │
│  │  ├─ IZElement#D(asm) │◄─SourceNode├─ child 0/1, file: W   │  │
│  │  └─ IZElement#C(prt) │◄─SourceNode├─ child 0/2, file: Z   │  │
│  └──────────────────────┘   (shared)  └────────────────────────┘  │
│                                              │                    │
│      Dedup by IZElement ref:                  │                    │
│      C→Z appears in 2 occurrences             │                    │
└──────────────────────────────────────────────┼────────────────────┘
                                               │ Export()
                                               ▼
┌──────────────────────────────────────────────────────────────────┐
│                   EXPORTED PACKAGE (on disk)                      │
│                                                                   │
│  cad/                                                             │
│  ├─ MYASM__ROOT__Main.ics  (root, external-linked)               │
│  ├─ MYASM__P01__Y.ics      (definition for IZElement#B, 1 occur)│
│  ├─ MYASM__P02__Z.ics      (definition for IZElement#C, 2 occur)│
│  └─ MYASM__P03__W.ics      (definition for IZElement#D, 1 occur)│
│                                                                   │
│  pdm-bom-manifest.json                                            │
│  ├─ files: [root.ics, Y.ics, Z.ics, W.ics]                       │
│  └─ occurrences: [root, 0/0, 0/0/0, 0/0/1, 0/1, 0/2]           │
│       0/0/1 and 0/2 both have definitionFile="MYASM__P02__Z.ics" │
└──────────────────────────────────────────────────────────────────┘
```

## Export-Local Mapping: PdmSourceNode → DefinitionFile

This mapping exists **only during the export session** and is never persisted to disk. It drives which `.ics` file each plan item contributes to.

### Architecture Boundary

The mapping is computed in the **IronCAD layer** where IZElement (COM interop) types are available, then passed to **Workspace** as a clean DTO — no COM/IZElement types cross the boundary.

**Snapshot model**: `IronCadSceneSnapshot` carries two dictionaries:
- **`Elements`** — `IReadOnlyDictionary<PdmSourceNode, IZElement>` — raw COM handles used by the writer for `Apply()` and `SaveAs()`.
- **`ElementIds`** — `IReadOnlyDictionary<PdmSourceNode, ElementId>` — opaque identity tokens for shared-definition grouping. The Reader derives one `ElementId` per unique `IZElement` (same IZElement → same id). This mapping is separate from `Elements` so the dedup builder never touches COM types.

**Dedup chain**:
1. The `IronCadDefinitionFileMapBuilder` receives `IReadOnlyDictionary<PdmSourceNode, ElementId>` (from `snapshot.ElementIds`)
2. Groups `PdmSourceNode` instances by shared `ElementId` value equality
3. Output: `IDictionary<PdmSourceNode, string>` — each source node → definition file name

### Mapping Logic (pseudocode — IronCAD layer)

```csharp
// Step 1: Build reverse map: ElementId → List<PdmSourceNode>
// snapshot.ElementIds is IReadOnlyDictionary<PdmSourceNode, ElementId>
var idToSourceNodes = new Dictionary<ElementId, List<PdmSourceNode>>();
foreach (var kvp in snapshot.ElementIds)
{
    if (!idToSourceNodes.TryGetValue(kvp.Value, out var list))
        idToSourceNodes[kvp.Value] = list = new List<PdmSourceNode>();
    list.Add(kvp.Key);
}

// Step 2: Assign definition file name per unique ElementId
// Result: PdmSourceNode → definitionFile (Workspace-safe DTO)
var defFileMap = new Dictionary<PdmSourceNode, string>();
foreach (var (elementId, sourceNodes) in idToSourceNodes)
{
    var representative = sourceNodes[0]; // any PdmSourceNode for this ElementId
    var fileName = PdmNameNormalizer.CreateCanonicalFileName(
        representative.Name /* or ItemCode after preview */,
        representative.DisplayName,
        representative.Kind);
    foreach (var sourceNode in sourceNodes)
        defFileMap[sourceNode] = fileName;
}

// Step 3: Pass to Workspace (no ElementId or IZElement types cross the boundary)
manifestFactory.Create(plan, defFileMap);
```

### Mapping Contract

| Component | Receives | Produces/Consumes | COM types? |
|-----------|----------|------------------|------------|
| IronCAD reader | `IZSceneDoc` | `IronCadSceneSnapshot` (`Elements`: `PdmSourceNode → IZElement` + `ElementIds`: `PdmSourceNode → ElementId`) | ✅ Inside IronCAD layer (keeps IZElement for COM ops; derives ElementId for dedup) |
| IronCAD export writer | `IronCadSceneSnapshot`, `PdmNormalizationPlan` | **`IDictionary<PdmSourceNode, string>`** (source node → def file name) | ❌ Builder uses `ElementIds` only; writer uses `Elements` for COM |
| Workspace manifest factory | `PdmNormalizationPlan`, `IDictionary<PdmSourceNode, string>` | `PdmPackageManifest` with `definitionFile` set | ❌ No COM exposure |
| Round-trip verifier | Already-open exported root + `IronCadExternalReferenceValidationContext` | Validation result | ✅ Opened by document-service seam; verifier itself does not open documents |

### Relationship to Persistent Data

- **Plan items** persist in `pdm-bom-manifest.json` as occurrence entries (keyed by `NodeId`)
- **Definition files** persist in `cad/` directory
- The **mapping** (PdmSourceNode → filename) is ephemeral — it lives only in the export writer's memory
- Round-trip validation re-discovers the mapping by comparing `occurrencePath` + `canonicalFileName` — NOT by PdmSourceNode or IZElement reference

### Passing the Mapping to PdmManifestV2Factory

`PdmManifestV2Factory.Create()` currently receives `PdmNormalizationPlan` and iterates its items to build definitions and occurrences. The plan is **unchanged** — it still has one `PdmPlanItem` per occurrence with no notion of shared definition groups. Dedup is driven entirely by the `sourceNodeToDefFileMap` parameter — the factory never touches `ElementId` or `IronCadSceneSnapshot` types.

To supply the PdmSourceNode→definition-file mapping without modifying the plan:

```csharp
// New parameter on Create() — keyed by PdmSourceNode (Workspace type, no COM)
var manifest = PdmManifestV2Factory.Create(
    plan,
    sourceNodeToDefFileMap  // IDictionary<PdmSourceNode, string> from Export()
);
```

Inside `Create()`, for each plan item:
1. Look up `planItem.SourceNode` in the map — it returns the definition file path
2. Assign it to the occurrence entry's `DefinitionFile` property
3. Definitions are still deduplicated by `DefinitionId` (now driven by the map, not by occurrence path)

**This keeps `PdmNormalizationPlan` clean** — it remains a pure DTO for the plan/preview flow. The linked-export concern is injected at manifest-creation time by the export writer.

**If T3 fails**: Linked export is blocked with an error (see spec.md), so the mapping is never needed.

## State Transitions

Export flow (unchanged high-level, modified writer step):

```
Source Scene (open+modified) → Save → Scene (saved)
  → Reader.Read() → Snapshot (ElementId + tree structure)
  → Planner.CreatePlan() → Plan (items with SourceNode refs)
  → [Preview Dialog] → FinalPlan
  → Writer.Apply() → Scene (properties assigned)
  → Writer.Export() ──► Package (cad/ + manifest)
       │
       ├── Step 1: Build occurrence → canonical definition-file map
       ├── Step 2: Stage canonical root in package cad/
       ├── Step 3: Temporarily apply canonical definition-name stems
       ├── Step 4: Invoke native Save All As External for cad/
       ├── Step 5: Verify emitted definition files
       ├── Step 6: Restore approved names/properties and save all links
       └── Step 7: Write manifest with files[] + occurrences[]
                    (each occurrence records its definitionFile)
  → PackageValidator.Validate() → Validated package
  → ExportPackageVerifier.Verify() → Final validation
  → [Success/Failure message]
```
