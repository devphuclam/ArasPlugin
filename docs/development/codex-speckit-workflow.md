# Codex and OpenCode workflow

This repository supports the same Spec Kit workflow in both runtimes.

## Canonical ownership

- `.opencode/commands/` contains the canonical Spec Kit command bodies.
- `.agents/skills/speckit-*/SKILL.md` contains thin Codex adapters.
- `.agents/skills/{grill-with-docs,domain-modeling,codebase-design,tdd,diagnosing-bugs,code-review}/` contains the local Matt Pocock supporting skills.
- `specs/<feature>/` contains the canonical feature artifacts.

The adapter layer is intentionally thin. It prevents Spec Kit logic from
forking between runtimes and keeps the existing OpenCode commands unchanged.

## Invocation

In OpenCode, use the slash commands:

```text
/speckit.specify
/speckit.clarify
/speckit.plan
/speckit.tasks
/speckit.checklist
/speckit.analyze
/speckit.implement
/speckit.constitution
/speckit.converge
/speckit.taskstoissues
```

In Codex, invoke the matching skill by name:

```text
$speckit-specify
$speckit-clarify
$speckit-plan
$speckit-tasks
$speckit-checklist
$speckit-analyze
$speckit-implement
$speckit-constitution
$speckit-converge
$speckit-taskstoissues
```

Codex also accepts a natural-language request such as “run Spec Kit analyze
for Feature 004”. The Codex adapter reads the matching OpenCode command before
acting. Repository-local files cannot register arbitrary `/speckit.*` slash
syntax inside the Codex application; the `$speckit-*` skill invocation is the
portable Codex form.

## Writer and reviewer rule

Only one runtime edits source on a branch at a time. OpenCode remains the
default implementation writer for focused Spec Kit tasks. Codex may review
artifacts or implementation, and may implement only when explicitly assigned.
No runtime may create a competing `spec.md`, `plan.md`, or `tasks.md`.

## New-session setup

After pulling a branch containing new adapters, start a new Codex session (or
refresh the workspace skill catalog) so the ten `speckit-*` skills are indexed.
