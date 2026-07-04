# Part Library Stage 1 Deployment Guide

## Scope

Part Library Phase 1 provides the core governance and reuse workflow for existing Aras Parts:

- save an existing Aras Part to a Library;
- store Library Entry metadata;
- resolve a Part using the `Pinned` revision policy;
- resolve a Part using the `LatestReleased` revision policy;
- resolve a Part using the `LatestCurrent` revision policy;
- reuse the resolved Part in another PDM Project;
- maintain Entry lifecycle state;
- record usage;
- support idempotent retry behavior;
- calculate Where Used data;
- surface diagnostic Entries when resolution fails.

Phase 2 UI features are not included in this deployment guide.

## Required ItemTypes

- `idea_PartLibrary`
- `idea_PartLibraryEntry`
- `idea_PartLibraryUsage`

## Library Properties

`idea_PartLibrary` should expose:

- `name`
- `description`
- `library_type`
- `status`
- `default_revision_policy`
- `is_public`

Recommended values:

- `library_type`: `Personal`, `Team`, `Standard`
- `status`: `Active`, `Archived`
- `default_revision_policy`: `Pinned`, `LatestReleased`, `LatestCurrent`

## Relationship Configuration

- Source ItemType: `idea_PartLibrary`
- Related ItemType: `Part`
- Relationship ItemType: `idea_PartLibraryEntry`

## Entry Properties

`idea_PartLibraryEntry` should expose:

- `part_config_id`
- `revision_policy`
- `pinned_part_id`
- `pinned_revision`
- `entry_status`
- `category`
- `tags`
- `note`
- `source_project`
- `source_commit`
- `usage_count`
- `last_used_on`

Revision policy values:

- `Pinned`
- `LatestReleased`
- `LatestCurrent`

Entry status values:

- `Draft`
- `PendingReview`
- `Published`
- `Deprecated`

`usage_count` is only a cache. `idea_PartLibraryUsage` is the authoritative usage source.

## Usage Properties

`idea_PartLibraryUsage` should expose:

- `library_entry_id`
- `part_id`
- `project_code`
- `parent_part_id`
- `quantity`
- `used_by`
- `commit_id`
- `action_type`
- `idempotency_key`

`idempotency_key` configuration:

- Data Type: `String`
- Length: `64` or greater
- Required: `No`
- SHA-256 values produced by the desktop are 64 hexadecimal characters

Supported `action_type` values:

- `ReusedFromLibrary`
- `AddedToProject`
- `UpdatedInProject`

## Entry Lifecycle

Documented states:

- `Draft`
- `PendingReview`
- `Published`
- `Deprecated`

Recommended transitions:

- `Draft` -> `PendingReview`
- `Draft` -> `Published`
- `PendingReview` -> `Published`
- `Published` -> `Deprecated`

Other transitions are business decisions and should be documented explicitly if allowed.

## Server Methods

### `idea_AddPartToLibrary`

- Called by the desktop connector.
- May use compatibility fallback behavior when the live server method is not available.
- Should remain deployed for centralized validation.

### `idea_RecordPartLibraryUsage`

- Called directly through `ApplyMethod`.
- Validates the Entry, Part, quantity, parent Part, and action type.
- Queries by `idempotency_key`.
- Returns `already_exists`.
- Requires the `idempotency_key` property.

### `idea_SyncPartLibraryEntryStatus`

- Synchronizes `entry_status` with the Entry lifecycle state.

## Server Event

- ItemType: `idea_PartLibraryEntry`
- Event: `OnAfterPromote`
- Method: `idea_SyncPartLibraryEntryStatus`

Do not attach `idea_RecordPartLibraryUsage` as an ItemType event.

## Permissions

Minimum permissions for the connector identity:

- `Part`: `Get`
- `idea_PartLibrary`: `Get`
- `idea_PartLibraryEntry`: `Get`, `Add` where applicable, `Update/Edit`, `Delete` where applicable, `Promote`
- `idea_PartLibraryUsage`: `Get`, `Add`
- Methods: execute `idea_AddPartToLibrary`, `idea_RecordPartLibraryUsage`, and `idea_SyncPartLibraryEntryStatus` where required

Lifecycle transition roles and State Permissions must also allow the operation.

## Manual Method Compilation

ServerMethods files are excluded from normal desktop compilation.

To deploy manually:

1. Open Aras Method Editor.
2. Paste `idea_RecordPartLibraryUsage`.
3. Save.
4. Confirm compilation.
5. Paste `idea_SyncPartLibraryEntryStatus`.
6. Save.
7. Confirm compilation.

Do not claim successful live compilation unless it was actually performed.

## Idempotency Limitation

- Query-before-add handles normal retries.
- It does not fully prevent two truly concurrent requests from inserting at the same instant.
- A unique constraint or index on `idempotency_key` is recommended when supported and approved for the live Aras database.
- Do not claim atomic concurrency safety without that constraint.

## Live Acceptance Checklist

1. Save a Part to Library using `Pinned`.
2. Verify the Entry exists.
3. Resolve `LatestReleased`.
4. Resolve `LatestCurrent`.
5. Verify failed resolution does not mutate the Entry.
6. Add a Library Part to another PDM Project.
7. Push the Project.
8. Verify no duplicate Part.
9. Verify no duplicate CAD.
10. Verify no duplicate BOM relationship.
11. Record usage.
12. Submit the same `idempotency_key` again.
13. Verify one Usage Item exists.
14. Verify the second response returns `already_exists = 1`.
15. Verify the displayed usage count equals the actual Usage Items.
16. Publish the Entry.
17. Verify the state and `entry_status` are `Published`.
18. Deprecate the Entry.
19. Verify a Deprecated Entry cannot be reused.

## Phase Status Roadmap

### Phase 1

Core Library Governance and Reuse

Includes:

- save Part;
- resolve policy;
- reuse Part;
- lifecycle;
- usage;
- Where Used;
- diagnostics.

### Phase 2

Complete Library User Experience

Planned items:

- Create/Edit/Archive Library;
- Aras Part picker;
- Move Entry target dialog;
- revision browser;
- LatestCurrent UI action;
- Vault CAD download;
- Open in IronCAD from Vault;
- Open in Aras;
- CAD/BOM/Revisions tab content;
- Entry Status filter.

### Phase 3

Production Deployment and Hardening

Planned items:

- Aras import package;
- CI;
- concurrency uniqueness;
- large-data usage-count performance;
- security and governance;
- deployment evidence.

Phase 2 and Phase 3 are not implemented in this commit.
