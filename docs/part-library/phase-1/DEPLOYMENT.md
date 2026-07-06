# Part Library Phase 1 Deployment

## Required ItemTypes

- `idea_PartLibrary`
- `idea_PartLibraryEntry`
- `idea_PartLibraryUsage`

`idea_PartLibraryEntry` is a relationship from `idea_PartLibrary` to `Part`.

## Library Properties

| Property | Purpose |
|---|---|
| `name` | Display name |
| `description` | Description |
| `library_type` | `Personal`, `Team`, or `Standard` |
| `status` | `Active` or `Archived` |
| `default_revision_policy` | Default policy |
| `is_public` | Visibility flag |

## Entry Properties

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

Policy values: `Pinned`, `LatestReleased`, `LatestCurrent`.

Entry states/statuses: `Draft`, `PendingReview`, `Published`, `Deprecated`.

## Usage Properties

- `library_entry_id`
- `part_id`
- `project_code`
- `parent_part_id`
- `quantity`
- `used_by`
- `commit_id`
- `action_type`
- `idempotency_key`

`idea_PartLibraryUsage` is authoritative. Entry `usage_count` is a cache.

`idempotency_key` must support at least 64 characters. Supported actions are `ReusedFromLibrary`, `AddedToProject`, and `UpdatedInProject`.

## Server Methods

| Method | Responsibility |
|---|---|
| `idea_AddPartToLibrary` | Central validation and idempotent Entry creation |
| `idea_RecordPartLibraryUsage` | Validate and record usage with retry protection |
| `idea_SyncPartLibraryEntryStatus` | Synchronize lifecycle state to `entry_status` |

Server Method source is under `src/IdeaCadConnector.Aras/ServerMethods/`. These files are deployment artifacts and are not compiled into the desktop application.

`idea_RecordPartLibraryUsage` returns `already_exists = 1` when the submitted `idempotency_key` was recorded previously.

## Lifecycle Event

- ItemType: `idea_PartLibraryEntry`
- Event: `OnAfterPromote`
- Method: `idea_SyncPartLibraryEntryStatus`

Recommended transitions:

- `Draft` -> `PendingReview`
- `Draft` -> `Published`
- `PendingReview` -> `Published`
- `Published` -> `Deprecated`

Do not attach `idea_RecordPartLibraryUsage` as an ItemType event.

## Permissions

The connector identity needs:

- `Part`: Get;
- `idea_PartLibrary`: Get;
- `idea_PartLibraryEntry`: Get, Add, Edit, Delete relationship where allowed, Promote;
- `idea_PartLibraryUsage`: Get, Add;
- execution permission for the three server Methods.

Lifecycle transition roles and State Permissions must independently permit the requested action.

## Manual Deployment

1. Create or verify the three ItemTypes and exact property names.
2. Configure the Entry relationship to existing `Part`.
3. Configure lifecycle states and transition roles.
4. Paste each Method source into the Aras Method Editor.
5. Save and confirm compilation.
6. Attach only the documented lifecycle event.
7. Verify permissions with the actual connector identity.
8. Execute the live checks in [ACCEPTANCE.md](ACCEPTANCE.md).

Do not claim successful live compilation or deployment unless it was actually performed.

## Live Acceptance

Complete the environment checks recorded in [ACCEPTANCE.md](ACCEPTANCE.md) after deployment.

## Concurrency Limitation

Query-before-add protects normal retries. It does not guarantee uniqueness for two truly concurrent inserts. A database-supported unique constraint/index on `idempotency_key` is recommended after approval.
