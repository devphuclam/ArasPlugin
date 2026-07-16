# Spec Kit Workflow Migration Completion

## Completion status

```text
MIGRATION PARTIALLY COMPLETE - BLOCKED
```

The repository migration work is complete through canonical routing, taxonomy, pilot traceability, issue projection policy, and legacy retirement. The Definition of Done is not marked 100% complete because GitHub CLI installation/authentication could not be completed in this environment.

## Completed areas

- Spec Kit foundation and OpenCode integration.
- Canonical constitution, `AGENTS.md`, and `CONTEXT.md`.
- OpenCode routing through canonical instructions and Spec Kit commands.
- Canonical architecture, domain, development, security, and ADR taxonomy.
- Pilot feature migration at `specs/001-pdm-cad-launch-action/`.
- Pilot consistency analysis: PASS; no CRITICAL/HIGH findings; all requirements covered and all historical tasks marked completed.
- GitHub Issues projection policy documented; no issue created because the pilot has no eligible open task.
- Legacy AI workflow artifacts archived under `docs/archive/legacy-ai-work-kit/` with `MIGRATION_INDEX.md`.
- Legacy ticket commands/scripts retained only as compatibility/deprecation adapters.

## Canonical workflow

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

## Canonical sources

```text
.specify/memory/constitution.md
AGENTS.md
CONTEXT.md
specs/<feature>/
docs/architecture/
docs/domain/
docs/development/
docs/security/
docs/adr/
```

## Legacy status

The former `docs/ai/`, `docs/plans/`, `docs/superpowers/`, `.superpowers/`, and `tasks/ai/` trees were moved with relative structure to `docs/archive/legacy-ai-work-kit/`. Completed, partial, blocked, and not-started legacy tickets are classified in `MIGRATION_INDEX.md`; none were silently reopened or converted into active work.

## Verification evidence

- Branch: `chore/spec-kit-workflow-migration`.
- Spec Kit: `0.12.16`.
- Integration: OpenCode `v1.0.0`, installed/default.
- Pilot: `specs/001-pdm-cad-launch-action/`.
- Build: `dotnet build IdeaCadConnector.sln` — 0 warnings, 0 errors.
- Tests: `dotnet test IdeaCadConnector.sln` — 645 passed, 0 failed, 0 skipped.
- Git status: clean after the completion-report commit.
- No product source/test behavior changes were made by migration.

## Blocker

GitHub CLI/auth readiness remains blocked. Command `winget install --id GitHub.cli --exact --accept-source-agreements --accept-package-agreements` found GitHub CLI `2.96.0` and verified the MSI hash, but the installer returned exit code `1602` after cancellation. Subsequent `gh --version` returned `GH_MISSING_AFTER_INSTALL_ATTEMPT`. Install `gh` and complete interactive `gh auth login` before projecting future open Spec Kit tasks. No issue was created for the completed pilot.
