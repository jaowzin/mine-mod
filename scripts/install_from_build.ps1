param(
    [Parameter(Mandatory=$true)]
    [string]$BuildDir,

    [Parameter(Mandatory=$true)]
    [string]$AppFolder,

    [string]$ModuleDestination = "$env:APPDATA\Minecraft Bedrock\mods"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $BuildDir)) {
    throw "BuildDir not found: $BuildDir"
}
if (!(Test-Path $AppFolder)) {
    throw "AppFolder not found: $AppFolder"
}

# Narrow guard for the CTF package/version shown in PowerShell.
if ($AppFolder -notmatch "MINECRAFTUWP_1\.26\.2101\.0_x64__8wekyb3d8bbwe") {
    throw "Refusing to install: AppFolder does not look like the allowed CTF package/version."
}

$proxy = Get-ChildItem -Path $BuildDir -Recurse -Filter "vcruntime140_1.dll" | Select-Object -First 1
$module = Get-ChildItem -Path $BuildDir -Recurse -Filter "ctf_patch_module.dll" | Select-Object -First 1

if (!$proxy) { throw "Built proxy vcruntime140_1.dll not found under $BuildDir" }
if (!$module) { throw "Built ctf_patch_module.dll not found under $BuildDir" }

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"

function Backup-IfExists($Path) {
    if (Test-Path $Path) {
        Copy-Item -LiteralPath $Path -Destination "$Path.bak.$stamp" -Force
        Write-Host "Backed up: $Path"
    }
}

# Copy the clean UWPDesktop x64 runtime as vcruntime140_2.dll.
$uwpDll = Get-ChildItem "C:\Program Files\WindowsApps" -Recurse -Filter "vcruntime140_1.dll" -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -like "*uwpdesktop*" -and $_.FullName -like "*x64*" } |
  Select-Object -First 1

if (!$uwpDll) {
    throw "Could not locate the clean UWPDesktop x64 vcruntime140_1.dll."
}

$dstProxy = Join-Path $AppFolder "vcruntime140_1.dll"
$dstRuntime = Join-Path $AppFolder "vcruntime140_2.dll"

Backup-IfExists $dstProxy
Backup-IfExists $dstRuntime

Copy-Item -LiteralPath $proxy.FullName -Destination $dstProxy -Force
Copy-Item -LiteralPath $uwpDll.FullName -Destination $dstRuntime -Force

New-Item -ItemType Directory -Path $ModuleDestination -Force | Out-Null
$dstModule = Join-Path $ModuleDestination "ctf_patch_module.dll"
Backup-IfExists $dstModule
Copy-Item -LiteralPath $module.FullName -Destination $dstModule -Force

Write-Host "Installed CTF proxy:"
Write-Host "  $dstProxy"
Write-Host "Installed clean forwarded runtime:"
Write-Host "  $dstRuntime"
Write-Host "Installed CTF module:"
Write-Host "  $dstModule"
Write-Host ""
Write-Host "Create marker with:"
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\create_marker.ps1"
Write-Host ""
Write-Host "Enable patches only after editing src\module\user_patches.h:"
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\set_patches_enabled.ps1 -Enabled 1"
