# DeepSeek compatibility entry point

DeepSeek may be used as the model backend for an approved coding agent. This file is compatibility guidance; repository governance comes from the canonical instructions.

## Before using an agent

From the `ArasPlugin/` repository root, read:

- `AGENTS.md`
- `.specify/memory/constitution.md`
- `CONTEXT.md`

For new feature behavior, use Spec Kit artifacts under `specs/<feature>/` and the Spec Kit command sequence. Do not create feature plans or tickets in the archived legacy tree.

For bugs, hotfixes, and chores, use the approved issue tracker. Never guess Aras schema, permissions, lifecycle behavior, credentials, or product behavior.

## Safety

- Keep API keys and tokens process-scoped; never write them to the repository, prompts, logs, or evidence.
- Stop when schema facts, acceptance criteria, working-tree state, or verification evidence are uncertain.
- Do not modify source outside an approved Spec Kit task or issue.
- Run the repository baseline commands and report their exact outcomes before claiming success.

## Legacy references

`docs/archive/legacy-ai-work-kit/` is transitional or historical. It may be read for traceability but is not a canonical workflow location.
