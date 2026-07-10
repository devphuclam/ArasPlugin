---
description: Independently review the current ticket diff
agent: idea-reviewer
---
Read @.ai-work/current-prompt.md and independently inspect the complete current Git diff plus relevant surrounding source.

Repository state:
- Branch: !`git branch --show-current`
- HEAD: !`git rev-parse --short HEAD`
- Status: !`git status --short`
- Diff summary: !`git diff --stat`

Do not edit files. Apply the Reviewer checklist and return findings plus a final verdict.
