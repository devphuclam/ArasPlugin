# Spec Kit adapter contract

The canonical Spec Kit command implementations live in `.opencode/commands/`.
The `speckit-*` directories under `.agents/skills/` are Codex adapters only.

When a Codex adapter is invoked:

1. Read the referenced `.opencode/commands/speckit.<command>.md` completely.
2. Treat the current user request as the command's `$ARGUMENTS`.
3. Follow the command's preconditions, artifact paths, quality gates, and
   stopping rules exactly.
4. Use the repository's canonical source order from `AGENTS.md`.
5. Do not create competing `spec.md`, `plan.md`, or `tasks.md` artifacts.
6. Report the exact files changed and verification results.

OpenCode remains the default Spec Kit implementation runtime. Codex can review
and execute the same command contract when the user explicitly invokes the
corresponding adapter.
