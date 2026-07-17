param(
    [Parameter(Mandatory)]
    [string]$SourceIcs,

    [Parameter(Mandatory)]
    [string]$OutputDir,

    [switch]$Open = $false
)

$ErrorActionPreference = 'Stop'
$toolDir = Split-Path -Parent $PSCommandPath
$probe = Join-Path $toolDir 'bin\Phase0Probe.exe'

if (-not (Test-Path -LiteralPath $probe)) {
    Write-Host "BUILDING Phase0Probe..."
    $src = Join-Path $toolDir 'Phase0Probe.cs'
    $refs = @(
        (Join-Path $toolDir 'bin\interop.ICApiIronCAD.dll')
    )
    $refArgs = $refs | ForEach-Object { "-r:$_" }
    Add-Type -Path $src -ReferencedAssemblies $refs -OutputAssembly $probe -OutputType ConsoleApplication
}

if (-not (Test-Path -LiteralPath $SourceIcs)) {
    Write-Error "Source file not found: $SourceIcs"
    exit 2
}

$null = New-Item -ItemType Directory -Force -Path $OutputDir

$openArg = if ($Open) { '--open' } else { '' }

Write-Host "=== Phase 0: T1-T6 Validation ==="
Write-Host "Source: $SourceIcs"
Write-Host "Output: $OutputDir"
Write-Host ""

# T1-T6 all use the same probe; results are interpreted from output
Write-Host "--- Running Phase0Probe ---"
$output = & $probe $SourceIcs $OutputDir $openArg 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host "PROBE FAILED (exit $exitCode)"
    Write-Host $output
    exit $exitCode
}

# Parse probe output
$links = @()
$topPresent = $false
$elementCount = 0
$saveNoneExists = $false
foreach ($line in $output) {
    Write-Host $line
    if ($line -match '^TOP_PRESENT=(.+)') { $topPresent = ($matches[1] -eq 'True') }
    if ($line -match '^ELEMENT_COUNT=(\d+)') { $elementCount = [int]$matches[1] }
    if ($line -match '^SAVE_NONE_EXISTS=(.+)') { $saveNoneExists = ($matches[1] -eq 'True') }
    if ($line -match '^LINK\|(\d+)\|(.*)') {
        $links += @{ Index = $matches[1]; ModelLinkPath = $matches[2] }
    }
}

Write-Host ""
Write-Host "=== Phase 0 Results ==="

# T1: Hierarchy preservation
if ($topPresent -and $elementCount -ge 3) {
    Write-Host "T1 PASS: Hierarchy preserved (top=$topPresent, elements=$elementCount)"
} else {
    Write-Host "T1 FAIL: Hierarchy NOT preserved (top=$topPresent, elements=$elementCount)"
}

# T2: Transform preservation -- requires manual verification
Write-Host "T2 MANUAL: Open '$OutputDir\save-none.ics' in IronCAD and verify occurrence transforms match source positions"

# T3: IZElement dedup -- requires separate script with multiple occurrences
Write-Host "T3 MANUAL: Create scene with 1 part at 3 positions; run probe; verify links share the same file path"

# T4: Save-through-root -- requires manual UAT
Write-Host "T4 MANUAL: Follow quickstart.md UAT scenario - edit child through root, verify SHA256 changes"

# T5: Custom property round-trip
Write-Host "T5 MANUAL: Open saved file in IronCAD; verify all 6 PDM properties present on every element"

# T6: External link isolation
$outsideLinks = $links | Where-Object { $_.ModelLinkPath -and $_.ModelLinkPath -notmatch [regex]::Escape($OutputDir) }
if ($outsideLinks.Count -eq 0) {
    Write-Host "T6 PASS: No external links point outside output directory"
} else {
    Write-Host "T6 FAIL: $($outsideLinks.Count) link(s) point outside output directory"
    foreach ($link in $outsideLinks) {
        Write-Host "  Element $($link.Index): $($link.ModelLinkPath)"
    }
}

Write-Host ""
Write-Host "=== Outcome Determination ==="
Write-Host "See specs/002-ironcad-linked-export/research.md for full decision matrix."
Write-Host "Record results in research.md Consolidated Decisions table."

exit $exitCode
