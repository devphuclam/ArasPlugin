---
description: Show safe status for the current AI ticket
agent: idea-planner
---
Without modifying files, summarize:
- Branch: !`git branch --show-current`
- HEAD: !`git rev-parse --short HEAD`
- Status: !`git status --short`
- Diff summary: !`git diff --stat`
- Current prompt: @.ai-work/current-prompt.md

Report whether the ticket is in planning, implementation, review, verification, or blocked state, and identify the next safe action.
