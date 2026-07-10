---
description: Fix only approved review findings for the current ticket
agent: idea-implementer
---
Read @.ai-work/current-prompt.md.

Fix only these explicitly approved review findings:
$ARGUMENTS

Do not implement unapproved suggestions or refactor unrelated code. Add/update tests for each corrected finding, rerun verification, and map every finding to its correction and test evidence.
