# 10 — DeepSeek Workflow

## Recommended topology

```text
DeepSeek model
  ↓ API backend
Coding agent with filesystem + terminal
  ↓
Ticket branch in IdeaCadConnector
```

Use a reasoning/pro model for planning and complex implementation. Start a fresh session for review so the reviewer is not anchored to the implementer's reasoning.

## Session sequence

1. Run `Start-AiTicket.ps1`.
2. Open DeepSeek-backed coding agent.
3. Planner reads `.ai-work/current-prompt.md` and returns plan only.
4. User approves or corrects plan.
5. Implementer edits and tests.
6. Close session.
7. New Reviewer session reads ticket and full diff.
8. Implementer fixes accepted findings.
9. Run `Verify-AiTicket.ps1`.
10. Create PR using template.

## Context hygiene

Do not dump the entire repository into every prompt. Give:

- governance docs;
- current ticket;
- files discovered by planner;
- related contracts/tests;
- current diff.

Ask the agent to search the repo instead of assuming README is complete.

## DeepSeek web chat mode

Use only for:

- reviewing a pasted diff;
- drafting a ticket;
- explaining an error;
- analyzing a limited uploaded context bundle.

Do not treat web-chat claims as build evidence.
