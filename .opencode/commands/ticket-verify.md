---
description: Compatibility adapter to verify approved work
agent: idea-verifier
---
Verify approved feature task or issue `$1` from the repository root. Read canonical instructions first. Run the relevant tests and:

```powershell
dotnet build IdeaCadConnector.sln
dotnet test IdeaCadConnector.sln
```

Record exact commands, exit codes, results, scope, and environment blockers. Do not modify files or create evidence by guessing.
