[CmdletBinding()]
param(
    [int]$MaxFiles = 15
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$files = @(& git diff --name-only)
$count = $files.Count
Write-Host "Changed files: $count"
$files | ForEach-Object { Write-Host " - $_" }
if ($count -gt $MaxFiles) {
    Write-Host "Scope warning: more than $MaxFiles files changed. Stop and review/split the ticket." -ForegroundColor Red
    exit 2
}
$forbidden = $files | Where-Object { $_ -match '(^|/)(bin|obj|\.vs|artifacts)/|\.(dll|pdb|snk|png)$' }
if ($forbidden) {
    Write-Host 'Forbidden/generated/binary files are in the diff:' -ForegroundColor Red
    $forbidden | ForEach-Object { Write-Host " - $_" }
    exit 3
}
Write-Host 'Basic scope check passed.' -ForegroundColor Green
