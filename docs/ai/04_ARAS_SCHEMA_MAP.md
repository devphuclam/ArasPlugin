# 04 - Aras Schema Map

This file must be verified against the actual Aras test/live database. Code evidence is not sufficient for destructive or schema-changing work.

Status values:

- `CONFIRMED-CODE`: exact logical name is used by current source.
- `CONFIRMED-LIVE`: manually verified on Aras.
- `CONFIRMED-LIVE-ABSENT`: manually verified on Aras as not deployed/found.
- `TBD-LIVE-VERIFY`: blocker for dependent tickets.

## Core ItemTypes and relationships

BASE-04 verification note, 2026-07-10:

- Live Aras read-only AML evidence collected from `http://172.16.10.227/InnovatorServer/`, database `InnovatorSolutions`.
- Evidence files are under `.ai-work/verification/BASE-04-live-schema/`.
- Token was used only from `.ai-work/live-token.local.txt` and is not recorded in evidence.
- Rows are promoted to `CONFIRMED-LIVE` only where read-only AML evidence confirms the logical name or deployment status.

| Logical name | Kind | Status | Notes |
|---|---|---|---|
| `Part` | ItemType | CONFIRMED-LIVE | Existing Aras standard item; live item count 1 |
| `CAD` | ItemType | CONFIRMED-LIVE | Native file property used by CAD flow; live item count 1 |
| `Document` | ItemType | CONFIRMED-LIVE | Current push creates/reuses metadata; live item count 1 |
| `Project` | ItemType | CONFIRMED-LIVE | Repository/project record; live item count 1 |
| `File` | ItemType | CONFIRMED-LIVE | Vault physical file; live item count 1 |
| `Part BOM` | Relationship | CONFIRMED-LIVE | Parent to child Part; live item count 1 |
| `Part CAD` | Relationship | CONFIRMED-LIVE | Part to CAD; live item count 1 |
| `Part Document` | Relationship | CONFIRMED-LIVE | Part to Document; live item count 1 |
| `Project Document` | Relationship | CONFIRMED-LIVE | Project to Document; live item count 1 |

## Physical Document file attachment - critical blocker

| Question | Status | Verified value |
|---|---|---|
| Relationship or property linking Document to File | CONFIRMED-LIVE | `Document File` relationship ItemType exists. `source_id` data source resolves to `Document`; `related_id` data source resolves to `File`. |
| Supports multiple File versions | TBD-LIVE-VERIFY | Relationship has standard generation/current properties, but functional multi-file/version behavior was not tested. |
| Required File classification/property | TBD-LIVE-VERIFY | `File` ItemType exists; required File classification/properties for Document attachment still need verification. |
| Document lock/version policy | TBD-LIVE-VERIFY | Needs read/write UAT or exported lifecycle/version policy evidence. |
| Permission to add/version Document and File link | TBD-LIVE-VERIFY | Needs permission evidence for each target role/user. |

Tickets `DOC-03` onward must not implement write behavior until the remaining Document File policy and permission rows are confirmed.

## PDM schema

| Logical name | Status | Known properties from current code/docs |
|---|---|---|
| `PDM Commit` | CONFIRMED-LIVE-ABSENT | Live Aras returned no `ItemType` named `PDM Commit`. Code attempts best-effort add with `commit_code`, `repository_code`, `branch_name`, `message`, `package_source_path`, `cad_source_path`; author/parent need completion. |
| `PDM Commit File` | CONFIRMED-LIVE-ABSENT | Live Aras returned no `ItemType` named `PDM Commit File`. Code attempts `commit_id`, `relative_path`, `file_role`; `change_type` and `vault_file_id` are planned, not confirmed. |
| `PDM Branch` | CONFIRMED-LIVE-ABSENT | Live Aras returned no `ItemType` named `PDM Branch`. Planned only; not deployed on current live server. |

## Part Library

| Logical name | Status | Notes |
|---|---|---|
| `idea_PartLibrary` | CONFIRMED-LIVE | Library metadata; live item count 1 |
| `idea_PartLibraryEntry` | CONFIRMED-LIVE | Relationship to existing Part; live item count 1 |
| `idea_PartLibraryUsage` | CONFIRMED-LIVE | Authoritative usage history; live item count 1 |

Live-confirmed properties include:

- `idea_PartLibrary`: `name`, `status`, `library_type`, `default_revision_policy`, `is_public`, `description`.
- `idea_PartLibraryEntry`: `source_id`, `related_id`, `entry_status`, `revision_policy`, `pinned_part_id`, `part_config_id`, `usage_count`, `source_project`, `source_commit`, `tags`, `category`, `note`.
- `idea_PartLibraryUsage`: `library_entry_id`, `part_id`, `parent_part_id`, `project_code`, `commit_id`, `quantity`, `action_type`, `used_by`, `idempotency_key`.

Remaining live checks before schema-changing work:

- Confirm add/edit/get permissions for each UAT role.
- Confirm lifecycle states and server event/method dependencies.

## Server methods found in source

- `idea_EnsurePrimaryIronCadPartCad`
- `idea_CommitCadCheckin`
- `idea_ReviseCad`
- `idea_StartDetailedDesign`
- `idea_AddPartToLibrary`
- `idea_RecordPartLibraryUsage`
- `idea_GetPrimaryIronCadForPart`
- `idea_SyncPartLibraryEntryStatus`

Deployment status on live server:

| Method | Live | Last verified |
|---|---|---|
| `idea_EnsurePrimaryIronCadPartCad` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_CommitCadCheckin` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_ReviseCad` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_StartDetailedDesign` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_AddPartToLibrary` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_RecordPartLibraryUsage` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_GetPrimaryIronCadForPart` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |
| `idea_SyncPartLibraryEntryStatus` | CONFIRMED-LIVE | 2026-07-10 read-only AML get |

## Lifecycle / permission evidence

Read-only probes found these live lifecycle map names:

- `CAD`
- `Custom CAD Document`
- `Custom Part`
- `Document`
- `idea_PartLibraryEntry Lifecycle`
- `Part`

Read-only ItemType probes found the same permission id on `CAD`, `Part`, and `Document`: `102D29B8CD9948BFB5F558341DF4C0F9`.

Remaining blocker:

- Permission matrix was not decoded yet.
- Role-specific add/edit/version permission checks were not executed.
- Lifecycle transition paths were not decoded yet.

## BASE-04 current conclusion

Confirmed live:

- Core ItemTypes and relationships currently used by clone/push.
- `Document File` relationship exists for physical Document attachments.
- Part Library ItemTypes and key properties exist.
- All eight listed server methods exist on live.

Confirmed absent on live:

- `PDM Commit`
- `PDM Commit File`
- `PDM Branch`

Still blocked:

- Document File write/version/permission behavior.
- Role-specific permission matrix.
- Lifecycle transition map details.
- Any ticket that requires server-side PDM Branch/Commit schema.
