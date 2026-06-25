#Requires -Version 5.1
<#
.SYNOPSIS
    Create IronCAD study case project: .ics parts + assembly + PDF/DWG naming-policy files.
.PARAMETER OutputFolder   Target folder (created if missing).
.PARAMETER ProjectName    File name prefix. Default: IRONCASE
.PARAMETER Version        Version string. Default: 1.0
.PARAMETER Groups         Comma-separated group names. Default: "Frame,Drive,Sensor"
                          Custom subs: "Frame:BasePlate+SidePanel,Drive:MotorMount+Gearbox"
.PARAMETER SkipIcs        Skip IronCAD COM - only create PDF/DWG placeholder files.
.PARAMETER Force          Overwrite existing files.
.EXAMPLE
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\IRONCASE"
    .\New-IronCadProject.ps1 -OutputFolder "D:\Cases\MOTOR" -ProjectName "MOTOR" -Groups "Body,Engine,Wheel" -Version "2.0"
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\QUICK" -SkipIcs
    .\New-IronCadProject.ps1 -OutputFolder "C:\Cases\IRONCASE" -Force
#>
param(
    [Parameter(Mandatory = $true)][string]$OutputFolder,
    [string]$ProjectName = "IRONCASE",
    [string]$Version     = "1.0",
    [string]$Groups      = "Frame,Drive,Sensor",
    [switch]$SkipIcs,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step { param([string]$m) Write-Host "  $m" -ForegroundColor Cyan }
function Write-OK   { param([string]$m) Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "  [!]  $m" -ForegroundColor Yellow }
function Write-Fail { param([string]$m) Write-Host "  [X]  $m" -ForegroundColor Red }

# ---- Parse groups ----
$defaultSubs = @{
    Frame   = @("BasePlate","SidePanel"); Drive  = @("MotorMount","Gearbox")
    Sensor  = @("SensorMount","CoverLid"); Body  = @("UpperBody","LowerBody")
    Engine  = @("Block","Head");           Wheel = @("Hub","Rim")
    Rotor   = @("Shaft","Blade");          Pump  = @("Impeller","Volute")
    Valve   = @("Stem","Seat");            Housing=@("Shell","Bracket")
}

$parsedGroups = [System.Collections.Generic.List[hashtable]]::new()
foreach ($token in ($Groups -split ",")) {
    $token = $token.Trim(); if (-not $token) { continue }
    if ($token -match "^(.+):(.+)$") {
        $gName = $Matches[1].Trim()
        $subs  = @($Matches[2] -split "\+" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    } else {
        $gName = $token
        $subs  = if ($defaultSubs.ContainsKey($gName)) { $defaultSubs[$gName] } else { @("${gName}Part1","${gName}Part2") }
    }
    $parsedGroups.Add(@{ Name = $gName; Subs = $subs })
}
if ($parsedGroups.Count -eq 0) { Write-Error "No groups parsed."; exit 1 }

$partList = [System.Collections.Generic.List[hashtable]]::new(); $gs = 0
for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g = $parsedGroups[$gi]
    for ($si = 0; $si -lt $g.Subs.Count; $si++) {
        $gs++
        $partList.Add(@{ GroupIndex=$gi+1; GroupName=$g.Name; ChildLetter=[char](65+$si)
                         ChildSeq=$si+1; SubName=$g.Subs[$si]; GlobalSeq=$gs })
    }
}

# ---- Summary ----
Write-Host ""
Write-Host "=== New-IronCadProject.ps1 ===" -ForegroundColor Magenta
Write-Host "  Project : $ProjectName  v$Version"
Write-Host "  Output  : $OutputFolder"
Write-Host "  Groups  : $(($parsedGroups | ForEach-Object { $_.Name }) -join ", ")"
Write-Host "  Parts   : $($partList.Count)  |  SkipIcs: $SkipIcs"
Write-Host ""

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$aras01 = Join-Path (Split-Path $OutputFolder -Parent) "ARAS01"
New-Item -ItemType Directory -Path $aras01 -Force | Out-Null

# ======== STEP 1: PDF / DWG naming-policy files ========
Write-Host "[1/3] Creating PDF/DWG naming-policy files..." -ForegroundColor Yellow
for ($gi = 0; $gi -lt $parsedGroups.Count; $gi++) {
    $g = $parsedGroups[$gi]; $gn = "{0:D2}" -f ($gi+1)
    $gPdf = Join-Path $OutputFolder "$gn. $($g.Name).pdf"
    if ($Force -or -not (Test-Path $gPdf)) {
        [System.IO.File]::WriteAllText($gPdf, "PDM Group: $($g.Name)`nProject: $ProjectName v$Version`nCreated: $(Get-Date -Format yyyy-MM-dd)`n")
        Write-OK "$gn. $($g.Name).pdf"
    }
    foreach ($p in @($partList | Where-Object { $_.GroupIndex -eq ($gi+1) })) {
        $cs = "{0:D2}" -f $p.ChildSeq
        $cPdf = Join-Path $OutputFolder "$gn$($p.ChildLetter). $($g.Name)_${cs}_$($p.SubName).pdf"
        if ($Force -or -not (Test-Path $cPdf)) {
            [System.IO.File]::WriteAllText($cPdf, "PDM Part: $($p.SubName)`nGroup: $($g.Name)`nProject: $ProjectName v$Version`nCreated: $(Get-Date -Format yyyy-MM-dd)`n")
            Write-OK "$gn$($p.ChildLetter). $($g.Name)_${cs}_$($p.SubName).pdf"
        }
    }
}
$rdwg = Join-Path $OutputFolder "${ProjectName}_Ver${Version}.dwg"
if ($Force -or -not (Test-Path $rdwg)) { [System.IO.File]::WriteAllText($rdwg,"Root: $ProjectName v$Version"); Write-OK "${ProjectName}_Ver${Version}.dwg" }
$adwg = Join-Path $aras01 "Assembly-${ProjectName}-Ver${Version}A.dwg"
if ($Force -or -not (Test-Path $adwg)) { [System.IO.File]::WriteAllText($adwg,"Assembly DWG"); Write-OK "ARAS01\Assembly-${ProjectName}-Ver${Version}A.dwg" }
foreach ($p in $partList) {
    $seq = "{0:D3}" -f $p.GlobalSeq
    $pd = Join-Path $aras01 "${ProjectName}_Ver${Version}_${seq}.dwg"
    if ($Force -or -not (Test-Path $pd)) { [System.IO.File]::WriteAllText($pd,"Part: $($p.SubName)"); Write-OK "ARAS01\${ProjectName}_Ver${Version}_${seq}.dwg" }
}

# ======== STEP 2: IronCAD .ics via COM (ImportFile + STL) ========
Write-Host ""
if ($SkipIcs) {
    Write-Host "[2/3] Skipping .ics creation (-SkipIcs)." -ForegroundColor Yellow
} else {
    Write-Host "[2/3] Creating .ics files via IronCAD COM (ImportFile/STL method)..." -ForegroundColor Yellow
    $stale = @(Get-Process | Where-Object { $_.Name -like "IRONCAD*" })
    if ($stale.Count -gt 0) {
        Write-Warn "Killing $($stale.Count) stale IronCAD process(es)..."
        $stale | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 3
    }

    function New-BoxStl {
        param([string]$Name,[double]$Sx,[double]$Sy,[double]$Sz)
        $hx=$Sx/2; $hy=$Sy/2; $hz=$Sz/2
        $t=@(
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
        $sb=[System.Text.StringBuilder]::new()
        $null=$sb.AppendLine("solid $Name")
        foreach ($f in $t) {
            $null=$sb.AppendLine(("  facet normal {0:F6} {1:F6} {2:F6}" -f $f[0],$f[1],$f[2]))
            $null=$sb.AppendLine("    outer loop")
            $null=$sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[3],$f[4],$f[5]))
            $null=$sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[6],$f[7],$f[8]))
            $null=$sb.AppendLine(("      vertex {0:F6} {1:F6} {2:F6}" -f $f[9],$f[10],$f[11]))
            $null=$sb.AppendLine("    endloop"); $null=$sb.AppendLine("  endfacet")
        }
        $null=$sb.AppendLine("endsolid $Name"); return $sb.ToString()
    }

    $tmpDir = Join-Path $env:TEMP "IcStl_$(New-Guid)"
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
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
                Write-Warn "$icsName exists - skipping (-Force to overwrite)"; continue
            }
            Write-Step "Creating $icsName ($($p.SubName))..."
            $scale   = 0.04 + ($p.GlobalSeq * 0.012)
            $stlTxt  = New-BoxStl $p.SubName ($scale*1.6) ($scale*1.2) ($scale*0.8)
            $stlPath = Join-Path $tmpDir "$($p.SubName).stl"
            [System.IO.File]::WriteAllText($stlPath, $stlTxt)

            $page = $icApp.Pages.Add($null, $null)
            $null = $page.ImportFile($stlPath, $false)   # embed STL geometry
            $page.SaveAs($icsPath)
            try { $page.Close() } catch {}
            try { $icApp.Pages.Remove($page) } catch {}
            Write-OK $icsName
        }

        $asmName = "Assembly-${ProjectName}-Ver${Version}A.ics"
        $asmPath = Join-Path $OutputFolder $asmName
        if ($Force -or -not (Test-Path $asmPath)) {
            Write-Step "Creating $asmName (assembly linking all parts)..."
            $asmPage = $icApp.Pages.Add($null, $null)
            foreach ($p in $partList) {
                $seq  = "{0:D3}" -f $p.GlobalSeq
                $pth  = Join-Path $OutputFolder "${ProjectName}_Ver${Version}_${seq}.ics"
                if (Test-Path $pth) {
                    try { $null = $asmPage.ImportFile($pth, $true) }   # link
                    catch { Write-Warn "Link $($p.SubName) failed: $_" }
                }
            }
            $asmPage.SaveAs($asmPath)
            try { $asmPage.Close() } catch {}
            try { $icApp.Pages.Remove($asmPage) } catch {}
            Write-OK $asmName
        }
    }
    catch {
        Write-Fail "IronCAD COM error: $_"
        Write-Host "  Tip: Kill IronCAD then retry, or add -SkipIcs to skip." -ForegroundColor Yellow
        Write-Host "    Get-Process IRONCAD | Stop-Process -Force" -ForegroundColor Yellow
    }
    finally {
        if ($null -ne $icApp) {
            try { $icApp.Quit() } catch {}
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($icApp) | Out-Null
        }
        Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
    }
    Write-Host ""
}

# ======== STEP 3: Structure manifest ========
Write-Host "[3/3] Writing structure manifest..." -ForegroundColor Yellow
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("$ProjectName v$Version - Structure Manifest")
$lines.Add("Generated : $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$lines.Add("Output    : $OutputFolder")
$lines.Add("")
$lines.Add("ICS Structure:")
$lines.Add("  Assembly-${ProjectName}-Ver${Version}A.ics")
for ($gi=0;$gi-lt$parsedGroups.Count;$gi++) {
    $g=$parsedGroups[$gi]
    $lines.Add("    +-- Scene_$($g.Name)")
    foreach ($p in @($partList | Where-Object { $_.GroupIndex -eq ($gi+1) })) {
        $seq="{0:D3}" -f $p.GlobalSeq
        $lines.Add("        +-- ${ProjectName}_Ver${Version}_${seq}.ics ($($p.SubName))")
    }
}
$lines.Add(""); $lines.Add("Naming-policy files:")
for ($gi=0;$gi-lt$parsedGroups.Count;$gi++) {
    $g=$parsedGroups[$gi]; $gn="{0:D2}" -f ($gi+1)
    $lines.Add("  $gn. $($g.Name).pdf")
    foreach ($p in @($partList | Where-Object { $_.GroupIndex -eq ($gi+1) })) {
        $cs="{0:D2}" -f $p.ChildSeq
        $lines.Add("  $gn$($p.ChildLetter). $($g.Name)_${cs}_$($p.SubName).pdf")
    }
}
$lines.Add(""); $lines.Add("ARAS01 folder:")
$lines.Add("  Assembly-${ProjectName}-Ver${Version}A.dwg")
foreach ($p in $partList) {
    $seq="{0:D3}" -f $p.GlobalSeq
    $lines.Add("  ${ProjectName}_Ver${Version}_${seq}.dwg ($($p.SubName))")
}
$mf = Join-Path $OutputFolder "${ProjectName}-STRUCTURE.txt"
$lines.ToArray() | Set-Content -Path $mf -Encoding UTF8
Write-OK (Split-Path $mf -Leaf)

Write-Host ""; Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "  IRONCASE : $OutputFolder"; Write-Host "  ARAS01   : $aras01"
Write-Host "  Next: Open app > browse to OutputFolder > Analyze" -ForegroundColor Cyan
Write-Host ""
