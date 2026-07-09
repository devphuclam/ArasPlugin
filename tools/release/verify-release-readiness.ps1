param(
    [string]$Version = "v0.3.0-rc1",
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SkipPackage,
    [string]$OutputDir = "artifacts/release"
)

$ErrorActionPreference = "Stop"
$exitCode = 0
$results = @()

function Write-Result([string]$Area, [string]$Outcome, [string]$Detail) {
    $symbol = if ($Outcome -eq "PASS") { "[PASS]" } else { "[FAIL]" }
    Write-Host "$symbol $Area`: $Detail"
    $script:results += @{ Area = $Area; Outcome = $Outcome; Detail = $Detail }
    if ($Outcome -ne "PASS") { $script:exitCode = 1 }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..\..")
$solutionPath = Join-Path $repoRoot "IdeaCadConnector.sln"

Write-Host "=== Release Readiness Verification ==="
Write-Host ""

# 1. Print current git SHA
$commit = (git -C $repoRoot rev-parse HEAD).Trim()
Write-Host "Git commit: $commit"
Write-Host ""

# 2. Workspace status
Write-Host "Workspace status:"
git -C $repoRoot status --short
Write-Host ""

# 3. Debug build
Write-Host "--- Debug build ---"
dotnet build $solutionPath -c Debug
if ($LASTEXITCODE -eq 0) {
    Write-Result "Debug build" "PASS" "0 warnings, 0 errors"
} else {
    Write-Result "Debug build" "FAIL" "Exit code $LASTEXITCODE"
    exit $exitCode
}

# 4. Release build
Write-Host "--- Release build ---"
dotnet build $solutionPath -c $Configuration
if ($LASTEXITCODE -eq 0) {
    Write-Result "Release build" "PASS" "0 warnings, 0 errors"
} else {
    Write-Result "Release build" "FAIL" "Exit code $LASTEXITCODE"
    exit $exitCode
}

# 5. Debug tests
if (-not $SkipTests) {
    Write-Host "--- Debug tests ---"
    dotnet test $solutionPath -c Debug --no-build
    if ($LASTEXITCODE -eq 0) {
        Write-Result "Debug tests" "PASS" "All tests passed"
    } else {
        Write-Result "Debug tests" "FAIL" "Exit code $LASTEXITCODE"
    }

    # 6. Release tests
    Write-Host "--- Release tests ---"
    dotnet test $solutionPath -c $Configuration --no-build
    if ($LASTEXITCODE -eq 0) {
        Write-Result "Release tests" "PASS" "All tests passed"
    } else {
        Write-Result "Release tests" "FAIL" "Exit code $LASTEXITCODE"
    }
} else {
    Write-Host "Skipping tests (SkipTests switch)"
}

# 7. Package creation
$packageScript = Join-Path $scriptRoot "package-release.ps1"
if (-not $SkipPackage) {
    Write-Host "--- Package creation ---"
    & $packageScript -Version $Version -Configuration $Configuration -OutputDir $OutputDir
    if ($LASTEXITCODE -eq 0) {
        Write-Result "Package script" "PASS" "Package created"
    } else {
        Write-Result "Package script" "FAIL" "Exit code $LASTEXITCODE"
    }
} else {
    Write-Host "Skipping package (SkipPackage switch)"
}

# 8. Validate package
$zipPath = Join-Path $repoRoot ($OutputDir + "\IdeaCadConnector-$Version.zip")
if (-not $SkipPackage -and (Test-Path -LiteralPath $zipPath)) {
    Write-Host "--- Package validation ---"
    $validationScript = Join-Path $scriptRoot "validate-release-package.ps1"
    & $validationScript -PackagePath $zipPath -ExpectedVersion $Version
    if ($LASTEXITCODE -eq 0) {
        Write-Result "Package validation" "PASS" "All checks passed"
    } else {
        Write-Result "Package validation" "FAIL" "Exit code $LASTEXITCODE"
    }
}

Write-Host ""
Write-Host "=== Verification Summary ==="
$passCount = ($results | Where-Object { $_.Outcome -eq "PASS" }).Count
$failCount = ($results | Where-Object { $_.Outcome -eq "FAIL" }).Count
Write-Host "PASS: $passCount"
Write-Host "FAIL: $failCount"
Write-Host "Exit code: $exitCode"

exit $exitCode
