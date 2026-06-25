#Requires -Version 5.1
<#
.SYNOPSIS
    Create IronCAD study case project: .ics parts + assembly + PDF/DWG naming-policy files.

.PARAMETER OutputFolder
    Target folder for project files (created if missing).

.PARAMETER ProjectName
    File name prefix. Default: IRONCASE

.PARAMETER Version
    Version string (used in file names). Default: 1.0

.PARAMETER Groups
    Comma-separated group names. Each group gets 2 sub-parts.
    Default: "Frame,Drive,Sensor"
    Custom sub-parts: "Frame:BasePlate+SidePanel,Drive:MotorMount+Gearbox"

.PARAMETER SkipIcs
    Skip IronCAD COM - only create PDF/DWG placeholder files.

.PARAMETER Force
    Overwrite existing files.

.EXAMPLE
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\IRONCASE"
    .\New-IronCadProject.ps1 -OutputFolder "D:\Cases\MOTOR" -ProjectName "MOTOR" -Groups "Body,Engine,Wheel" -Version "2.0"
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\QUICK" -SkipIcs
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\IRONCASE" -Force
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputFolder,
    [string]$ProjectName = "IRONCASE",
    [string]$Version     = "1.0",
    [string]$Groups      = "Frame,Drive,Sensor",
    [switch]$SkipIcs,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step { param([string]$m) Write-Host "  $m" -ForegroundColor Cyan }
function Write-OK   { param([string]$m) Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "  [!]  $m" -ForegroundColor Yellow }
function Write-Fail { param([string]$m) Write-Host "  [X]  $m" -ForegroundColor Red }

# ---------- Parse groups --------------------------------------------------
$defaultSubNames = @{
    Frame   = @("BasePlate",  "SidePanel")
    Drive   = @("MotorMount", "Gearbox")
    Sensor  = @("SensorMount","CoverLid")
    Body    = @("UpperBody",  "LowerBody")
    Engine  = @("Block",      "Head")
    Wheel   = @("Hub",        "Rim")
    Rotor   = @("Shaft",      "Blade")
    Housing = @("Shell",      "Bracket")
    Pump    = @("Impeller",   "Volute")
    Valve   = @("Stem",       "Seat")
}

$parsedGroups = [System.Collections.Generic.List[hashtable]]::new()
foreach ($token in ($Groups -split ",")) {
    $token = $token.Trim()
    if (-not $token) { continue }
    if ($token -match "^(.+):(.+)$") {
        $gName = $Matches[1].Trim()
        $subs  = @($Matches[2] -split "\+" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    } else {
        $gName = $token
        $subs  = if ($defaultSubNames.ContainsKey($gName)) { $defaultSubNames[$gName] } else { @("${gName}Part1","${gName}Part2") }
    }
    $parsedGroups.Add(@{ Name = $gName; Subs = $subs })
}
if ($parsedGroups.Count -eq 0) { Write-Error "No groups parsed."; exit 1 }

$partList  = [System.Collections.Generic.List[hashtable]]::new()
$globalSeq = 0
for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g = $parsedGroups[$gi]
    for ($si = 0; $si -lt $g.Subs.Count; $si++) {
        $globalSeq++
        $partList.Add(@{
            GroupIndex  = $gi + 1
            GroupName   = $g.Name
            ChildLetter = [char](65 + $si)
            ChildSeq    = $si + 1
            SubName     = $g.Subs[$si]
            GlobalSeq   = $globalSeq
        })
    }
}

# ---------- Print summary -------------------------------------------------
Write-Host ""
Write-Host "=== New-IronCadProject.ps1 ===" -ForegroundColor Magenta
Write-Host "  Project : $ProjectName  v$Version"
Write-Host "  Output  : $OutputFolder"
Write-Host "  Groups  : $(($parsedGroups | ForEach-Object { $_.Name }) -join ", ")"
Write-Host "  Parts   : $($partList.Count)"
Write-Host "  SkipIcs : $SkipIcs"
Write-Host ""

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$aras01Folder = Join-Path (Split-Path $OutputFolder -Parent) "ARAS01"
New-Item -ItemType Directory -Path $aras01Folder -Force | Out-Null

# ---------- STEP 1: PDF / DWG naming-policy files -------------------------
Write-Host "[1/3] Creating PDF/DWG naming-policy files..." -ForegroundColor Yellow

for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g        = $parsedGroups[$gi]
    $groupNum = "{0:D2}" -f ($gi + 1)

    $groupPdfName = "$groupNum. $($g.Name).pdf"
    $groupPdfPath = Join-Path $OutputFolder $groupPdfName
    if ($Force -or -not (Test-Path $groupPdfPath)) {
        [System.IO.File]::WriteAllText($groupPdfPath,
            "PDM Group: $($g.Name)`nProject: $ProjectName v$Version`nCreated: $(Get-Date -Format 'yyyy-MM-dd')`n")
        Write-OK $groupPdfName
    }

    $partsInGroup = @($partList | Where-Object { $_.GroupIndex -eq ($gi + 1) })
    foreach ($p in $partsInGroup) {
        $childSeqStr  = "{0:D2}" -f $p.ChildSeq
        $childPdfName = "$groupNum$($p.ChildLetter). $($g.Name)_${childSeqStr}_$($p.SubName).pdf"
        $childPdfPath = Join-Path $OutputFolder $childPdfName
        if ($Force -or -not (Test-Path $childPdfPath)) {
            [System.IO.File]::WriteAllText($childPdfPath,
                "PDM Part: $($p.SubName)`nGroup: $($g.Name)`nProject: $ProjectName v$Version`nCreated: $(Get-Date -Format 'yyyy-MM-dd')`n")
            Write-OK $childPdfName
        }
    }
}

$rootDwgName = "${ProjectName}_Ver${Version}.dwg"
$rootDwgPath = Join-Path $OutputFolder $rootDwgName
if ($Force -or -not (Test-Path $rootDwgPath)) {
    [System.IO.File]::WriteAllText($rootDwgPath, "Root drawing: $ProjectName v$Version")
    Write-OK $rootDwgName
}

$asmDwgName = "Assembly-${ProjectName}-Ver${Version}A.dwg"
$asmDwgPath = Join-Path $aras01Folder $asmDwgName
if ($Force -or -not (Test-Path $asmDwgPath)) {
    [System.IO.File]::WriteAllText($asmDwgPath, "Assembly DWG")
    Write-OK "ARAS01\$asmDwgName"
}

foreach ($p in $partList) {
    $seq         = "{0:D3}" -f $p.GlobalSeq
    $partDwgName = "${ProjectName}_Ver${Version}_${seq}.dwg"
    $partDwgPath = Join-Path $aras01Folder $partDwgName
    if ($Force -or -not (Test-Path $partDwgPath)) {
        [System.IO.File]::WriteAllText($partDwgPath, "Part DWG: $($p.SubName)")
        Write-OK "ARAS01\$partDwgName"
    }
}

# ---------- STEP 2: IronCAD .ics files via COM ----------------------------
Write-Host ""
if ($SkipIcs) {
    Write-Host "[2/3] Skipping .ics creation (-SkipIcs)." -ForegroundColor Yellow
} else {
    Write-Host "[2/3] Creating .ics files via IronCAD COM..." -ForegroundColor Yellow

    $stale = @(Get-Process | Where-Object { $_.Name -like "IRONCAD*" })
    if ($stale.Count -gt 0) {
        Write-Warn "Killing $($stale.Count) stale IronCAD process(es)..."
        $stale | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
    }

    function New-BoxStl {
        param([string]$Name, [double]$Sx, [double]$Sy, [double]$Sz)
        $hx = $Sx / 2; $hy = $Sy / 2; $hz = $Sz / 2
        $faces = @(
            @(0,0,1,   -$hx,-$hy,$hz,  $hx,-$hy,$hz,  $hx,$hy,$hz),
            @(0,0,1,   -$hx,-$hy,$hz,  $hx,$hy,$hz,  -$hx,$hy,$hz),
            @(0,0,-1,  -$hx,-$hy,-$hz,-$hx,$hy,-$hz,  $hx,$hy,-$hz),
            @(0,0,-1,  -$hx,-$hy,-$hz, $hx,$hy,-$hz,  $hx,-$hy,-$hz),
            @(1,0,0,    $hx,-$hy,-$hz, $hx,$hy,-$hz,  $hx,$hy,$hz),
            @(1,0,0,    $hx,-$hy,-$hz, $hx,$hy,$hz,   $hx,-$hy,$hz),
            @(-1,0,0,  -$hx,-$hy,-$hz,-$hx,-$hy,$hz, -$hx,$hy,$hz),
            @(-1,0,0,  -$hx,-$hy,-$hz,-$hx,$hy,$hz,  -$hx,$hy,-$hz),
            @(0,1,0,   -$hx,$hy,-$hz, -$hx,$hy,$hz,   $hx,$hy,$hz),
            @(0,1,0,   -$hx,$hy,-$hz,  $hx,$hy,$hz,   $hx,$hy,-$hz),
            @(0,-1,0,  -$hx,-$hy,-$hz, $hx,-$hy,-$hz, $hx,-$hy,$hz),
            @(0,-1,0,  -$hx,-$hy,-$hz, $hx,-$hy,$hz, -$hx,-$hy,$hz)
        )
        $sb = [System.Text.StringBuilder]::new()
        $null = $sb.AppendLine("solid $Name")
        foreach ($f in $faces) {
            $null = $sb.AppendLine(("  facet normal {0:F6} {1:F6} {2:F6}" -f $f[0],$f[1],$f[2]))
            $null = $sb.AppendLine("    outer loop")
            $null = $sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[3],$f[4],$f[5]))
            $null = $sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[6],$f[7],$f[8]))
            $null = $sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[9],$f[10],$f[11]))
            $null = $sb.AppendLine("    endloop")
            $null = $sb.AppendLine("  endfacet")
        }
        $null = $sb.AppendLine("endsolid $Name")
        return $sb.ToString()
    }

    $tempDir = Join-Path $env:TEMP "IcStl_$(New-Guid)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    $icApp = $null

    try {
        Write-Step "Starting IronCAD.Application COM..."
        $icApp = New-Object -ComObject IronCAD.Application
        $icApp.Visible = $true
        Start-Sleep -Seconds 3
        $icPid = (Get-Process | Where-Object { $_.Name -like "IRONCAD*" } | Select-Object -First 1).Id
        Write-OK "IronCAD started (PID: $icPid)"

        foreach ($p in $partList) {
            $seq     = "{0:D3}" -f $p.GlobalSeq
            $icsName = "${ProjectName}_Ver${Version}_${seq}.ics"
            $icsPath = Join-Path $OutputFolder $icsName

            if (-not $Force -and (Test-Path $icsPath)) {
                Write-Warn "$icsName exists - skipping (use -Force to overwrite)"
                continue
            }

            Write-Step "Creating $icsName ($($p.SubName))..."
            $scale   = 0.04 + ($p.GlobalSeq * 0.012)
            $stlText = New-BoxStl $p.SubName ($scale * 1.6) ($scale * 1.2) ($scale * 0.8)
            $stlPath = Join-Path $tempDir "$($p.SubName).stl"
            [System.IO.File]::WriteAllText($stlPath, $stlText)

            $page = $icApp.Pages.Add($null, $null)
            $null = $page.ImportFile($stlPath, $false)
            $page.SaveAs($icsPath)
            $page.Close()
            try { $icApp.Pages.Remove($page) } catch {}
            Write-OK $icsName
        }

        $asmIcsName = "Assembly-${ProjectName}-Ver${Version}A.ics"
        $asmIcsPath = Join-Path $OutputFolder $asmIcsName
        if ($Force -or -not (Test-Path $asmIcsPath)) {
            Write-Step "Creating $asmIcsName (assembly)..."
            $asmPage = $icApp.Pages.Add($null, $null)
            foreach ($p in $partList) {
                $seq      = "{0:D3}" -f $p.GlobalSeq
                $partPath = Join-Path $OutputFolder "${ProjectName}_Ver${Version}_${seq}.ics"
                if (Test-Path $partPath) {
                    try { $null = $asmPage.ImportFile($partPath, $true) }
                    catch { Write-Warn "Link $($p.SubName) failed: $_" }
                }
            }
            $asmPage.SaveAs($asmIcsPath)
            $asmPage.Close()
            try { $icApp.Pages.Remove($asmPage) } catch {}
            Write-OK $asmIcsName
        }
    }
    catch {
        Write-Fail "IronCAD COM error: $_"
        Write-Host ""
        Write-Host "  Tip: Kill IronCAD then retry, or use -SkipIcs to skip .ics creation." -ForegroundColor Yellow
        Write-Host "    Get-Process IRONCAD | Stop-Process -Force" -ForegroundColor Yellow
    }
    finally {
        if ($null -ne $icApp) {
            try { $icApp.Quit() } catch {}
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($icApp) | Out-Null
        }
        Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
    }
    Write-Host ""
}

# ---------- STEP 3: Structure manifest ------------------------------------
Write-Host "[3/3] Writing structure manifest..." -ForegroundColor Yellow

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("$ProjectName v$Version - Structure Manifest")
$lines.Add("Generated : $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$lines.Add("Output    : $OutputFolder")
$lines.Add("")
$lines.Add("ICS Structure:")
$lines.Add("  Assembly-${ProjectName}-Ver${Version}A.ics")
for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g            = $parsedGroups[$gi]
    $isLast       = ($gi -eq $parsedGroups.Count - 1)
    $gBranch      = if ($isLast) { "+--" } else { "+--" }
    $lines.Add("    $gBranch Scene_$($g.Name)")
    $partsInGroup = @($partList | Where-Object { $_.GroupIndex -eq ($gi + 1) })
    for ($pi = 0; $pi -lt $partsInGroup.Count; $pi++) {
        $p      = $partsInGroup[$pi]
        $seq    = "{0:D3}" -f $p.GlobalSeq
        $indent = "        "
        $branch = if ($pi -eq $partsInGroup.Count - 1) { "\\--" } else { "+--" }
        $lines.Add("$indent$branch ${ProjectName}_Ver${Version}_${seq}.ics ($($p.SubName))")
    }
}
$lines.Add("")
$lines.Add("Naming-policy files ($(Split-Path $OutputFolder -Leaf)):")
for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g        = $parsedGroups[$gi]
    $groupNum = "{0:D2}" -f ($gi + 1)
    $lines.Add("  $groupNum. $($g.Name).pdf")
    $partsInGroup = @($partList | Where-Object { $_.GroupIndex -eq ($gi + 1) })
    foreach ($p in $partsInGroup) {
        $childSeqStr = "{0:D2}" -f $p.ChildSeq
        $lines.Add("  $groupNum$($p.ChildLetter). $($g.Name)_${childSeqStr}_$($p.SubName).pdf")
    }
}
$lines.Add("")
$lines.Add("ARAS01 folder:")
$lines.Add("  Assembly-${ProjectName}-Ver${Version}A.dwg")
foreach ($p in $partList) {
    $seq = "{0:D3}" -f $p.GlobalSeq
    $lines.Add("  ${ProjectName}_Ver${Version}_${seq}.dwg ($($p.SubName))")
}

$manifestPath = Join-Path $OutputFolder "${ProjectName}-STRUCTURE.txt"
$lines.ToArray() | Set-Content -Path $manifestPath -Encoding UTF8
Write-OK (Split-Path $manifestPath -Leaf)

# ---------- Done ----------------------------------------------------------
Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "  IRONCASE : $OutputFolder"
Write-Host "  ARAS01   : $aras01Folder"
Write-Host ""
Write-Host "  Next: Open app > browse to OutputFolder > click Analyze" -ForegroundColor Cyan
Write-Host ""