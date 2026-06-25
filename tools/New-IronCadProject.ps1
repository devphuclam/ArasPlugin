#Requires -Version 5.1
<#
.SYNOPSIS
    Create IronCAD study case project: .ics parts + assembly + PDF/DWG naming-policy files.
.PARAMETER OutputFolder   Target folder (created if missing).
.PARAMETER ProjectName    File name prefix. Default: IRONCASE
.PARAMETER Version        Version string. Default: 1.0
.PARAMETER Groups         Comma-separated group names. Default: "Frame,Drive,Sensor"
                          Custom subs: "Frame:BasePlate+SidePanel,Drive:MotorMount+Gearbox"
.PARAMETER SeedPartPath   Existing green/native IronCAD part used as the seed for all generated .ics files.
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
    [string]$SeedPartPath = "C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\Stapler.ics",
    [switch]$SkipIcs,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step { param([string]$m) Write-Host "  $m" -ForegroundColor Cyan }
function Write-OK   { param([string]$m) Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "  [!]  $m" -ForegroundColor Yellow }
function Write-Fail { param([string]$m) Write-Host "  [X]  $m" -ForegroundColor Red }

$script:IronCadInteropPath = "C:\Program Files\IronCAD\2025\ICAPI\Samples\C#\References\interop.ICApiIronCAD.dll"
$script:IronCadNativeBridgeReady = $false

function Ensure-IronCadNativeBridge {
    if ($script:IronCadNativeBridgeReady) { return }
    if (-not (Test-Path $script:IronCadInteropPath)) {
        throw "ICAPI interop not found: $script:IronCadInteropPath"
    }

    Add-Type -Path $script:IronCadInteropPath
    $type = 'IronCadNativeBridge' -as [type]
    if ($null -eq $type) {
        $bridgeCode = @"
using System;
using System.Reflection;
using interop.ICApiIronCAD;

public static class IronCadNativeBridge
{
    private static double[] P3(double x, double y, double z)
    {
        return new[] { x, y, z };
    }

    private static double[] P2(double x, double y)
    {
        return new[] { x, y };
    }

    public static void SaveScene(object sceneObj, string filePath)
    {
        var scene = (IZSceneDoc)sceneObj;
        try
        {
            scene.SaveAs(filePath, eZLinksSaveOptions.Z_LINKS_IGNORE, true);
        }
        catch
        {
            sceneObj.GetType().InvokeMember(
                "SaveAs",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                null,
                sceneObj,
                new object[] { filePath });
        }
    }

    public static object CreateSceneDocument(object appObj)
    {
        var app = (IZBaseApp)appObj;
        IZDoc doc = app.CreateNewDoc(eZDocType.Z_SCENE, false, true, string.Empty, true);
        if (doc == null)
            throw new InvalidOperationException("CreateNewDoc returned null.");
        return doc;
    }

    public static void CloseDocument(object docObj)
    {
        var doc = (IZDoc)docObj;
        try
        {
            doc.Close();
        }
        catch
        {
        }
    }

    public static void CreatePart(object sceneObj, int seq, string partName, string partNumber)
    {
        var scene = (IZSceneDoc)sceneObj;
        IZPart part = CreateNativePart(scene, seq);
        if (part == null)
            throw new InvalidOperationException("Native part creation returned null.");

        try { ((IZElement)part).Name = partName; } catch { }
        try { part.BOMPartNumber = partNumber; } catch { }
        try { part.BOMDescription = partName; } catch { }
        try { part.Update(); } catch { }
        try { scene.Update(); } catch { }
    }

    public static void AddLinkedDocument(object sceneObj, string filePath, string shapeName)
    {
        var scene = (IZSceneDoc)sceneObj;
        object added = null;
        try
        {
            added = scene.GetType().InvokeMember(
                "Shapes",
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                null,
                scene,
                null);
            if (added != null)
            {
                var linked = added.GetType().InvokeMember(
                    "Add",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    added,
                    new object[] { filePath });
                var elem = linked as IZElement;
                if (elem != null)
                {
                    try { elem.Name = shapeName; } catch { }
                }
                return;
            }
        }
        catch
        {
        }

        scene.GetType().InvokeMember(
            "ImportFile",
            BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
            null,
            scene,
            new object[] { filePath, true });
    }

    private static IZPart CreateNativePart(IZSceneDoc scene, int seq)
    {
        int recipe = (seq - 1) % 5;
        double n = seq;
        switch (recipe)
        {
            case 0:
            {
                double sx = 0.08 + (n * 0.015);
                double sy = 0.05 + (n * 0.010);
                double sz = 0.025 + (n * 0.008);
                return scene.CreateBlockPart(P3(-sx / 2, -sy / 2, -sz / 2), P3(sx / 2, sy / 2, sz / 2));
            }
            case 1:
            {
                double radius = 0.02 + (n * 0.004);
                double height = 0.05 + (n * 0.010);
                return scene.CreateCylinderPart(radius, height, P3(0, 0, 0), P3(0, 0, 1));
            }
            case 2:
            {
                double radius = 0.025 + (n * 0.003);
                return scene.CreateSpherePart(radius, P3(0, 0, 0));
            }
            case 3:
            {
                double radius = 0.03 + (n * 0.004);
                double height = 0.06 + (n * 0.009);
                return scene.CreateConePart(radius, height, 0.30, P3(0, 0, 0), P3(0, 0, 1));
            }
            default:
                return CreateBracket(scene, n);
        }
    }

    private static IZPart CreateBracket(IZSceneDoc scene, double n)
    {
        double width = 0.10 + (n * 0.010);
        double height = 0.08 + (n * 0.008);
        double thick = 0.015 + (n * 0.003);
        double[][] points =
        {
            new[] { -width / 2, -height / 2 },
            new[] {  width / 2, -height / 2 },
            new[] {  width / 2, -height / 2 + thick },
            new[] { -width / 2 + thick, -height / 2 + thick },
            new[] { -width / 2 + thick,  height / 2 },
            new[] { -width / 2,  height / 2 }
        };

        IZProfile profile = scene.CreateProfile();
        if (profile == null)
            throw new InvalidOperationException("CreateProfile returned null.");

        for (int i = 0; i < points.Length; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Length];
            profile.CreateLine(P2(start[0], start[1]), P2(end[0], end[1]), i + 1);
        }

        IZPart part = scene.CreatePart();
        if (part == null)
            throw new InvalidOperationException("CreatePart returned null.");

        var features = (IZPartFeatureMgr)part;
        features.CreateExtrudeFeature(
            eZOperationType.Z_UNITE,
            false,
            thick * 2.5,
            0.0,
            0.0,
            profile,
            eZFeatureProfileRelType.Z_FEATURE_PROFILE_ABSORB);

        try { part.Update(); } catch { }
        return part;
    }
}
"@
        Add-Type -TypeDefinition $bridgeCode -Language CSharp -ReferencedAssemblies $script:IronCadInteropPath | Out-Null
    }
    $script:IronCadNativeBridgeReady = $true
}

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

# ======== STEP 2: IronCAD .ics from seed part + best-effort assembly ========
Write-Host ""
if ($SkipIcs) {
    Write-Host "[2/3] Skipping .ics creation (-SkipIcs)." -ForegroundColor Yellow
} else {
    Write-Host "[2/3] Creating .ics files from seed part..." -ForegroundColor Yellow

    if (-not (Test-Path $SeedPartPath)) {
        throw "SeedPartPath not found: $SeedPartPath"
    }

    foreach ($p in $partList) {
        $seq     = "{0:D3}" -f $p.GlobalSeq
        $icsName = "${ProjectName}_Ver${Version}_${seq}.ics"
        $icsPath = Join-Path $OutputFolder $icsName
        if (-not $Force -and (Test-Path $icsPath)) {
            Write-Warn "$icsName exists - skipping (-Force to overwrite)"
            continue
        }

        Write-Step "Copying seed part -> $icsName ($($p.SubName))..."
        Copy-Item -LiteralPath $SeedPartPath -Destination $icsPath -Force
        Write-OK $icsName
    }

    $asmName = "Assembly-${ProjectName}-Ver${Version}A.ics"
    $asmPath = Join-Path $OutputFolder $asmName
    $icApp = $null
    try {
        $stale = @(Get-Process | Where-Object { $_.Name -like "IRONCAD*" })
        if ($stale.Count -gt 0) {
            Write-Warn "Killing $($stale.Count) stale IronCAD process(es)..."
            $stale | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Seconds 3
        }

        Write-Step "Starting IronCAD.Application COM for assembly..."
        $icApp = New-Object -ComObject IronCAD.Application
        $icApp.Visible = $true
        Start-Sleep -Seconds 3
        $icPid = (Get-Process | Where-Object { $_.Name -like "IRONCAD*" } | Select-Object -First 1).Id
        Write-OK "IronCAD started (PID: $icPid)"

        if ($Force -or -not (Test-Path $asmPath)) {
            Write-Step "Creating $asmName (best-effort linked assembly)..."
            $asmPage = $icApp.Pages.Add($null, $null)
            foreach ($p in $partList) {
                $seq = "{0:D3}" -f $p.GlobalSeq
                $pth = Join-Path $OutputFolder "${ProjectName}_Ver${Version}_${seq}.ics"
                if (Test-Path $pth) {
                    try { $null = $asmPage.ImportFile($pth, $true) }
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
        Write-Warn "Assembly creation failed. Falling back to seed copy for root assembly."
        Copy-Item -LiteralPath $SeedPartPath -Destination $asmPath -Force
        Write-OK "$asmName (seed fallback)"
    }
    finally {
        if ($null -ne $icApp) {
            try { $icApp.Quit() } catch {}
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($icApp) | Out-Null
        }
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
