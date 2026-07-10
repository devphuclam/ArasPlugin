[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Z]+-\d{2}$')]
    [string]$TicketId
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$matches = Get-ChildItem 'tasks\ai\tickets' -Filter "$TicketId-*.md"
if ($matches.Count -ne 1) { throw "Ticket not found or ambiguous: $TicketId" }

$out = Join-Path $repoRoot ".ai-work\context\$TicketId"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$files = @(
    'AI_START_HERE.md',
    'DEEPSEEK.md',
    'docs\ai\01_AI_RUNBOOK.md',
    'docs\ai\02_PROJECT_STATE.md',
    'docs\ai\03_ARCHITECTURE_RULES.md',
    'docs\ai\04_ARAS_SCHEMA_MAP.md',
    'docs\ai\05_TESTING_GUIDE.md',
    'docs\ai\08_DEFINITION_OF_DONE.md',
    'docs\ai\12_REVIEW_CHECKLIST.md'
)
foreach ($file in $files) {
    if (Test-Path $file) {
        $dest = Join-Path $out $file
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        Copy-Item $file $dest
    }
}
Copy-Item $matches[0].FullName (Join-Path $out $matches[0].Name)
& git status --short | Set-Content (Join-Path $out 'git-status.txt')
& git log -5 --oneline | Set-Content (Join-Path $out 'git-log.txt')
& git diff --stat | Set-Content (Join-Path $out 'git-diff-stat.txt')
& git diff | Set-Content (Join-Path $out 'git-diff.patch')

Write-Host "Context pack created: $out" -ForegroundColor Green
Write-Host 'Review it for secrets before uploading to any hosted chat.' -ForegroundColor Yellow
