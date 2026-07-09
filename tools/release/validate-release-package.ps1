param(
    [string]$PackagePath,
    [string]$ExpectedVersion = "v0.3.0-rc1",
    [switch]$RequireArasMethod = $true,
    [switch]$CheckNoActiveConfig = $true,
    [switch]$AllowPdb = $true
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

function Resolve-PackageRoot([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Container) {
        return (Resolve-Path -LiteralPath $Path).Path
    }
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $extracted = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "IdeaCadConnector_Validate_" + [Guid]::NewGuid().ToString("N"))
        try {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::ExtractToDirectory($Path, $extracted)
            return $extracted
        } catch {
            throw "Cannot extract zip: $_"
        }
    }
    throw "PackagePath not found: $Path"
}

function Clean-Extracted([string]$Path, [string]$originalInput) {
    if ($originalInput -ne $Path -and (Test-Path -LiteralPath $Path)) {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path -LiteralPath $PackagePath)) {
    Write-Host "[FAIL] PackagePath not found: $PackagePath"
    exit 1
}

$originalInput = $PackagePath
$packageRoot = Resolve-PackageRoot $PackagePath

try {
    Write-Host "Validating package at: $packageRoot"
    Write-Host ""

    # --- VERSION.txt ---
    $versionFile = Join-Path $packageRoot "VERSION.txt"
    if (Test-Path -LiteralPath $versionFile) {
        $content = Get-Content -LiteralPath $versionFile -Raw
        if ($content -match "version=$ExpectedVersion") {
            Write-Result "VERSION.txt" "PASS" "Exists and matches version $ExpectedVersion"
        } else {
            Write-Result "VERSION.txt" "FAIL" "Exists but version mismatch. Expected: $ExpectedVersion"
        }
    } else {
        Write-Result "VERSION.txt" "FAIL" "File not found"
    }

    # --- Required directories ---
    $requiredDirs = @("app", "aras", "docs", "checksums")
    foreach ($dir in $requiredDirs) {
        $path = Join-Path $packageRoot $dir
        if (Test-Path -LiteralPath $path -PathType Container) {
            Write-Result "Directory $dir" "PASS" "Exists"
        } else {
            Write-Result "Directory $dir" "FAIL" "Not found"
        }
    }

    # --- App executable ---
    $exePath = Join-Path $packageRoot "app\IdeaCadConnector.Desktop.exe"
    if (Test-Path -LiteralPath $exePath) {
        Write-Result "app\IdeaCadConnector.Desktop.exe" "PASS" "Exists"
    } else {
        Write-Result "app\IdeaCadConnector.Desktop.exe" "FAIL" "Not found"
    }

    # --- Aras method ---
    if ($RequireArasMethod) {
        $methodPath = Join-Path $packageRoot "aras\server-methods\idea_GetPrimaryIronCadForPart.cs"
        if (Test-Path -LiteralPath $methodPath) {
            Write-Result "aras\server-methods\idea_GetPrimaryIronCadForPart.cs" "PASS" "Exists"
        } else {
            Write-Result "aras\server-methods\idea_GetPrimaryIronCadForPart.cs" "FAIL" "Not found"
        }
    }

    # --- Config template ---
    $templatePath = Join-Path $packageRoot "docs\templates\IdeaCadConnector.environment.template.json"
    if (Test-Path -LiteralPath $templatePath) {
        Write-Result "docs\templates\IdeaCadConnector.environment.template.json" "PASS" "Exists"
    } else {
        Write-Result "docs\templates\IdeaCadConnector.environment.template.json" "FAIL" "Not found"
    }

    # --- Active config exclusion ---
    if ($CheckNoActiveConfig) {
        $activeConfigs = Get-ChildItem -LiteralPath $packageRoot -Filter "IdeaCadConnector.environment.json" -Recurse -ErrorAction SilentlyContinue
        if ($activeConfigs) {
            Write-Result "Active config excluded" "FAIL" "Found active config at: $($activeConfigs[0].FullName)"
        } else {
            Write-Result "Active config excluded" "PASS" "No IdeaCadConnector.environment.json in package"
        }

        $activeConfigsInDocsTemplates = Get-ChildItem -LiteralPath (Join-Path $packageRoot "docs") -Filter "IdeaCadConnector.environment.json" -Recurse -ErrorAction SilentlyContinue
        if ($activeConfigsInDocsTemplates) {
            Write-Result "Active config in docs\ " "FAIL" "Found unexpected config in docs folder"
        }
    }

    # --- Required docs ---
    $requiredDocs = @(
        "docs\INSTALL.md",
        "docs\CONFIGURATION.md",
        "docs\UAT-CHECKLIST.md",
        "docs\ROLLBACK.md",
        "docs\RELEASE-NOTES.md",
        "docs\INSTALLATION-HARDENING.md",
        "docs\MACHINE-READINESS.md",
        "docs\TROUBLESHOOTING.md",
        "docs\INTERNAL-UAT-RESULT-TEMPLATE.md",
        "docs\IT-HANDOFF.md"
    )
    foreach ($doc in $requiredDocs) {
        $path = Join-Path $packageRoot $doc
        if (Test-Path -LiteralPath $path) {
            Write-Result $doc "PASS" "Exists"
        } else {
            Write-Result $doc "FAIL" "Not found"
        }
    }

    # --- Validation script in package ---
    $validationScriptPath = Join-Path $packageRoot "tools\validate-release-package.ps1"
    if (Test-Path -LiteralPath $validationScriptPath) {
        Write-Result "tools\validate-release-package.ps1" "PASS" "Exists"
    } else {
        Write-Result "tools\validate-release-package.ps1" "FAIL" "Not found"
    }

    # --- Checksums ---
    $checksumFile = Join-Path $packageRoot "checksums\SHA256SUMS.txt"
    if (Test-Path -LiteralPath $checksumFile) {
        Write-Result "checksums\SHA256SUMS.txt" "PASS" "Exists"
    } else {
        Write-Result "checksums\SHA256SUMS.txt" "FAIL" "Not found"
    }

    # --- Forbidden files ---
    $forbiddenPatterns = @("*.user", "*.suo")
    if (-not $AllowPdb) {
        $forbiddenPatterns += "*.pdb"
    }
    $forbiddenFound = $false
    foreach ($pattern in $forbiddenPatterns) {
        $matches = Get-ChildItem -LiteralPath $packageRoot -Filter $pattern -Recurse -ErrorAction SilentlyContinue
        foreach ($m in $matches) {
            Write-Result "Forbidden file check" "FAIL" "Found forbidden file: $($m.FullName)"
            $forbiddenFound = $true
        }
    }
    if (-not $forbiddenFound) {
        Write-Result "Forbidden files (*.user, *.suo)" "PASS" "None found"
    }

    # --- Secret-like keys in template ---
    $templateContent = Get-Content -LiteralPath $templatePath -Raw -ErrorAction SilentlyContinue
    if ($templateContent) {
        $secretKeys = @("password", "token", "secret", "cookie", "session", "credential", "passphrase")
        $foundSecrets = $false
        foreach ($key in $secretKeys) {
            if ($templateContent -match "\""$key\"":") {
                Write-Result "Template secret scan" "FAIL" "Found secret-like key '$key' in template"
                $foundSecrets = $true
            }
        }
        if (-not $foundSecrets) {
            Write-Result "Template secret scan" "PASS" "No secret-like keys in template"
        }
    }

    Write-Host ""
    Write-Host "=== Validation Summary ==="
    $passCount = ($results | Where-Object { $_.Outcome -eq "PASS" }).Count
    $failCount = ($results | Where-Object { $_.Outcome -eq "FAIL" }).Count
    Write-Host "PASS: $passCount"
    Write-Host "FAIL: $failCount"
    Write-Host "Exit code: $exitCode"

} finally {
    Clean-Extracted $packageRoot $originalInput
}

exit $exitCode
