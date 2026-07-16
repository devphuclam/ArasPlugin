[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Write-Host 'The legacy AI Work Kit installer is retired.' -ForegroundColor Yellow
Write-Host 'Canonical instructions are AGENTS.md, .specify/memory/constitution.md, CONTEXT.md, and specs/<feature>/.' -ForegroundColor Cyan
Write-Host 'Use Spec Kit for features and the approved GitHub Issue workflow for bugs, hotfixes, and chores.' -ForegroundColor Cyan
Write-Host 'No files were changed.' -ForegroundColor Yellow
exit 1
