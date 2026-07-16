# AI Workflow

## Canonical feature workflow

```text
/speckit.specify
→ /speckit.clarify
→ /speckit.plan
→ /speckit.tasks
→ /speckit.analyze
→ /speckit.implement
→ independent review
→ verification
```

Feature artifacts live under `specs/<feature>/`. Bugs, hotfixes, and chores use the approved issue tracker. `tasks/ai/`, `docs/ai/`, `docs/plans/`, `docs/superpowers/`, and `.superpowers/` are transitional or historical paths and do not receive new feature workflow.

## Context discipline

Start with `AGENTS.md`, the constitution, `CONTEXT.md`, the relevant feature artifacts, source, tests, and only the detailed references needed for the task. Never include secrets, credentials, binaries, or unrelated archived material.

Source references: `docs/ai/11_CONTEXT_PACK_RULES.md`, `AGENTS.md`, and `.specify/memory/constitution.md`.
