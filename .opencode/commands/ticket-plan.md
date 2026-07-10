---
description: Plan the current AI ticket without modifying files
agent: idea-planner
---
Read the complete current ticket context from @.ai-work/current-prompt.md.

Also obey the project governance documents already loaded by OpenCode configuration.

Current repository state:
- Branch: !`git branch --show-current`
- HEAD: !`git rev-parse --short HEAD`
- Status: !`git status --short`
- Diff summary: !`git diff --stat`

Plan this ticket only. Do not edit files. Do not run build or tests. Return the required Planner format and stop for user approval.
