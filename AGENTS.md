# ArasPlugin Agent Instructions

## Repository scope

- Run commands from the `ArasPlugin/` repository root.
- Do not use parent-workspace instructions as a substitute for this file.
- Repository-local `.agents/skills/` is the project's canonical skill set.
- Keep changes within the approved feature, issue, or chore scope.

## Canonical sources

Use this order when sources disagree:

1. Source and tests for current behavior.
2. `.specify/memory/constitution.md`.
3. `specs/<feature>/spec.md`.
4. `specs/<feature>/plan.md`.
5. `specs/<feature>/tasks.md`.
6. `CONTEXT.md` and `docs/domain/`.
7. ADRs and detailed references.

## AI collaboration model

ChatGPT/Codex handles requirement clarification, research, architecture analysis,
independent artifact review, and independent code review. OpenCode hosts Spec
Kit commands and is the default implementation runtime for focused fixes,
builds, and tests. Either tool may implement when explicitly assigned, but only
one writer may edit source on a branch at a time.

## Work routing

- Feature: use Spec Kit artifacts under `specs/<feature>/`.
- Bug, hotfix, or chore: use an approved work item or GitHub Issue.
- Requirement clarification: `grill-with-docs`.
- Domain terminology: `domain-modeling`.
- Technical design review: `codebase-design`.
- Implementation: `tdd`.
- Bug investigation: `diagnosing-bugs`.
- Independent review: `code-review`.

## Supporting skills

- `grill-with-docs`: clarify requirements, terminology, and acceptance criteria.
- `domain-modeling`: maintain domain vocabulary, context, and ADRs.
- `codebase-design`: review technical plans and architecture boundaries.
- `tdd`: use red-green-refactor for approved tasks.
- `diagnosing-bugs`: reproduce and isolate bugs before fixing them.
- `code-review`: review implementation against specifications and standards.

No skill may create a competing `spec.md`, `plan.md`, or `tasks.md`.

## Writer and reviewer rule

- Never let ChatGPT/Codex and OpenCode edit source simultaneously.
- The writer completes its diff before the reviewer starts.
- Reviewers do not modify source during review passes.
- Findings use severity `BLOCKER`, `HIGH`, `MEDIUM`, or `LOW`.
- The writer fixes only findings that have been confirmed and approved.

## Safety constraints

- Never guess Aras schema or live product behavior.
- Do not modify source outside the approved task or issue.
- Do not use destructive Git commands.
- Do not edit the registry or run `.reg` files without explicit request.
- Do not install dependencies or tools outside approved scope.
- Never write secrets to files, prompts, logs, or commits.

## Build and test

Run from the repository root and report exact results:

```powershell
dotnet build IdeaCadConnector.sln
dotnet test IdeaCadConnector.sln
```

`Build not available` is not `Build passed`.

## Handoff requirements

Record the repository root, branch, HEAD, working-tree status, active work item,
canonical artifact paths, completed and next tasks, changed files, commands and
results, open findings, blockers, approved decisions, unsafe assumptions, and
prohibited next actions.

## Detailed references

- `.specify/memory/constitution.md`
- `CONTEXT.md`
- `docs/architecture/solution-architecture.md`
- `docs/domain/aras-and-cad-domain.md`
- `docs/development/build-and-test.md`
- `docs/development/chatgpt-opencode-workflow.md`
- `docs/development/ai-prompt-library.md`
- `docs/security/data-safety.md`
- `docs/adr/`

Keep this file operational and short.
