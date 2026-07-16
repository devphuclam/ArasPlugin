# ArasPlugin Agent Instructions

## Repository scope

- Run every command from the `ArasPlugin/` repository root.
- Do not use `.specify/` or `.agents/` from the parent workspace as canonical project instructions.
- Keep each change within the approved feature, issue, or chore scope.

## Canonical source order

1. Source and tests for current behavior.
2. `.specify/memory/constitution.md` for governing principles.
3. `specs/<feature>/spec.md`.
4. `specs/<feature>/plan.md`.
5. `specs/<feature>/tasks.md`.
6. `CONTEXT.md` and `docs/domain/`.
7. ADRs and detailed reference documentation.

## Work routing

- Feature behavior: use Spec Kit artifacts under `specs/<feature>/`.
- Bug, hotfix, or chore: use the approved issue tracker.
- Architecture decision: record an ADR under `docs/adr/`.
- Feature research: use the feature's `research.md`.
- Do not create new feature tickets in `docs/archive/legacy-ai-work-kit/tasks/ai/`.

## Safety constraints

- Never guess Aras schema or live product behavior.
- Do not modify source outside an approved task or issue.
- Do not use destructive Git commands.
- Do not edit the registry or run `.reg` files without explicit request.
- Do not install dependencies or tools outside approved scope.
- Never write secrets to files, prompts, logs, or commits.

## Build and test

Run the repository baseline from the root:

```powershell
dotnet build IdeaCadConnector.sln
dotnet test IdeaCadConnector.sln
```

Report the exact command and result. `Build not available` is not `Build passed`.

## Review and verification

- `idea-planner`: readiness and consistency checker.
- `idea-implementer`: implements only approved `tasks.md` or approved issue scope.
- `idea-reviewer`: reviews changes and does not modify source.
- `idea-verifier`: runs verification and does not modify source.
- Resolve BLOCKER/HIGH findings before completion; distinguish environment failures from regressions.

## Legacy transition

The following archived paths are historical: `docs/archive/legacy-ai-work-kit/tasks/ai/`, `docs/archive/legacy-ai-work-kit/docs/ai/`, `docs/archive/legacy-ai-work-kit/docs/plans/`, `docs/archive/legacy-ai-work-kit/docs/superpowers/`, and `docs/archive/legacy-ai-work-kit/.superpowers/`. Read them only for traceability or unmigrated knowledge; do not create new feature workflow there.

## Detailed references

- `.specify/memory/constitution.md`
- `CONTEXT.md`
- `docs/architecture/solution-architecture.md`
- `docs/domain/aras-and-cad-domain.md`
- `docs/development/build-and-test.md`
- `docs/security/data-safety.md`

Keep this file operational and short; do not turn it into a long onboarding document.
