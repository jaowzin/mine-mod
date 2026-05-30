$ErrorActionPreference = "Stop"

$dir = Join-Path $env:LOCALAPPDATA "MinecraftBedrockCTF"
New-Item -ItemType Directory -Path $dir -Force | Out-Null

$marker = Join-Path $dir "ALLOW_CTF.txt"

@"
CTF-ID=MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF
CTF-SHA256=06ca408f52e98204f93da63aee16bb6b751b0e3256bdcb6095d3dada1ba55c0e
# Set to 1 only after you add your own CTF-only signatures to src/module/user_patches.h
ENABLE_PATCHES=0
"@ | Set-Content -Path $marker -Encoding ASCII

Write-Host "Marker created:"
Write-Host $marker
Write-Host ""
Write-Host "Logs will be written under:"
Write-Host $dir
