# Verifier Prompt

Do not implement new features.

1. Inspect git status and diff scope.
2. Clean/build solution.
3. Run ticket-specific tests.
4. Run the broader relevant suite.
5. Map every acceptance criterion to code and evidence.
6. Distinguish baseline failure from PR regression.
7. Verify docs/project state updates.
8. Report exact commands and outputs.

A command not run must be marked NOT RUN, never PASS.
