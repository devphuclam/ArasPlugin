# 09 — Security and Data Safety

## Secrets

Never commit or paste into AI context:

- Aras password;
- OAuth/access/refresh tokens;
- DeepSeek API key;
- production connection strings;
- private customer files;
- Vault URLs containing temporary credentials.

Use placeholders and process-scoped environment variables.

## Repository context

Before uploading files to a hosted AI service, confirm company policy permits source code and schema sharing. Prefer a coding agent configured for the approved environment.

## Destructive operations

AI must not:

- delete live Aras items;
- modify production schema;
- bulk-unlink CAD/Document relationships;
- overwrite a modified workspace;
- force-push shared branches;
- discard uncommitted work;
- run migration against production.

without an explicit, reviewed, environment-specific command.

## Logging

Redact:

- Authorization headers;
- token payloads;
- passwords;
- API keys;
- sensitive file content.

Log IDs, operation type, safe path/filename and error code instead.
