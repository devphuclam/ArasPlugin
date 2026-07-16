# AI Prompt Library

These prompts are ready to copy and paste. Replace angle-bracket placeholders.

## 1. Clarify a feature

```text
Read AGENTS.md, .specify/memory/constitution.md, CONTEXT.md and the relevant domain documentation.
Use the grill-with-docs skill.
Help me clarify this feature before specification or implementation:

<FEATURE DESCRIPTION>

Ask focused questions about the user goal, current and expected behavior,
acceptance criteria, edge cases, failure behavior, security and data safety,
Aras schema evidence, and in-scope/out-of-scope behavior.
Do not modify source or create spec.md. Do not propose implementation details
until requirements are clear. End with a consolidated description suitable for
/speckit.specify.
```

## 2. Review spec.md

```text
Read AGENTS.md, the constitution, CONTEXT.md and <FEATURE_PATH>/spec.md.
Review the specification only. Check ambiguity, acceptance criteria, error and
empty states, conflicts, scope expansion, unverified Aras assumptions, and
implementation details incorrectly placed in the specification.
Do not modify source or create plan.md. Return BLOCKER, HIGH, MEDIUM, and LOW
findings with the affected section and a concrete correction.
```

## 3. Review plan and tasks

```text
Read AGENTS.md, the constitution, CONTEXT.md, and <FEATURE_PATH>/spec.md,
plan.md, and tasks.md. Use the codebase-design skill.
Check specification coverage; Core, Workspace, Aras, Ui, Desktop, and IronCAD
boundaries; dependency cycles; guessed Aras behavior; compatibility; task size,
ordering, traceability, testing, and scope. Do not modify source or create a
second plan/task list. Return BLOCKER, HIGH, MEDIUM, and LOW findings.
```

## 4. Implement approved tasks

```text
Read AGENTS.md and the canonical artifacts under <FEATURE_PATH>.
Use the tdd skill. Implement only unchecked tasks in tasks.md. For behavior
changes, write and run a failing test, make the smallest valid change, run the
focused test, and refactor only while green. Mark [x] only with evidence.
Do not change requirements, rewrite plan.md, expand scope, or modify files
outside the task without reporting the need. After each logical group report
task IDs, files, tests/results, and remaining tasks.
```

## 5. Independent code review

```text
Read AGENTS.md, the constitution, CONTEXT.md, and <FEATURE_PATH>/spec.md,
plan.md, and tasks.md. Use the code-review skill and review the current Git diff.
Review specification compliance, maintainability, architecture boundaries,
Aras/domain safety, security and secrets, compatibility, test quality, and
out-of-scope files. Do not modify source. For each BLOCKER, HIGH, MEDIUM, or
LOW finding include file/location, violated requirement, why it matters, and
the smallest correction.
```

## 6. Fix approved findings

```text
Read these approved findings:
<APPROVED FINDINGS>

Fix only approved BLOCKER and HIGH findings. Use tdd for behavior changes. Do
not fix lower-severity findings if that expands scope, refactor unrelated code,
or change specs/plans unless explicitly approved. Run focused tests and the
relevant review again, then report every changed file.
```

## 7. Investigate a bug

```text
Read AGENTS.md, the constitution, and relevant source/tests. Use the
diagnosing-bugs skill to investigate:

<BUG DESCRIPTION>

Reproduce the problem, isolate the smallest failing case, collect evidence,
test explicit hypotheses, and distinguish environment failure from regression.
Do not modify product source before identifying a supported root cause. Return
reproduction, observed/expected behavior, root cause, evidence, regression test,
smallest fix, and unresolved uncertainty.
```

## 8. Final verification

```text
Perform final verification for <FEATURE OR WORK ITEM>.
Run:
dotnet build IdeaCadConnector.sln
dotnet test IdeaCadConnector.sln
git status --short --untracked-files=all
git diff --check

Report commands, exit codes, warnings/errors, passed/failed/skipped tests,
changed and out-of-scope files, remaining unchecked tasks, and final Git status.
Do not claim completion when a required command was not run; distinguish tooling
failure from code regression.
```

## 9. Handoff

```text
Prepare a handoff for another AI working on this repository. Include the root,
branch, HEAD, working-tree status, active work item, canonical artifact paths,
completed and next task IDs, changed files, commands/results, open findings,
blockers, approved decisions, unsafe assumptions, and prohibited actions.
Do not omit failed commands or uncommitted changes.
```

## 10. New-session orientation

```text
Read AGENTS.md, the constitution, CONTEXT.md, active feature artifacts, current
Git status, and the latest commit. Do not modify anything. Based on repository
evidence, report the active work item, completed work, next approved task,
allowed files, required verification, blockers/findings, and the next skill.
```
