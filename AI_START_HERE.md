# IdeaCadConnector AI Entry Point

This file is a compatibility entry point. The canonical workflow for new feature behavior is GitHub Spec Kit.

## Start here

From the `ArasPlugin/` repository root, read:

1. `AGENTS.md`
2. `.specify/memory/constitution.md`
3. `CONTEXT.md`

For a feature, use the canonical sequence:

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

Feature artifacts belong under `specs/<feature>/`. Bugs, hotfixes, and chores use the approved issue tracker.

## Legacy references

`docs/ai/`, `docs/plans/`, `docs/superpowers/`, `tasks/ai/`, and `.superpowers/` are transitional or historical paths. Read them only for traceability or knowledge not yet migrated. Do not create new feature workflow or tickets there.

DeepSeek and OpenCode compatibility details remain in `DEEPSEEK.md` and `OPENCODE_START_HERE.md`; neither replaces the canonical instructions above.
