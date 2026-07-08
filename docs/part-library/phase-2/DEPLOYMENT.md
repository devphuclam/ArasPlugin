# Part Library Phase 2 Deployment

**State:** `IN_PROGRESS` (Sprint 2.3 App UAT accepted)

This document records deployment assumptions and live-system dependencies for Phase 2. It is not a claim that any Phase 2 change has been deployed.

## Baseline

- Planning baseline commit: `956af6841392b609d9c06df60d484fe5244500c1`
- Phase 1 deployed artifacts remain the active live baseline unless a later Phase 2 deployment is explicitly recorded.

## Phase 1 Artifacts That Must Remain Stable

- ItemTypes:
  - `idea_PartLibrary`
  - `idea_PartLibraryEntry`
  - `idea_PartLibraryUsage`
- Relationship:
  - `idea_PartLibraryEntry` from Library to existing `Part`
- Server Methods:
  - `idea_AddPartToLibrary`
  - `idea_RecordPartLibraryUsage`
  - `idea_SyncPartLibraryEntryStatus`
- Lifecycle event:
  - `idea_PartLibraryEntry` `OnAfterPromote` -> `idea_SyncPartLibraryEntryStatus`
- Server Methods (Sprint 2.3 addition):
  - `idea_GetPrimaryIronCadForPart` — read-only C# method deployed 2026-07-08; accepts `part_id` (string), returns CAD item with native file; resolves primary CAD for a given Part ID; no mutation.

Phase 2 must not rename or rebuild these artifacts just to match the package wording.

## Expected Phase 2 Deployment Surface

### Definitely application-side

- `Core` contracts for Library CRUD, Part picker, revision history, CAD details, BOM details, and navigation data
- `Desktop` dialogs, tabs, filters, status messaging, and localization
- `Aras` client query logic and permission/error classification

Current Sprint 2.3 UI wiring is application-side only; no live Aras ItemType, Method, or lifecycle change is required for the code that landed in this packet.

### Potentially Aras-side

The current package suggests operations that may remain client-driven or may need a server-owned entry point after implementation review:

- move Entry with rollback-safe semantics;
- possibly Library CRUD permission enforcement beyond raw AML;
- any operation that cannot be made safe with client-side add/verify/delete orchestration.

No new server Method is approved yet.

## Deployment Assumptions by Requirement Group

| Group | Planned live dependency |
|---|---|
| `LM` | existing Library schema plus verified `Add`/`Edit`/archive permissions |
| `PP` | readable `Part` search/select fields and any needed state/classification access |
| `ME` | delete/add/edit rights on `idea_PartLibraryEntry`, or an approved Method if client orchestration is unsafe |
| `RV` | readable `Part` revision/generation/state/current data |
| `VT` | readable `Part CAD`, `CAD`, native `File`, and Vault download permissions |
| `OA` | environment-correct Innovator base URL and database context |
| `TAB` | readable BOM, CAD, revision, and where-used relationships |
| `FLT` | no additional schema required if based on resolved query data |

## Decisions

All decisions D-01 through D-06 are APPROVED as of 2026-07-06. No unresolved decisions block the current packet.

| ID | Approved Rule |
|---|---|
| `D-01` | Only Trưởng phòng thiết kế cơ (Library Manager) may create Libraries |
| `D-02` | Unique on `Library ID + part_config_id`, case-insensitive. Active: Draft, PendingReview, Published. |
| `D-03` | Archived Libraries hidden by default; explicit filter to display; read-only; no new Entries; not selectable as Move or Part Picker target |
| `D-04` | Move preserves identity, config_id, policy, pinned info, lifecycle, status, metadata. Block on unsafe lifecycle preservation |
| `D-05` | Per-Windows-user Vault cache keyed by server/db/File ID/revision. Temp-first download; zero-byte reject; extension validate; atomic move; temp cleanup |
| `D-06` | Preferred: existing bridge/connector. Process launch fallback only with verified local file |

## Known Identities

| Account | Role | Password | Notes |
|---|---|---|---|
| `admin` | Full Access | `innovator` | Bypasses all role-based restrictions. For setup/emergency only. |

## Deployment Rules for Later Implementation

1. Do not claim a new Method exists live until it is compiled and tested in Aras.
2. Do not use `InnovatorAdmin` to hide permission defects during UAT.
3. Do not merge Vault-related claims without real download/open validation using a non-admin workflow identity.
4. Record any approved Phase 2 Aras artifact under the canonical deployment section only after local code and live configuration match.
5. If a new Method becomes necessary, store its source under `src/IdeaCadConnector.Aras/ServerMethods/` and document:
   - exact method name;
   - required permissions;
   - manual compile step;
   - live verification checklist;
   - rollback/removal path.

## Rollback Considerations

- application-only changes can roll back by reverting the deployment build;
- any new Aras Method or permission change must have an export or copyable source before save;
- Vault cache logic must be safe to disable without deleting user source files;
- no Phase 2 deployment may alter or invalidate Phase 1 Entry/Usage data.

## Current Planning Outcome

Phase 2 is `IN_PROGRESS`. Sprint 2.3 App UAT accepted. Server method `idea_GetPrimaryIronCadForPart` deployed to Aras for live CAD lookup. No additional Aras ItemType, permission, lifecycle, or Vault configuration change is approved by this document.
