# Phase 0 Evidence — Spec Kit Migration

Timestamp: `2026-07-16T09:36:14.5478781+07:00`

## Repository state

| Property | Result |
|---|---|
| Root | `C:/Users/TD-999/Research/ArasInnovator/copilot-worktrees/Workspace/ArasPlugin` |
| Branch | `chore/spec-kit-workflow-migration` |
| Commit | `b9cfc52 docs: finalize Spec Kit migration design and technical plan` |
| Git directory | `.git` |
| Common directory | `.git` |
| Worktree | single worktree; not linked |
| Source/test changes | none |
| Status after build/test | clean |

The two approved documents were committed in `b9cfc52`. No source, test, project,
solution, OpenCode or `.gitignore` file was staged in that commit.

## Spec Kit CLI

| Command | Exit code | Standard output | Standard error | Conclusion |
|---|---:|---|---|---|
| `specify version` | 0 | CLI Version `0.12.16`; Python `3.14.6`; Windows AMD64; OS `10.0.19045` | empty | CLI verified |
| `specify version --features --json` | 0 | `controlled_multi_install_integrations`, `integration_use_command`, `workflow_catalog`, `bundled_templates` and related flags are true | empty | feature flags verified |
| `specify check` | 0 | CLI ready; Claude Code, Codex CLI, Gemini CLI, opencode and VS Code available | empty | tooling check pass |
| `specify self check` | 0 | `Up to date: 0.12.16` | empty | version current |
| `specify integration list` | 1 | empty | `Error: Not a Spec Kit project (no .specify/ directory)` followed by `Run this command from a Spec Kit project root or set SPECIFY_INIT_DIR to one.` | integration resolution blocked |
| `specify integration list --help` | 0 | Usage shows `--catalog` and `--help` | empty | catalog command verified |
| `specify integration list --catalog` | 1 | empty | same `Not a Spec Kit project (no .specify/ directory)` error | integration resolution blocked |
| `specify integration list --catalog --help` | 0 | Usage shows `--catalog` and `--help` | empty | catalog help verified |

No integration name was inferred. `specify init` was not run. No CLI reinstall,
repair or upgrade was attempted.

## Build/test baseline

Environment:

- .NET SDK `10.0.300`, MSBuild version reported by SDK `18.6.3`.
- `where.exe dotnet`: `C:\Program Files\dotnet\dotnet.exe`.
- `where.exe msbuild`: exit code `1`, no executable found in PATH.
- Solution target is .NET Framework `net48` through `Directory.Build.props`.

| Command | Exit code | Duration | Result | Failure category | Evidence |
|---|---:|---:|---|---|---|
| `dotnet --info` | 0 | <1 s | Pass | Pass | SDK/runtime information printed |
| `dotnet build IdeaCadConnector.sln` | 0 | 14.14 s | Pass | Pass | Build succeeded; 0 warnings; 0 errors |
| `dotnet test IdeaCadConnector.sln` | 0 | 19.25 s | Pass | Pass | 645 passed, 0 failed, 0 skipped |
| `msbuild IdeaCadConnector.sln` | not run | n/a | unavailable | Missing SDK/tooling | `where.exe msbuild` found no executable |

No source or test file was changed to establish this baseline. Build outputs were
created under existing `bin/`/`obj/` paths and did not leave Git changes.

## Git tracking

The following paths are ignored by `.gitignore:9:*.md`:

| Path | Ignored | Matching rule | Future minimal change |
|---|---:|---|---|
| `.specify/memory/constitution.md` | yes | `*.md` | unignore exact canonical path |
| `specs/001-example/spec.md` | yes | `*.md` | unignore `specs/` |
| `AGENTS.md` | yes | `*.md` | unignore exact path |
| `CONTEXT.md` | yes | `*.md` | unignore exact path |
| `docs/adr/0001-example.md` | yes | `*.md` | unignore `docs/adr/` |
| `.agents/skills/example/SKILL.md` | yes | `*.md` | decide repo-local skills first |

`.gitignore` was not changed in Phase 0.

## GitHub tooling

`Get-Command gh -ErrorAction SilentlyContinue` returned no command (`gh-not-found`).
Therefore `gh --version`, `gh auth status` and `gh repo view` could not execute.
The Git remote is verified as:

`https://github.com/devphuclam/ArasPlugin.git`

GitHub CLI installation, authentication and issue projection remain blocked and no
issue was created.

## Phase 0 result

PHASE 0 COMPLETE — FOUNDATION STILL BLOCKED

## Updated readiness

| Area | Status | Evidence |
|---|---|---|
| Repository branch | Ready | migration branch exists |
| Design/plan commit | Ready | commit `b9cfc52` contains two documents |
| Spec Kit CLI | Ready | `0.12.16`, check/self-check pass |
| Integration resolution | Blocked | no `.specify/` directory; catalog commands exit 1 |
| Build baseline | Complete | `dotnet build` exit 0 |
| Test baseline | Complete | 645/645 passed |
| Git tracking | Complete | broad `*.md` ignore recorded; no changes made |
| GitHub Issues tooling | Blocked | `gh` unavailable; auth unverified |
| Foundation implementation | Blocked | no approval and integration unresolved |

## Foundation Gate 1 evidence

### Precondition and approved scope

- Branch: `chore/spec-kit-workflow-migration`.
- Repository state before Foundation Gate 1 was clean; no modified design document was lost.
- Foundation Gate 1 approval covered only canonical tracking preparation, real Spec Kit initialization, collision verification, and regression baseline verification.
- No source, test, solution, project, `opencode.json`, existing OpenCode agent, or existing ticket command was intentionally changed.

### Canonical tracking preparation

The minimal canonical-artifact exception block was added to `.gitignore` and committed separately:

- Commit: `1084df1 chore: track canonical Spec Kit artifacts`.
- Tracked by the exception: `.specify/`, `specs/`, `AGENTS.md`, `CONTEXT.md`, and `docs/adr/`.
- `.agents/skills/example/SKILL.md` remained ignored; no local skill tree was created.

### Real repository initialization

Command executed:

```powershell
specify init --here --force --integration opencode --script ps
```

Result: exit code `0`, duration approximately `0.87s`. The command completed without creating a Git repository and without modifying existing tracked files. It generated the canonical `.specify/` infrastructure and ten Spec Kit OpenCode command files under `.opencode/commands/`.

Initialization commit:

- Commit: `55a7279 chore: initialize Spec Kit with OpenCode integration`.
- Files committed: `28`.
- Generated command set: `speckit.analyze`, `speckit.checklist`, `speckit.clarify`, `speckit.constitution`, `speckit.converge`, `speckit.implement`, `speckit.plan`, `speckit.specify`, `speckit.tasks`, and `speckit.taskstoissues`.
- Generated metadata confirms integration `opencode`, PowerShell script `ps`, default integration `opencode`, and active Spec Kit/OpenCode manifests.

### Collision and post-init checks

- Existing `.opencode/agents/**` remained unchanged.
- Existing ticket commands and `opencode.json` remained unchanged.
- No `AGENTS.md`, `CONTEXT.md`, `docs/adr/`, feature spec, issue, ADR, or source/test migration artifact was created.
- `specify integration list`, `specify integration list --catalog`, and `specify integration info opencode` each returned exit code `0` after initialization.
- Runtime integration catalog cache files were removed after verification; no cache files were included in the commit.

### Regression baseline after initialization

| Command | Exit code | Result |
|---|---:|---|
| `dotnet build IdeaCadConnector.sln` | 0 | Build succeeded; 0 warnings, 0 errors; 15.61s |
| `dotnet test IdeaCadConnector.sln` | 0 | 645 passed, 0 failed, 0 skipped; 5s test duration |
| `git status --short --untracked-files=all` | 0 | Clean; generated build output remains ignored |

### Gate result

`FOUNDATION INITIALIZED — READY FOR CANONICAL INSTRUCTION DESIGN`

Foundation Gate 1 is complete. The next approval is exactly one gate: approve canonical instruction design (`constitution + AGENTS.md + CONTEXT.md`). No canonical instruction files, domain docs, ADRs, feature specs, tasks, GitHub Issues, or source migration were created in this gate.

## Next approval requested after Foundation Gate 1

Approve exactly one next gate: canonical instruction design (`constitution + AGENTS.md + CONTEXT.md`).
Do not create those instruction files automatically before approval.


## Disposable Spec Kit init dry run

### Dry-run state

| Property | Result |
|---|---|
| Temp root | C:\Users\TD-999\AppData\Local\Temp\arasplugin-speckit-opencode-dryrun-20260716-094100 |
| Inside ArasPlugin | no |
| Git repository | no .git directory created |
| CLI | specify 0.12.16 |
| Integration/script | opencode / ps |
| Requested command | specify init --here --integration opencode --script ps --no-git |
| First result | exit 2: No such option: --no-git |
| Help result | specify init --help exit 0; --no-git unsupported |
| Executed safe command | specify init --here --integration opencode --script ps |
| Init result | exit 0; project ready |
| Duration | 0.93 seconds |

No --force, --ignore-agent-tools or --offline flag was used. No feature workflow was
run. The only deviation was removing the unsupported --no-git option after CLI help
proved it does not exist.

### Init output evidence

The successful output selected integration opencode and script ps, checked required
tools, installed the integration, installed shared PowerShell infrastructure and
templates, copied the constitution template, installed the bundled workflow, and
finalized the project. It exposed slash commands for constitution, specify, plan,
tasks, implement, converge, plus optional clarify, analyze and checklist. It warned
that .opencode may contain credentials or private artifacts.

### Complete generated file inventory

| Relative path | Type | Shared/OpenCode | Collision in ArasPlugin | Note |
|---|---|---|---|---|
| .opencode/commands/speckit.analyze.md | command | OpenCode | functional only | new name |
| .opencode/commands/speckit.checklist.md | command | OpenCode | none | new name |
| .opencode/commands/speckit.clarify.md | command | OpenCode | none | new name |
| .opencode/commands/speckit.constitution.md | command | OpenCode | none | new name |
| .opencode/commands/speckit.converge.md | command | OpenCode | none | new name |
| .opencode/commands/speckit.implement.md | command | OpenCode | ticket-implement overlap | reconcile |
| .opencode/commands/speckit.plan.md | command | OpenCode | ticket-plan overlap | Spec Kit owns plan |
| .opencode/commands/speckit.specify.md | command | OpenCode | none | new name |
| .opencode/commands/speckit.tasks.md | command | OpenCode | ticket-plan overlap | Spec Kit owns tasks |
| .opencode/commands/speckit.taskstoissues.md | command | OpenCode | none | GitHub only |
| .specify/init-options.json | metadata | shared | none | init state |
| .specify/integration.json | metadata | shared | none | integration state |
| .specify/integrations/opencode.manifest.json | manifest | shared | none | 10 OpenCode hashes |
| .specify/integrations/speckit.manifest.json | manifest | shared | none | scripts/templates hashes |
| .specify/memory/.constitution-template.json | metadata | shared | none | memory support |
| .specify/memory/constitution.md | constitution | shared | ignored by *.md | tracking gate |
| .specify/scripts/powershell/check-prerequisites.ps1 | script | shared | none | PowerShell |
| .specify/scripts/powershell/common.ps1 | script | shared | none | PowerShell |
| .specify/scripts/powershell/create-new-feature.ps1 | script | shared | none | PowerShell |
| .specify/scripts/powershell/setup-plan.ps1 | script | shared | none | PowerShell |
| .specify/scripts/powershell/setup-tasks.ps1 | script | shared | none | PowerShell |
| .specify/templates/checklist-template.md | template | shared | ignored by *.md | tracking gate |
| .specify/templates/constitution-template.md | template | shared | ignored by *.md | tracking gate |
| .specify/templates/plan-template.md | template | shared | ignored by *.md | tracking gate |
| .specify/templates/spec-template.md | template | shared | ignored by *.md | tracking gate |
| .specify/templates/tasks-template.md | template | shared | ignored by *.md | tracking gate |
| .specify/workflows/speckit/workflow.yml | workflow | shared | none | bundled |
| .specify/workflows/workflow-registry.json | registry | shared | none | bundled |

No .opencode/agents, .opencode/command singular directory, AGENTS.md or CONTEXT.md
was generated. No .git was created.

### Integration metadata

The parsed .specify/integration.json contains:

    version: 0.12.16
    integration_state_schema: 1
    installed_integrations: [opencode]
    integration_settings.opencode.script: ps
    integration_settings.opencode.invoke_separator: .
    integration: opencode
    default_integration: opencode

The parsed .specify/init-options.json contains:

    ai: opencode
    feature_numbering: sequential
    here: true
    integration: opencode
    script: ps
    speckit_version: 0.12.16

The OpenCode manifest records 10 managed speckit command files. The Spec Kit manifest
records 5 PowerShell scripts and 5 templates. The workflow registry records bundled
speckit workflow version 1.0.0, named Full SDD Cycle.

### Integration commands after disposable init

| Command | Exit | Standard output | Standard error | Conclusion |
|---|---:|---|---|---|
| specify integration list | 0 | table; default/installed opencode | empty | works |
| specify integration list --catalog | 0 | full catalog; opencode v1.0.0, installed/default | empty | works |
| specify integration info opencode | 0 | opencode v1.0.0, installed/currently active, GitHub Spec Kit repository | empty | confirmed |

### OpenCode command inventory and collision

| Generated command | Reads/writes | Existing ArasPlugin equivalent | Collision |
|---|---|---|---|
| speckit.analyze | spec/plan/tasks → report | none | none |
| speckit.checklist | requirements → checklist | none | none |
| speckit.clarify | spec → updated spec | none | none |
| speckit.constitution | principles → constitution | none | none |
| speckit.converge | code/spec/plan/tasks → tasks | none | none |
| speckit.implement | tasks → source/tests | ticket-implement | functional overlap |
| speckit.plan | spec → plan.md | ticket-plan | functional overlap |
| speckit.specify | description → spec.md | none | none |
| speckit.tasks | spec/plan → tasks.md | ticket-plan | functional overlap |
| speckit.taskstoissues | tasks/spec/plan → GitHub Issues | none | gated by reviewed tasks |

Existing agents are idea-planner, idea-implementer, idea-reviewer and idea-verifier.
Existing commands are ticket-plan, ticket-implement, ticket-review, ticket-fix-review,
ticket-verify and ticket-status. No exact path/name collision was found; functional
overlap requires adapter review.

### Dry-run comparison and pre-init manifest

| Dry-run path | Exists in ArasPlugin | Collision | Proposed owner/action |
|---|---:|---|---|
| .specify/** | no | none | Spec Kit; create after approval |
| .specify/integration.json | no | none | Spec Kit metadata |
| .specify/init-options.json | no | none | Spec Kit metadata |
| .specify/memory/constitution.md | no | ignored Markdown | create after tracking approval |
| .specify/scripts/** | no | none | Spec Kit scripts |
| .specify/templates/** | no | ignored Markdown | create after tracking approval |
| .opencode/commands/speckit.* | no | functional overlap | create, then reconcile ticket commands |
| .opencode/agents/** | yes | exact directory | preserve existing agents; do not overwrite |
| opencode.json | yes | root config | audit/update only after approval |
| AGENTS.md | no | none generated | create separately after approval |
| CONTEXT.md | no | none generated | create separately after approval |

### Proposed real-init command

    specify init --here --force --integration opencode --script ps

The dry run confirms opencode and ps. --force is only proposed because the real repo is
non-empty and CLI help defines it as the non-interactive merge/overwrite option for
--here. This command was not executed. Before real init, protect existing agents,
ticket commands, opencode.json, design/plan files and all source/test paths.

## Disposable dry-run result

DRY RUN COMPLETE — REPOSITORY INIT READY FOR APPROVAL

## Init readiness

| Area | Status | Evidence |
|---|---|---|
| OpenCode integration key | Ready | metadata says opencode installed/default |
| PowerShell scripts | Ready | script ps; five generated scripts |
| Generated path inventory | Ready | complete 30-file inventory above |
| Integration metadata | Ready | integration/init-options parsed |
| Integration commands | Ready in disposable project | list/catalog/info exit 0 |
| .opencode collision | Review required | existing agents/ticket commands |
| Root-file collision | Review required | opencode.json exists; AGENTS/CONTEXT not generated |
| .gitignore requirement | Review required | broad *.md ignores canonical Markdown |
| Rollback readiness | Ready | dry run outside repo; branch commit boundary |
| Repository init | Ready for approval only | not executed |

## Historical dry-run approval (superseded)

The dry-run approval gate was completed by Foundation Gate 1 above. This historical section
is retained as evidence of the disposable rehearsal and is no longer an outstanding request.
