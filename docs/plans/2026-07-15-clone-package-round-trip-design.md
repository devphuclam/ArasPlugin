# Clone Package Round-Trip Design

## Goal

Change the existing Clone operation so that the workspace downloaded from Aras has the same package layout produced by Normalize & Export and consumed by Push.

The existing Aras retrieval behavior remains in place: Clone resolves the repository root Part, traverses its BOM, resolves linked CAD items, and downloads their native `.ics` files. This change does not introduce package archives, commit snapshots, or a new server-side storage model.

## Required output

For repository `PDM-STUDYCASE`, Clone must produce:

```text
PDM-STUDYCASE/
|-- .idea-pdm/
|   `-- branches.json
|-- cad/
|   |-- <root scene>.ics
|   `-- <all other native CAD files>.ics
`-- pdm-bom-manifest.json
```

The package folder name is the normalized project/repository code. Native files keep the names stored by Push. The package must not contain legacy Clone artifacts such as `ARAS01`, generated empty PDF/DWG files, or `*-STRUCTURE.txt`.

## Data flow

1. Validate the Clone request and resolve the selected repository and branch.
2. Resolve the root Part and traverse live `Part BOM` relationships from Aras.
3. Resolve the preferred linked CAD for every included Part and its `native_file`.
4. Create the package root and `cad` directory.
5. Download each native file into `cad` using the native filename stored in Aras. Reject unsafe, missing, or duplicate destination names instead of silently renaming files.
6. Build a schema-version-2 PDM manifest from the resolved Parts, BOM relationships, CAD metadata, and filenames.
7. Identify the root definition from the root Part/CAD mapping and set `RootFile` to its package-relative path under `cad`.
8. Write `pdm-bom-manifest.json` with the existing manifest writer.
9. Create `.idea-pdm/branches.json` through `WorkspaceService`; it contains the cloned branch and always supports `main` as the default branch.
10. Validate the completed package with `PdmPackageValidator` before returning success.

## BOM and manifest mapping

- A CAD-backed Part maps to one manifest definition.
- A BOM relationship maps to an occurrence edge with its server quantity.
- Root Part maps to the root occurrence and root definition.
- Definition filenames are package-relative `cad/<native filename>` paths, matching Normalize & Export output.
- Occurrence identifiers and paths are deterministic for the same live BOM, so repeated Clone operations produce stable manifests apart from workspace branch timestamps.
- Multiple occurrences of the same definition remain separate occurrences; definitions are not duplicated.
- Child ordering uses BOM find number when available and a deterministic fallback when it is not.

## Filesystem behavior

- Clone writes to a temporary directory outside the visible destination and publishes the package only after validation succeeds.
- Temporary directory names must not appear as `.pending-*` folders in the selected destination.
- Existing destination content is never deleted by the repository client without an explicit overwrite decision from the UI.
- A failed download or validation removes only the temporary Clone directory and leaves an existing destination untouched.

## Error behavior

Clone fails with a clear error when:

- the root Part or root CAD cannot be resolved;
- a required CAD has no native file;
- a native filename is unsafe or collides with another downloaded file;
- a native download fails;
- the root `.ics` cannot be identified;
- manifest generation or validation fails;
- the destination exists and overwrite was not explicitly authorized.

Clone must not report success by substituting empty placeholder files.

## UI result

On success, `ResolvedProjectFolder` points to the package root, `ResolvedCadFolder` points to its `cad` directory, and the result exposes the root `.ics` path so the desktop app can open the correct scene in IronCAD.

## Compatibility

- No change to how Push stores Part, BOM, CAD, and CAD native files in Aras.
- Clone remains a live-data operation for the selected repository.
- Legacy package-shape generation is removed from successful Clone output.
- Non-main branch behavior remains subject to the current live-data limitation and must retain its existing warning.

## Verification

Automated tests must cover:

- exact top-level layout: `.idea-pdm`, `cad`, and `pdm-bom-manifest.json`;
- native filenames preserved under `cad`;
- root file path and root occurrence correctness;
- BOM quantities, ordering, and repeated occurrences;
- `branches.json` creation for `main` and a selected branch;
- absence of `ARAS01`, PDF/DWG placeholders, and `STRUCTURE.txt`;
- missing native file, unsafe filename, collision, failed download, invalid manifest, and existing destination handling;
- cleanup and no partial destination after failure;
- successful import of the cloned package through `PdmPackageImportReader`;
- existing Push and Aras repository tests remain green.
