# Review Checklist

## Scope and correctness

- Does the diff match exactly one approved feature task or issue?
- Are unrelated refactors, renames, generated files, and binaries absent?
- Are failure, cancellation, null/empty/duplicate, and partial-failure paths correct?
- Are async cancellation, streams, HTTP responses, and disposables handled?
- Are IDs, item numbers, configuration IDs, relationship direction, and version semantics verified?

## Data safety and security

- Can a local file be overwritten without backup or safety validation?
- Are manifest and branch-head updates delayed until required work succeeds?
- Are operations idempotent or safely retryable?
- Are tokens, passwords, API keys, production endpoints, and sensitive content absent from code, tests, logs, and commits?
- Is path traversal and unsafe file overwrite prevented?

## Tests and evidence

- Do tests prove observable behavior rather than only implementation details?
- Was the full relevant suite executed?
- Are integration/manual gaps explicitly recorded?
- Are reviewer BLOCKER/HIGH findings resolved?

Source reference: `docs/ai/12_REVIEW_CHECKLIST.md`.
