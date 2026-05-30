param(
    [ValidateSet("0","1")]
    [string]$Enabled = "0"
)

$ErrorActionPreference = "Stop"

$marker = Join-Path $env:LOCALAPPDATA "MinecraftBedrockCTF\ALLOW_CTF.txt"
if (!(Test-Path $marker)) {
    throw "Marker not found. Run scripts\create_marker.ps1 first."
}

$text = Get-Content $marker -Raw
if ($text -match "ENABLE_PATCHES=\d") {
    $text = $text -replace "ENABLE_PATCHES=\d", "ENABLE_PATCHES=$Enabled"
} else {
    $text += "`r`nENABLE_PATCHES=$Enabled`r`n"
}

Set-Content -Path $marker -Value $text -Encoding ASCII
Write-Host "ENABLE_PATCHES=$Enabled written to $marker"
