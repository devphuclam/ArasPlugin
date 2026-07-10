---
description: Verify a ticket with build, tests, and scope checks
agent: idea-verifier
---
Verify ticket ID `$1` using @.ai-work/current-prompt.md and the repository's verification scripts.

Run:
- .\scripts\ai\Check-AiScope.ps1
- .\scripts\ai\Verify-AiTicket.ps1 -TicketId $1

Inspect the generated evidence and return the Verifier report. Do not modify files.
