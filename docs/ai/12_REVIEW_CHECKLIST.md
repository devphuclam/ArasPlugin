# 12 — Review Checklist

## Scope

- Does the diff match exactly one ticket?
- Are unrelated renames/formatting/refactors absent?
- Did the AI modify generated/binary files?

## Correctness

- Is failure ever returned as success?
- Are null/empty/duplicate/cancel cases covered?
- Is async cancellation propagated?
- Are streams/HTTP responses/disposables handled?
- Are IDs confused with item numbers/config IDs?
- Are Aras relationship direction and version semantics verified?

## Data safety

- Can a local file be overwritten without backup?
- Is manifest/branch head updated too early?
- Can partial upload create inconsistent metadata?
- Is operation idempotent or safely retryable?

## Tests

- Do tests prove behavior rather than implementation detail?
- Is there a negative-path test?
- Was the full relevant suite executed?
- Are integration/manual gaps stated?

## Security

- No tokens/passwords/API keys in code/log/test fixtures.
- No production endpoint hardcoded.
- No untrusted path traversal or unsafe file overwrite.
