# CTFInstallerGui

Interface Windows para preparar a build personalizada do CTF.

Ela não é um injector genérico. O fluxo é:

1. detectar somente o pacote allowlisted `MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe`;
2. detectar automaticamente a pasta `payload` do artifact;
3. copiar o proxy `vcruntime140_1.dll` para a pasta da build;
4. copiar o runtime limpo UWPDesktop como `vcruntime140_2.dll`;
5. copiar `ctf_patch_module.dll` para `%APPDATA%\Minecraft Bedrock\mods`;
6. criar `%LOCALAPPDATA%\MinecraftBedrockCTF\ALLOW_CTF.txt`;
7. iniciar a build CTF manualmente ou pelo botão.

## Payload automático

No artifact do GitHub Actions, mantenha a estrutura assim:

```text
mc_ctf_clean_gui_release/
├─ gui/
│  └─ CTFInstallerGui.exe
└─ payload/
   ├─ vcruntime140_1.dll
   └─ ctf_patch_module.dll
```

A GUI detecta `payload` automaticamente como pasta irmã de `gui`. O botão **Manual** fica apenas como fallback.

## O jogo precisa estar aberto?

Não para instalar. Feche o jogo antes de instalar/remover.

Depois da instalação, abra o jogo. O proxy será carregado no início do processo e o módulo aplica o patch em memória quando `xgameruntime.dll` estiver carregada.

## Build

```powershell
dotnet publish tools\CTFInstallerGui\CTFInstallerGui.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

O executável pede administrador porque a pasta `WindowsApps` é protegida.
