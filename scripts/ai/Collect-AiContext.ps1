[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host 'Collect-AiContext.ps1 belongs to the retired AI Work Kit workflow.' -ForegroundColor Yellow
Write-Host 'Read AGENTS.md, .specify/memory/constitution.md, CONTEXT.md, and the approved specs/<feature>/ artifacts instead.' -ForegroundColor Cyan
Write-Host 'No context pack was created.' -ForegroundColor Yellow
exit 1
