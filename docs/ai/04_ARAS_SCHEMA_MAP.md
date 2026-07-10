# 04 — Aras Schema Map

This file must be verified against the actual Aras test/live database. Code evidence is not sufficient for destructive or schema-changing work.

Status values:

- `CONFIRMED-CODE`: exact logical name is used by current source.
- `CONFIRMED-LIVE`: manually verified on Aras.
- `TBD-LIVE-VERIFY`: blocker for dependent tickets.

## Core ItemTypes and relationships

| Logical name | Kind | Status | Notes |
|---|---|---|---|
| `Part` | ItemType | CONFIRMED-CODE | Existing Aras standard item |
| `CAD` | ItemType | CONFIRMED-CODE | Native file property used by CAD flow |
| `Document` | ItemType | CONFIRMED-CODE | Current push creates/reuses metadata |
| `Project` | ItemType | CONFIRMED-CODE | Repository/project record |
| `File` | ItemType | CONFIRMED-CODE | Vault physical file |
| `Part BOM` | Relationship | CONFIRMED-CODE | Parent → child Part |
| `Part CAD` | Relationship | CONFIRMED-CODE | Part → CAD |
| `Part Document` | Relationship | CONFIRMED-CODE | Part → Document |
| `Project Document` | Relationship | CONFIRMED-CODE | Project → Document |

## Physical Document file attachment — critical blocker

| Question | Status | Verified value |
|---|---|---|
| Relationship or property linking Document to File | TBD-LIVE-VERIFY | Do not invent |
| Supports multiple File versions | TBD-LIVE-VERIFY | |
| Required File classification/property | TBD-LIVE-VERIFY | |
| Document lock/version policy | TBD-LIVE-VERIFY | |
| Permission to add/version Document and File link | TBD-LIVE-VERIFY | |

Tickets `DOC-03` onward must not implement schema-dependent behavior until these values are confirmed.

## PDM schema

| Logical name | Status | Known properties from current code/docs |
|---|---|---|
| `PDM Commit` | TBD-LIVE-VERIFY | `commit_code`, `repository_code`, `branch_name`, `message`, `package_source_path`, `cad_source_path`; author/parent need completion |
| `PDM Commit File` | TBD-LIVE-VERIFY | `commit_id`, `relative_path`, `file_role`, `change_type`, `vault_file_id` planned |
| `PDM Branch` | TBD-LIVE-VERIFY | Planned; not safe to assume deployed |

## Part Library

| Logical name | Status | Notes |
|---|---|---|
| `idea_PartLibrary` | CONFIRMED-CODE | Library metadata |
| `idea_PartLibraryEntry` | CONFIRMED-CODE | Relationship to existing Part |
| `idea_PartLibraryUsage` | CONFIRMED-CODE | Authoritative usage history |

Required manual update: mark each as `CONFIRMED-LIVE` only after querying the target environment.

## Server methods found in source

- `idea_EnsurePrimaryIronCadPartCad`
- `idea_CommitCadCheckin`
- `idea_ReviseCad`
- `idea_StartDetailedDesign`
- `idea_AddPartToLibrary`
- `idea_RecordPartLibraryUsage`
- `idea_GetPrimaryIronCadForPart`
- `idea_SyncPartLibraryEntryStatus`

For each environment, record deployment status below:

| Method | Dev | Test | Production | Last verified |
|---|---|---|---|---|
| idea_EnsurePrimaryIronCadPartCad | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_CommitCadCheckin | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_ReviseCad | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_StartDetailedDesign | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_AddPartToLibrary | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_RecordPartLibraryUsage | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_GetPrimaryIronCadForPart | UNKNOWN | UNKNOWN | UNKNOWN | |
| idea_SyncPartLibraryEntryStatus | UNKNOWN | UNKNOWN | UNKNOWN | |
