# Data Safety

## Secrets

Never commit or paste Aras passwords, OAuth/access/refresh tokens, DeepSeek API keys, production connection strings, private customer files, or temporary credential-bearing URLs. Use placeholders and process-scoped environment variables.

## Destructive operations

Do not delete live Aras items, modify production schema, bulk-unlink CAD/Document relationships, overwrite a modified workspace, force-push, discard uncommitted work, or run production migration without an explicit reviewed environment-specific command.

## Logging

Redact authorization headers, token payloads, passwords, API keys, and sensitive file content. Log safe identifiers, operation type, safe path/filename, and error code instead.

## AI context

Before sending repository content to a hosted AI service, confirm company policy permits it. Prefer approved agents and provide only the precise context needed.

Source reference: archived `docs/archive/legacy-ai-work-kit/docs/ai/09_SECURITY_AND_DATA_SAFETY.md`.
