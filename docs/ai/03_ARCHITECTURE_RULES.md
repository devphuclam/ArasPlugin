# 03 — Architecture Rules

## Layer ownership

| Module | Owns | Must not own |
|---|---|---|
| Core | Contracts, DTOs, policies, validation | WPF, filesystem implementation, HTTP |
| Aras | AML/HTTP/OAuth/Vault/server integration | WPF UI, local workspace persistence |
| Workspace | Local manifest, scan, diff, branch registry, atomic file operations | Aras HTTP, WPF dialogs |
| Desktop | Application orchestration, ViewModels, navigation | Raw AML strings, reusable diff algorithms |
| Ui | Shared WPF views/view models | Aras/business persistence |
| IronCAD | IronCAD adapter/add-in | Aras repository orchestration |
| Tests | Unit/integration verification | Production-only shortcuts |

## Non-negotiable boundaries

- WPF dependencies stay out of Core and Workspace.
- Raw Aras calls stay in Aras project or server methods.
- `.idea-pdm` persistence stays in Workspace.
- ViewModels orchestrate services; they do not implement SHA256/diff/Vault protocols.
- A Library Entry references an existing Part and must not duplicate Part/CAD/File.
- Non-main branch must not silently update live main data.
- Binary CAD/PDF/DWG files are never auto-merged.

## Transaction rules

- Upload: do not mark item/commit successful before required physical file operations succeed.
- Pull: download to temp, validate, backup, then apply.
- Manifest: write temp + atomic replace only after complete success.
- Branch head: update only after commit creation succeeds.
- Failure: return typed/structured error and preserve recoverable state.

## Compatibility

Any change to manifest/schema/public contract requires:

- version field;
- migration or compatibility reader;
- tests for old and new shapes;
- rollback notes.
