param(
    [string]$Version = "v0.3.0-rc1",
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/release"
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-Exists([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Copy-RequiredFile([string]$Source, [string]$Destination) {
    Assert-Exists $Source "Required file"
    $destinationDir = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir | Out-Null
    }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "..\..")
$solutionPath = Join-Path $repoRoot "IdeaCadConnector.sln"
$desktopOutput = Join-Path $repoRoot "src\IdeaCadConnector.Desktop\bin\$Configuration\net48"
$methodSource = Join-Path $repoRoot "src\IdeaCadConnector.Aras\ServerMethods\idea_GetPrimaryIronCadForPart.cs"
$phase3Docs = Join-Path $repoRoot "docs\part-library\phase-3"
$outputRoot = Resolve-FullPath (Join-Path $repoRoot $OutputDir)
$packageName = "IdeaCadConnector-$Version"
$stageRoot = Join-Path $outputRoot $packageName
$zipPath = Join-Path $outputRoot "$packageName.zip"

if (-not $outputRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDir must resolve inside the repository: $outputRoot"
}

Assert-Exists $solutionPath "Solution"
Assert-Exists $methodSource "Aras server method"
Assert-Exists $phase3Docs "Phase 3 docs folder"

$envConfigName = "IdeaCadConnector.environment.json"
$envConfigActive = Join-Path $repoRoot $envConfigName

# Block packaging if active environment config would be copied
if (Test-Path -LiteralPath $envConfigActive) {
    throw "Active environment config found at $envConfigActive. Refusing to package active config with possible secrets."
}

$envConfigFilesInDesktop = Get-ChildItem -LiteralPath $repoRoot -Filter $envConfigName -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\TestResults\*" -and $_.FullName -notlike "*\artifacts\*" }
foreach ($f in $envConfigFilesInDesktop) {
    if ($f.DirectoryName -ne (Join-Path $repoRoot "docs\part-library\phase-3\templates")) {
        throw "Unexpected active config file at $($f.FullName). Refusing to package."
    }
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageRoot | Out-Null

Write-Host "Building $Configuration..."
dotnet build $solutionPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Assert-Exists $desktopOutput "Desktop output folder"

$appDir = Join-Path $stageRoot "app"
$arasMethodsDir = Join-Path $stageRoot "aras\server-methods"
$docsDir = Join-Path $stageRoot "docs"
$checksumsDir = Join-Path $stageRoot "checksums"
$toolsDir = Join-Path $stageRoot "tools"

New-Item -ItemType Directory -Path $appDir, $arasMethodsDir, $docsDir, $checksumsDir, $toolsDir | Out-Null

Get-ChildItem -LiteralPath $desktopOutput -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $appDir $_.Name) -Force
}

Assert-Exists (Join-Path $appDir "IdeaCadConnector.Desktop.exe") "Packaged desktop executable"

Copy-RequiredFile $methodSource (Join-Path $arasMethodsDir "idea_GetPrimaryIronCadForPart.cs")
Copy-RequiredFile (Join-Path $phase3Docs "DEPLOYMENT.md") (Join-Path $stageRoot "aras\README-Aras-Deployment.md")
Copy-RequiredFile (Join-Path $phase3Docs "DEPLOYMENT.md") (Join-Path $docsDir "INSTALL.md")
Copy-RequiredFile (Join-Path $phase3Docs "ENVIRONMENT-CONFIGURATION.md") (Join-Path $docsDir "CONFIGURATION.md")
Copy-RequiredFile (Join-Path $phase3Docs "UAT-CHECKLIST.md") (Join-Path $docsDir "UAT-CHECKLIST.md")
Copy-RequiredFile (Join-Path $phase3Docs "ROLLBACK.md") (Join-Path $docsDir "ROLLBACK.md")
Copy-RequiredFile (Join-Path $phase3Docs "RELEASE-NOTES-v0.3.0-rc1.md") (Join-Path $docsDir "RELEASE-NOTES.md")
Copy-RequiredFile (Join-Path $phase3Docs "templates\IdeaCadConnector.environment.template.json") (Join-Path $stageRoot "docs\templates\IdeaCadConnector.environment.template.json")

# Sprint 3.3 — Installation hardening docs
Copy-RequiredFile (Join-Path $phase3Docs "INSTALLATION-HARDENING.md") (Join-Path $docsDir "INSTALLATION-HARDENING.md")
Copy-RequiredFile (Join-Path $phase3Docs "MACHINE-READINESS.md") (Join-Path $docsDir "MACHINE-READINESS.md")
Copy-RequiredFile (Join-Path $phase3Docs "TROUBLESHOOTING.md") (Join-Path $docsDir "TROUBLESHOOTING.md")
Copy-RequiredFile (Join-Path $phase3Docs "INTERNAL-UAT-RESULT-TEMPLATE.md") (Join-Path $docsDir "INTERNAL-UAT-RESULT-TEMPLATE.md")
Copy-RequiredFile (Join-Path $phase3Docs "IT-HANDOFF.md") (Join-Path $docsDir "IT-HANDOFF.md")

# Validation script
$validationScript = Join-Path $repoRoot "tools\release\validate-release-package.ps1"
if (Test-Path -LiteralPath $validationScript) {
    Copy-Item -LiteralPath $validationScript -Destination (Join-Path $toolsDir "validate-release-package.ps1") -Force
} else {
    Write-Host "[WARNING] Validation script not found at $validationScript - will not be included in package"
}

$commit = (git -C $repoRoot rev-parse HEAD).Trim()
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$versionText = @(
    "version=$Version",
    "git_commit=$commit",
    "build_timestamp_utc=$timestamp",
    "configuration=$Configuration"
) -join [Environment]::NewLine
Set-Content -LiteralPath (Join-Path $stageRoot "VERSION.txt") -Value $versionText -Encoding UTF8

$checksumFile = Join-Path $checksumsDir "SHA256SUMS.txt"
$filesForChecksum = Get-ChildItem -LiteralPath $stageRoot -File -Recurse |
    Where-Object { $_.FullName -ne $checksumFile } |
    Sort-Object FullName

$checksumLines = foreach ($file in $filesForChecksum) {
    $relativePath = $file.FullName.Substring($stageRoot.Length + 1).Replace("\", "/")
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    "$($hash.Hash.ToLowerInvariant())  $relativePath"
}
Set-Content -LiteralPath $checksumFile -Value $checksumLines -Encoding ASCII

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath -Force

Assert-Exists $zipPath "Release zip"
Assert-Exists (Join-Path $stageRoot "VERSION.txt") "VERSION.txt"
Assert-Exists $checksumFile "SHA256SUMS.txt"
Assert-Exists (Join-Path $arasMethodsDir "idea_GetPrimaryIronCadForPart.cs") "Required Aras method in package"

Write-Host "Release package created:"
Write-Host "  $zipPath"
