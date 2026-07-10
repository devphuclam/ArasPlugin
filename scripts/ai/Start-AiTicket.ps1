[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Z]+-\d{2}$')]
    [string]$TicketId,
    [switch]$NoBranch
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
if (-not (Test-Path 'IdeaCadConnector.sln')) { throw 'IdeaCadConnector.sln not found.' }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git is required.' }

$status = & git status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
if ($status -and $TicketId -ne 'BASE-00') {
    Write-Host 'Working tree is not clean:' -ForegroundColor Red
    $status | ForEach-Object { Write-Host $_ }
    throw 'Commit/stash/backup current work before starting an AI ticket. Use BASE-00 to establish the baseline.'
}
if ($status -and $TicketId -eq 'BASE-00') {
    Write-Host 'BASE-00 is allowed to inspect a dirty tree. No branch will be created automatically.' -ForegroundColor Yellow
    $NoBranch = $true
}

$matches = Get-ChildItem -Path 'tasks\ai\tickets' -Filter "$TicketId-*.md"
if ($matches.Count -ne 1) { throw "Expected exactly one ticket for $TicketId; found $($matches.Count)." }
$ticket = $matches[0]
$slug = [IO.Path]::GetFileNameWithoutExtension($ticket.Name).ToLowerInvariant()
$branch = "ai/$slug"

if (-not $NoBranch) {
    $existing = & git branch --list $branch
    if ($existing) {
        & git switch $branch
    } else {
        & git switch -c $branch
    }
    if ($LASTEXITCODE -ne 0) { throw "Could not switch/create $branch" }
}

$work = Join-Path $repoRoot '.ai-work'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$head = (& git rev-parse HEAD).Trim()
$currentBranch = (& git branch --show-current).Trim()
$ticketText = Get-Content $ticket.FullName -Raw
$prompt = @"
# CURRENT AI TICKET

Repository: IdeaCadConnector
Branch: $currentBranch
Base/current HEAD: $head
Ticket file: $($ticket.FullName.Substring($repoRoot.Length + 1))

## Mandatory instructions

Read in order:
1. AI_START_HERE.md
2. docs/ai/01_AI_RUNBOOK.md
3. docs/ai/02_PROJECT_STATE.md
4. docs/ai/03_ARCHITECTURE_RULES.md
5. docs/ai/04_ARAS_SCHEMA_MAP.md
6. docs/ai/05_TESTING_GUIDE.md
7. docs/ai/prompts/01_PLANNER.md
8. The ticket below

Start in PLANNER mode. Do not edit code until the user approves the plan.
Search the current source and tests. Do not trust README as complete.
Stop on schema uncertainty, destructive risk, dirty tree, or scope expansion.

## Ticket

$ticketText
"@
$promptPath = Join-Path $work 'current-prompt.md'
Set-Content -Path $promptPath -Value $prompt -Encoding UTF8

Write-Host "Ticket prepared: $TicketId" -ForegroundColor Green
Write-Host "Branch: $currentBranch"
Write-Host "Prompt: $promptPath"
Write-Host 'Next run: .\scripts\ai\Start-DeepSeekClaudeCode.ps1'
