param(
    [Parameter(Mandatory=$true)]
    [string]$BuildDir,

    # Optional now: when omitted, the script resolves the installed Minecraft package dynamically.
    [string]$AppFolder = "",

    [string]$PackageName = "Microsoft.MinecraftUWP",
    [string]$PackageFamilySuffix = "8wekyb3d8bbwe",
    [string]$Architecture = "x64",

    [string]$ModuleDestination = "$env:APPDATA\Minecraft Bedrock\mods"
)

$ErrorActionPreference = "Stop"

function Get-AllowedCtfPackage {
    $escapedName = [regex]::Escape($PackageName)
    $escapedArch = [regex]::Escape($Architecture)
    $escapedSuffix = [regex]::Escape($PackageFamilySuffix)
    $packagePattern = "^$escapedName`_[^_]+_$escapedArch`__$escapedSuffix$"

    $pkgs = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue |
        Where-Object {
            $_.PackageFullName -match $packagePattern -and
            $_.InstallLocation -and
            (Test-Path $_.InstallLocation)
        } |
        Sort-Object Version -Descending

    return $pkgs | Select-Object -First 1
}

function Test-AllowedCtfAppFolder([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if (!(Test-Path $Path)) { return $false }

    $escapedName = [regex]::Escape($PackageName)
    $escapedArch = [regex]::Escape($Architecture)
    $escapedSuffix = [regex]::Escape($PackageFamilySuffix)
    $folderPattern = "(?i)[\\/]$escapedName`_[^\\/]+_$escapedArch`__$escapedSuffix$"

    return $Path -match $folderPattern
}

if (!(Test-Path $BuildDir)) {
    throw "BuildDir not found: $BuildDir"
}

if ([string]::IsNullOrWhiteSpace($AppFolder)) {
    $pkg = Get-AllowedCtfPackage
    if (!$pkg) {
        throw "Could not find an installed CTF Minecraft package matching $PackageName/*/$Architecture/$PackageFamilySuffix."
    }

    $AppFolder = $pkg.InstallLocation
    Write-Host "Detected installed package: $($pkg.PackageFullName)"
    Write-Host "Detected AppFolder: $AppFolder"
}

if (!(Test-AllowedCtfAppFolder $AppFolder)) {
    throw "Refusing to install: AppFolder does not match the allowed CTF package family/architecture. Path: $AppFolder"
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
  Where-Object { $_.FullName -like "*uwpdesktop*" -and $_.FullName -like "*$Architecture*" } |
  Select-Object -First 1

if (!$uwpDll) {
    throw "Could not locate the clean UWPDesktop $Architecture vcruntime140_1.dll."
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
