param(
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
    throw "Refusing to uninstall: AppFolder does not match the allowed CTF package family/architecture. Path: $AppFolder"
}

$proxy = Join-Path $AppFolder "vcruntime140_1.dll"
$runtime = Join-Path $AppFolder "vcruntime140_2.dll"
$module = Join-Path $ModuleDestination "ctf_patch_module.dll"

foreach ($p in @($proxy, $runtime, $module)) {
    if (Test-Path $p) {
        Remove-Item -LiteralPath $p -Force
        Write-Host "Removed: $p"
    }
}

Write-Host "If you have .bak.* files from previous installs, restore/remove them manually after verifying the CTF rules."
