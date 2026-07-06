# Part Library Phase 1 Design

## Purpose

Part Library is a controlled collection of references to existing Aras `Part` items. It lets engineers discover and reuse approved/common Parts in PDM projects without copying product data.

## Authoritative Business Rules

1. A Library Entry references an existing Part; it does not copy the Part.
2. Aras owns Part, CAD, File, lifecycle, revision, lock, and permission state.
3. Reuse first creates a local workspace reference, then creates or updates `Part BOM` during a successful live push to `main`.
4. Reuse identity is Aras Part ID, config ID, and revision policy. Part Number is display/search data only.
5. Existing projects are never silently migrated to another revision.
6. Removing a Library Entry never deletes its Part, CAD, or File.
7. Library CAD is referenced; PDM reuse does not recreate or upload it.
8. Non-main branches preserve local/staging-only behavior.
9. Geometry insertion into an active IronCAD scene is outside Phase 1.

## Architecture

```text
Aras Part/CAD/File
       |
idea_PartLibraryEntry
       |
HttpPartLibraryClient
       |
LibraryView / LibraryViewModel
       |
.idea-pdm/library-references.json
       |
PDM Analyze -> Structure -> Preview -> Commit -> Push
       |
Part BOM create/update by exact Aras identity
```

Part Library is a bounded context. It uses `IPartLibraryClient` and the existing session/shared-client pattern instead of broad application-session refactoring.

## Aras Model

- `idea_PartLibrary`: Library metadata and visibility.
- `idea_PartLibraryEntry`: relationship from Library to existing Part plus revision policy and governance metadata.
- `idea_PartLibraryUsage`: authoritative usage history.
- `Part CAD`: existing Part-to-CAD ownership.
- `Part BOM`: project structure created or updated during push.

Supported revision policies are `Pinned`, `LatestReleased`, and `LatestCurrent`. A failed resolution remains visible as a diagnostic Entry but cannot be reused.

## Local Workspace Model

References are stored in `.idea-pdm/library-references.json` with a schema version. Each reference contains:

- Library and Entry IDs;
- exact Part ID and config ID;
- revision and revision policy;
- target parent logical code;
- quantity;
- local logical code;
- actor and timestamp.

The file is written atomically. Analyze merges valid references into the PDM tree. Missing parents become blocking issues rather than silently disappearing.

## Reuse and Push

- Validate workspace, parent, quantity, cycle risk, deprecation, and resolution before persistence.
- Deduplicate local placement by parent and Entry/Part identity.
- Use exact Part/config identity during preview and push.
- Never fall back to creating a new Part for a Library reference.
- Update an existing BOM relationship when quantity changes.
- Record usage after successful live push using an idempotency key.

## UI Rules

- Loading, empty, disconnected, permission, malformed, and resolution-failed states are distinct.
- Resolution-failed and deprecated Entries cannot execute Add to Current PDM Project.
- Disabled buttons must look disabled.
- Dialog read-only values use one-way bindings.
- Dialog cancellation and unexpected local failures must not terminate the application.

## Guardrails

- No duplicate Part creation from Library reuse.
- No Part/CAD/File deletion through Library removal.
- No silent revision drift.
- No swallowed authentication or permission failures.
- No raw token, authorization header, or SOAP payload in user-facing errors.
- No assumption that an Aras Method or ItemType exists without deployment evidence.

## Deferred to Later Phases

- full Library administration;
- Part picker and revision browser;
- Vault-backed CAD download/open;
- Open in Aras;
- completed CAD/BOM/Revisions tabs;
- active-scene geometry insertion;
- 3D preview, geometry comparison, AI similarity, and bulk migration.
