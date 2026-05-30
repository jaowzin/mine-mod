param(
    [Parameter(Mandatory=$true)]
    [string]$AppFolder,

    [string]$ModuleDestination = "$env:APPDATA\Minecraft Bedrock\mods"
)

$ErrorActionPreference = "Stop"

if ($AppFolder -notmatch "MINECRAFTUWP_1\.26\.2101\.0_x64__8wekyb3d8bbwe") {
    throw "Refusing to uninstall: AppFolder does not look like the allowed CTF package/version."
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
