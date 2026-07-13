[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Z]+-\d{2}(-[A-Z0-9]+)*$')]
    [string]$TicketId,
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outDir = Join-Path $repoRoot ".ai-work\verification\$TicketId-$stamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Run-Step([string]$Name, [scriptblock]$Command) {
    $log = Join-Path $outDir "$Name.log"
    "COMMAND STEP: $Name`nSTART: $(Get-Date -Format o)`n" | Set-Content $log
    & $Command *>&1 | Tee-Object -FilePath $log -Append
    $code = $LASTEXITCODE
    "`nEXIT CODE: $code`nEND: $(Get-Date -Format o)" | Add-Content $log
    return $code
}

& git rev-parse HEAD | Set-Content (Join-Path $outDir 'head.txt')
& git branch --show-current | Set-Content (Join-Path $outDir 'branch.txt')
& git status --short | Set-Content (Join-Path $outDir 'status.txt')
& git diff --stat | Set-Content (Join-Path $outDir 'diff-stat.txt')

$buildHelper = Join-Path $repoRoot 'scripts\build-solution.ps1'
if (Test-Path $buildHelper) {
    $buildCode = Run-Step 'build' { & $buildHelper -Configuration $Configuration }
} else {
    $msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuild) {
        $buildCode = Run-Step 'build' { & $msbuild.Source 'IdeaCadConnector.sln' /m /t:Restore,Build /p:Configuration=$Configuration '/p:Platform=Any CPU' /v:minimal }
    } else {
        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($dotnet) {
            $buildCode = Run-Step 'build' { & $dotnet.Source build 'IdeaCadConnector.sln' --configuration $Configuration --no-restore -m:1 }
        } else {
            'NOT RUN: build helper, msbuild.exe, and dotnet were not found.' | Set-Content (Join-Path $outDir 'build.log')
            $buildCode = 9001
        }
    }
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $testCode = Run-Step 'tests' { & $dotnet.Source test '.\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj' --configuration $Configuration --no-restore }
} else {
    'NOT RUN: dotnet was not found.' | Set-Content (Join-Path $outDir 'tests.log')
    $testCode = 9002
}

$summary = @"
Ticket: $TicketId
HEAD: $(Get-Content (Join-Path $outDir 'head.txt') -Raw)
Branch: $(Get-Content (Join-Path $outDir 'branch.txt') -Raw)
Build exit: $buildCode
Test exit: $testCode
Evidence: $outDir

Interpretation:
- exit 0 = command passed
- exit 9001/9002 = NOT RUN because tool was unavailable
- any other non-zero = failed
"@
$summary | Set-Content (Join-Path $outDir 'SUMMARY.txt')
Write-Host $summary
if ($buildCode -ne 0 -or $testCode -ne 0) { exit 1 }
