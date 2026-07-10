# 11 — Context Pack Rules

## Always include

- Current ticket.
- AI Runbook.
- Project State.
- Architecture Rules.
- Aras Schema Map when Aras-related.
- Relevant public interfaces/DTOs.
- Existing tests for the behavior.

## Include only when relevant

- View/XAML for UI ticket.
- Server method for its client contract.
- Workspace persistence files for manifest/diff.
- Live server output after secrets are removed.

## Never include

- `.git/`;
- `.vs/`, `bin/`, `obj/`, artifacts;
- DLL/PDB/SNK;
- API keys/passwords/tokens;
- unrelated screenshots;
- entire old archived documentation unless needed.

## Maximum useful context

Prefer a precise set of 5–20 files over a raw ZIP. The agent should request more only after identifying a concrete missing dependency.
