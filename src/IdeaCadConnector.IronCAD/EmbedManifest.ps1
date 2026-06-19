param(
    [Parameter(Mandatory=$true)] [string]$DllPath,
    [Parameter(Mandatory=$true)] [string]$ManifestPath,
    [Parameter(Mandatory=$true)] [int]$ResourceId
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DllPath))      { throw "DLL not found: $DllPath" }
if (-not (Test-Path $ManifestPath)) { throw "Manifest not found: $ManifestPath" }

Add-Type -Namespace Win32 -Name Res -MemberDefinition @"
[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Unicode)]
public static extern IntPtr BeginUpdateResource(string fileName, bool deleteExistingResources);
[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Unicode)]
public static extern bool UpdateResource(IntPtr hUpdate, IntPtr type, IntPtr name, ushort lang, byte[] data, uint dataSize);
[DllImport("kernel32", SetLastError=true)]
public static extern bool EndUpdateResource(IntPtr hUpdate, bool discard);
"@

$manifestBytes = [System.IO.File]::ReadAllBytes($ManifestPath)
$RT_MANIFEST = [IntPtr]24
$NEUTRAL_LANG = [uint16]0

$h = [Win32.Res]::BeginUpdateResource($DllPath, $false)
if ($h -eq [IntPtr]::Zero) {
    throw "BeginUpdateResource failed (Win32 error $([System.Runtime.InteropServices.Marshal]::GetLastWin32Error())) for: $DllPath"
}

try {
    $ok = [Win32.Res]::UpdateResource($h, $RT_MANIFEST, [IntPtr]$ResourceId, $NEUTRAL_LANG, $manifestBytes, [uint32]$manifestBytes.Length)
    if (-not $ok) {
        $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        [Win32.Res]::EndUpdateResource($h, $true) | Out-Null
        throw "UpdateResource failed (Win32 error $err)"
    }

    $ok = [Win32.Res]::EndUpdateResource($h, $false)
    if (-not $ok) {
        $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "EndUpdateResource failed (Win32 error $err)"
    }
}
catch {
    throw
}

Write-Host "Embedded RT_MANIFEST id=$ResourceId ($($manifestBytes.Length) bytes) into $DllPath"
