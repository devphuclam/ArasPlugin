[CmdletBinding()]
param(
    [switch]$ReplaceExistingOpenCodeConfig
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repoRoot 'IdeaCadConnector.sln'
if (-not (Test-Path $solution)) {
    throw "Run this inside the IdeaCadConnector repository. Solution not found: $solution"
}

$templateRoot = Join-Path $repoRoot '.ai-workkit-opencode-template'
if (-not (Test-Path $templateRoot)) {
    throw "OpenCode template folder not found: $templateRoot"
}

$workRoot = Join-Path $repoRoot '.ai-work'
$backupRoot = Join-Path $workRoot 'backups'
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

$targetOpenCode = Join-Path $repoRoot '.opencode'
New-Item -ItemType Directory -Force -Path $targetOpenCode | Out-Null

foreach ($subdir in @('agents', 'commands')) {
    $sourceDir = Join-Path $templateRoot $subdir
    $targetDir = Join-Path $targetOpenCode $subdir
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    Get-ChildItem -Path $sourceDir -File | ForEach-Object {
        $target = Join-Path $targetDir $_.Name
        if (Test-Path $target) {
            Copy-Item $target (Join-Path $backupRoot ("$subdir-$($_.Name).$stamp.bak")) -Force
        }
        Copy-Item $_.FullName $target -Force
    }
}

$templateConfig = Join-Path $templateRoot 'opencode.json'
$targetConfig = Join-Path $repoRoot 'opencode.json'
if (Test-Path $targetConfig) {
    Copy-Item $targetConfig (Join-Path $backupRoot "opencode.json.$stamp.bak") -Force
    if ($ReplaceExistingOpenCodeConfig) {
        Copy-Item $templateConfig $targetConfig -Force
        Write-Host 'Replaced existing opencode.json after creating a backup.' -ForegroundColor Yellow
    } else {
        $recommended = Join-Path $repoRoot 'opencode.ai-workkit.recommended.json'
        Copy-Item $templateConfig $recommended -Force
        Write-Host 'Existing opencode.json was not replaced.' -ForegroundColor Yellow
        Write-Host "Recommended config written to: $recommended" -ForegroundColor Yellow
        Write-Host 'Merge its instructions, permissions, compaction, watcher, shell, and snapshot settings manually.' -ForegroundColor Yellow
    }
} else {
    Copy-Item $templateConfig $targetConfig -Force
    Write-Host 'Created opencode.json.' -ForegroundColor Green
}

$gitIgnore = Join-Path $repoRoot '.gitignore'
if (-not (Test-Path $gitIgnore)) {
    New-Item -ItemType File -Path $gitIgnore | Out-Null
}
$begin = '# BEGIN OPENCODE AI WORK KIT'
$end = '# END OPENCODE AI WORK KIT'
$text = Get-Content $gitIgnore -Raw
if ($text -notmatch [regex]::Escape($begin)) {
    $block = @"

$begin
!OPENCODE_START_HERE.md
!opencode.json
!opencode.ai-workkit.recommended.json
!.opencode/
!.opencode/**
!scripts/ai/Install-OpenCodeAiWorkKit.ps1

# Template sources are not needed after installation.
.ai-workkit-opencode-template/
$end
"@
    Add-Content -Path $gitIgnore -Value $block -Encoding UTF8
    Write-Host 'Added OpenCode AI Work Kit rules to .gitignore.' -ForegroundColor Green
}

$required = @(
    (Join-Path $targetOpenCode 'agents\idea-planner.md'),
    (Join-Path $targetOpenCode 'agents\idea-implementer.md'),
    (Join-Path $targetOpenCode 'agents\idea-reviewer.md'),
    (Join-Path $targetOpenCode 'agents\idea-verifier.md'),
    (Join-Path $targetOpenCode 'commands\ticket-plan.md'),
    (Join-Path $targetOpenCode 'commands\ticket-implement.md'),
    (Join-Path $targetOpenCode 'commands\ticket-review.md'),
    (Join-Path $targetOpenCode 'commands\ticket-verify.md')
)
$missing = $required | Where-Object { -not (Test-Path $_) }
if ($missing.Count -gt 0) {
    throw "Installation incomplete. Missing: $($missing -join ', ')"
}

Write-Host ''
Write-Host 'OpenCode AI Work Kit installed.' -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host 'Next:' -ForegroundColor Cyan
Write-Host '  1. Close any OpenCode session currently open for this repo.'
Write-Host '  2. Start OpenCode again from the repository root: opencode'
Write-Host '  3. Keep your existing free DeepSeek model selected in /models.'
Write-Host '  4. Run /ticket-plan for the ticket already prepared in .ai-work/current-prompt.md.'
Write-Host '  5. Do not use opencode --auto for this project.'
