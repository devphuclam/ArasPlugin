# ChatGPT/Codex and OpenCode Workflow

This repository uses Spec Kit as the canonical feature workflow. ChatGPT/Codex
is the analyst and independent reviewer; OpenCode is the Spec Kit host and
default implementer.

## Feature workflow

1. ChatGPT/Codex clarifies the request with `grill-with-docs`.
2. OpenCode runs `/speckit.specify` and `/speckit.clarify`.
3. ChatGPT/Codex reviews `spec.md`.
4. OpenCode runs `/speckit.plan` and `/speckit.tasks`.
5. ChatGPT/Codex reviews the plan and tasks with `codebase-design`.
6. OpenCode runs `/speckit.analyze`, then `/speckit.implement` with `tdd`.
7. ChatGPT/Codex performs an independent `code-review`.
8. OpenCode fixes approved findings, builds, tests, and commits.

Only one AI may write source on a branch at a time. The reviewer waits until the
writer has completed its diff and does not edit source during review.

## Bug workflow

ChatGPT/Codex uses `diagnosing-bugs` to reproduce the issue and identify its
root cause. OpenCode implements the approved regression test and fix with `tdd`,
then runs the focused and repository verification. ChatGPT/Codex performs the
independent `code-review`.

## Daily rules

- Read `AGENTS.md`, the constitution, `CONTEXT.md`, and relevant canonical artifacts first.
- Features belong under `specs/<feature>/`; do not create competing plans or task lists.
- Do not guess Aras schema, lifecycle, permission, or AML behavior.
- Do not run `specify init` or OpenCode `/init` in this existing repository.
- Do not change the constitution without an approved governance change.
- Do not use `/speckit.taskstoissues` unless GitHub CLI is explicitly available and the projection is approved.
- GitHub CLI is not a prerequisite for feature implementation.
- Keep secrets and credentials out of files, prompts, logs, and commits.

## Handoff

Pass the branch, HEAD, working-tree status, active feature, canonical artifact
paths, completed and next task IDs, changed files, commands and results, open
findings, blockers, approved decisions, and prohibited actions.
