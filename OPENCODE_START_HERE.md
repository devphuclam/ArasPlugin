# OpenCode compatibility entry point

OpenCode is configured to load the canonical instructions in this order:

1. `AGENTS.md`
2. `.specify/memory/constitution.md`
3. `CONTEXT.md`

Detailed canonical references are loaded only when relevant from `docs/domain/`, `docs/architecture/`, `docs/development/`, `docs/security/`, and `docs/adr/`.

## Canonical workflow

From the `ArasPlugin/` repository root, use:

```text
/speckit.specify
→ /speckit.clarify
→ /speckit.plan
→ /speckit.tasks
→ /speckit.analyze
→ /speckit.implement
→ review
→ verify
```

Feature artifacts belong under `specs/<feature>/`. Bugs, hotfixes, and chores use the approved issue tracker. The `ticket-*` commands remain compatibility adapters and must not create a competing feature workflow.

## Legacy references

`docs/archive/legacy-ai-work-kit/` is the historical archive. Do not create new feature workflow there.
