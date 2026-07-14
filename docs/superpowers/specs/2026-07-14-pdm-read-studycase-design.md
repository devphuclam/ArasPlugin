# IDEA PDM Manifest BOM Migration Design

## Goal

Move IDEA PDM Desktop from the legacy filename/folder naming policy to the normalized package policy by reading `pdm-bom-manifest.json` and rendering its BOM in the existing PDM project tree.

## Scope

- IDEA PDM Desktop is the consumer.
- Normalize Export remains the producer and is not changed.
- Read `pdm-bom-manifest.json` schema version 2 and its referenced CAD files.
- Reuse the existing `PdmProjectsView` and `PdmStructure` tree.
- Preserve legacy `Aras01FolderAnalyzer` behavior as fallback when no new manifest exists.
- Do not parse binary `.ics`, write CAD files, rename files, or push to Aras.

## Architecture

### Manifest reader

`PdmPackageImportReader` lives in Workspace. It finds the newest manifest below the selected folder, deserializes it, runs the existing `PdmPackageValidator`, and maps definitions/occurrences/BOM edges into the legacy-neutral `PdmFolderAnalysis` and `PdmBusinessStructureAnalysis` models already consumed by IDEA PDM Desktop.

The mapper keeps all readable definitions and occurrences even when validation reports issues. Validation issues become blocking `PdmNamingIssue` entries so the BOM remains visible but unsafe downstream actions stay blocked.

### Desktop integration

`PdmProjectsViewModel.AnalyzeFolder()` first asks the manifest reader for a normalized package. When found, it uses manifest-derived analysis and sets policy version `pdm-manifest-v2`. When no manifest exists, it executes the unchanged legacy analyzer/parser path.

The existing `BuildPdmStructure`, `BuildCadStructure`, document list, summary, and UI bindings display the imported BOM. No separate viewer or IronCAD ribbon command is added.

## Mapping

- Manifest `ProjectCode` -> project/repository code.
- Manifest `Revision` and definition `Revision` -> displayed revision.
- Definition `ItemCode` -> logical part code.
- Definition `DisplayName` -> node name.
- Definition `ItemType` (`ASM`/`PRT`) -> Assembly/Component.
- Definition `FileName` -> primary CAD/source document.
- Occurrence parent relationships -> BOM tree.
- Occurrence multiplicity/BOM edges -> displayed quantities where available.

## Safety

- Manifest paths are validated by `PdmPackageValidator` before integration.
- Invalid or missing referenced files remain visible as issues and block push.
- The reader never opens or writes `.ics` files.
- Absolute private paths and CAD names are not committed as fixtures.

## Verification

- Unit tests cover manifest discovery, mapping, hierarchy, quantities, validation propagation, latest-package selection, and legacy fallback.
- Debug and Release solution builds/tests pass.
- Runtime selects the exported StudyCase package in IDEA PDM Desktop and confirms the existing BOM tree shows root, assemblies, and parts from the manifest.
