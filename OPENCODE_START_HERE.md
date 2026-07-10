# OpenCode + DeepSeek Free — IdeaCadConnector AI Workflow

This add-on adapts the existing AI Work Kit to OpenCode. It does not replace the backlog, tickets, governance documents, or verification scripts.

## Install location

The ZIP contains an `IdeaCadConnector` directory. Extract it into the parent directory that already contains your `IdeaCadConnector` project.

Correct result:

```text
D:\Projects\ARAS-Plugin\IdeaCadConnector\OPENCODE_START_HERE.md
```

Incorrect result:

```text
D:\Projects\ARAS-Plugin\IdeaCadConnector\IdeaCadConnector\OPENCODE_START_HERE.md
```

## Install

Open PowerShell at the project root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\ai\Install-OpenCodeAiWorkKit.ps1
```

If an `opencode.json` already exists, the installer backs it up and writes `opencode.ai-workkit.recommended.json` instead of replacing it. Review and merge the recommended settings. To explicitly replace the existing config after its backup:

```powershell
.\scripts\ai\Install-OpenCodeAiWorkKit.ps1 -ReplaceExistingOpenCodeConfig
```

## Start OpenCode

Close any OpenCode session that was already open, then from the repository root run:

```powershell
opencode
```

Use `/models` and keep the free DeepSeek model that already works for you. This package deliberately does not hard-code a provider or model ID.

Do not start OpenCode with `--auto`. The package requires approvals for edits and potentially risky shell commands, and explicitly denies destructive Git commands and `git push`.

## Current ticket: next action

Because `Start-AiTicket.ps1` has already prepared `.ai-work/current-prompt.md`, run this inside OpenCode:

```text
/ticket-plan
```

The planner must end with `READY_FOR_APPROVAL`, `NEEDS_SPLIT`, or `BLOCKED` and cannot edit files.

When the plan is correct, reply in the same session:

```text
Plan approved exactly as written. Do not expand scope.
```

Then run:

```text
/ticket-implement
```

After implementation, open a fresh OpenCode session for independent review and run:

```text
/ticket-review
```

If review findings are approved, return to the implementation session and run:

```text
/ticket-fix-review BLOCKER-1, HIGH-1
```

Finally open another fresh session and run, replacing `BASE-00` with the current ticket ID:

```text
/ticket-verify BASE-00
```

## Installed agents

- `idea-planner`: no edits, no build/test.
- `idea-implementer`: edits require approval; no push or destructive Git.
- `idea-reviewer`: independent, read-only review.
- `idea-verifier`: read-only verification with build/test access.

OpenCode primary agents can also be selected by cycling agents in the TUI, but the custom commands already select the right agent automatically.

## Commands

- `/ticket-plan`
- `/ticket-implement [extra approved notes]`
- `/ticket-review`
- `/ticket-fix-review <approved finding IDs>`
- `/ticket-verify <ticket ID>`
- `/ticket-status`

## Important

- Never approve `git reset`, `git clean`, `git push`, or bulk deletion.
- Never accept an Aras schema name guessed by the model.
- Do not accept “Done” without build/test evidence, unless the ticket is explicitly documentation-only.
- Use a new session for Reviewer and Verifier so they do not inherit the Implementer's bias.
