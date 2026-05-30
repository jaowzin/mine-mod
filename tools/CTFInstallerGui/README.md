# CTFInstallerGui

Interface Windows para preparar a build personalizada do CTF.

Esta versão corrige o layout cortado da tela anterior e já usa ícone próprio do app.

## Fluxo

Ela não é um injector genérico. O fluxo é:

1. detectar somente o pacote allowlisted `MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe`;
2. detectar automaticamente a pasta `payload` do artifact;
3. criar/atualizar o marcador do CTF;
4. copiar o proxy `vcruntime140_1.dll` para a pasta da build;
5. copiar o runtime limpo UWPDesktop como `vcruntime140_2.dll`;
6. copiar `ctf_patch_module.dll` para `%APPDATA%\Minecraft Bedrock\mods`;
7. abrir a build pelo botão **Iniciar jogo**.

## Layout corrigido

A GUI agora usa:

- largura mínima maior;
- painel de ações com linhas fixas, sem cortar os botões;
- caminhos em campos read-only;
- payload automático por padrão;
- status visual para Build, Payload, Marcador, Instalação e Admin;
- ícone próprio embutido no `.exe`.

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

Abra o app por `gui\CTFInstallerGui.exe`. Ele detecta `..\payload` sozinho. O botão **Manual** fica apenas como fallback.

## O jogo precisa estar aberto?

Não para instalar. Feche o jogo antes de instalar/remover.

Depois da instalação, abra o jogo. O proxy será carregado no início do processo e o módulo aplica o patch em memória quando `xgameruntime.dll` estiver carregada.

## Build local

```powershell
dotnet publish tools\CTFInstallerGui\CTFInstallerGui.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o release\gui
```

O executável pede administrador porque a pasta `WindowsApps` é protegida.
