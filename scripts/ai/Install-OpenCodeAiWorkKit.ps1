[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host 'The legacy OpenCode AI Work Kit installer is retired.' -ForegroundColor Yellow
Write-Host 'OpenCode is already configured through opencode.json and GitHub Spec Kit.' -ForegroundColor Cyan
Write-Host 'Do not recreate ticket commands or legacy instruction paths.' -ForegroundColor Yellow
Write-Host 'No files were changed.' -ForegroundColor Yellow
exit 1
