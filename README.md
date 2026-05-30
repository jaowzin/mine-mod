# Minecraft Bedrock CTF Clean Module

Projeto C++ mínimo e auditável para o desafio:

- CTF-ID: `MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF`
- Build esperada: `MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe`
- Sem rede, sem updater, sem telemetria, sem persistência, sem mexer em conta/loja real.

## Componentes

### `vcruntime140_1.dll`

Proxy local. Ele exporta as mesmas 3 funções do `vcruntime140_1.dll` UWPDesktop x64 e encaminha tudo para:

```text
vcruntime140_2.dll
```

Ou seja: você deve colocar o runtime limpo original renomeado para `vcruntime140_2.dll` na pasta da build CTF.

O proxy só tenta carregar o módulo se:

```text
%LOCALAPPDATA%\MinecraftBedrockCTF\ALLOW_CTF.txt
```

contiver a assinatura do CTF.

### `ctf_patch_module.dll`

Módulo local do CTF. Ele já inclui o patch clonado do KG-UP-GOAT, mas só aplica quando o marcador do CTF existir e `ENABLE_PATCHES=1` estiver habilitado.

Ele só aplica patches se:

1. o marcador do CTF existir;
2. o processo estiver dentro da package/version allowlisted;
3. `ENABLE_PATCHES=1` estiver no marcador;
4. houver assinatura definida em `src/module/user_patches.h`;
5. cada assinatura bater exatamente uma vez.

Logs:

```text
%LOCALAPPDATA%\MinecraftBedrockCTF\loader.log
%LOCALAPPDATA%\MinecraftBedrockCTF\module.log
```

## Build local com Visual Studio

No "Developer PowerShell for VS 2022":

```powershell
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release -- /m
```

Saída esperada:

```text
build\Release\vcruntime140_1.dll
build\Release\ctf_patch_module.dll
```

## Build pelo GitHub Actions

Suba este repositório no GitHub e rode o workflow:

```text
Actions -> build-windows -> Run workflow
```

O artifact gerado será:

```text
mc_ctf_clean_module_release.zip
```

## Criar marcador local do CTF

```powershell
powershell -ExecutionPolicy Bypass -File scripts\create_marker.ps1
```

Por segurança, ele cria:

```text
ENABLE_PATCHES=0
```

Depois de revisar a assinatura CTF em `src/module/user_patches.h`, habilite:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\set_patches_enabled.ps1 -Enabled 1
```

## Instalação manual na build do CTF

Primeiro descubra a pasta:

```powershell
$pkg = Get-AppxPackage | Where-Object {
  $_.Name -like "*Minecraft*" -or $_.PackageFullName -like "*Minecraft*"
}

$pkg | Select-Object Name, PackageFullName, InstallLocation
```

Depois, usando a pasta retornada:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install_from_build.ps1 `
  -BuildDir ".\build" `
  -AppFolder "C:\Program Files\WindowsApps\MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe"
```

O script copia:

```text
vcruntime140_1.dll      -> pasta da build CTF
vcruntime140_2.dll      -> pasta da build CTF, vindo do VCLibs UWPDesktop x64 limpo
ctf_patch_module.dll    -> %APPDATA%\Minecraft Bedrock\mods
```

## Como adicionar patches

Edite:

```text
src/module/user_patches.h
```

A estrutura aceita pattern/mask, com `x` para byte exato e `?` para wildcard. Nesta versão, `user_patches.h` já contém o patch `xgameruntime.dll: isTrial\0 -> xzNope\0` identificado no KG.

Exemplo dummy, não funcional:

```cpp
static constexpr uint8_t kPattern[] = { 0xDE, 0xAD, 0xBE, 0xEF };
static constexpr uint8_t kPatch[]   = { 0x90, 0x90 };

inline const PatchSpec* GetUserPatches(std::size_t& count) {
    static const PatchSpec patches[] = {
        {
            "ctf-example",
            nullptr,
            kPattern,
            "xxxx",
            sizeof(kPattern),
            0,
            kPatch,
            sizeof(kPatch)
        },
    };

    count = _countof(patches);
    return patches;
}
```

Não deixe patches genéricos. Use assinaturas únicas da build do CTF.


## Patch clonado do KG-UP-GOAT

Análise estática do `KG-UP-GOAT.dll` mostrou que o patch efetivo é pequeno:

```text
módulo alvo: xgameruntime.dll
procura:     isTrial\0
substitui:   xzNope\0
```

Esse projeto já vem com essa assinatura em:

```text
src/module/user_patches.h
```

O módulo ainda exige o marcador do CTF e `ENABLE_PATCHES=1`; sem isso, ele só registra log e não altera memória.

## Remover

```powershell
powershell -ExecutionPolicy Bypass -File scripts\uninstall_manual.ps1 `
  -AppFolder "C:\Program Files\WindowsApps\MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe"
```

## Limites

Este projeto é para o laboratório autorizado do CTF. Não use em Minecraft oficial, Store real, pagamento real, conta Microsoft real ou servidor público.


## Interface Windows opcional

Este pacote também inclui `tools/CTFInstallerGui`, uma interface WinForms para instalar/remover o payload sem rodar instaladores de terceiros.

Ela não faz injeção genérica em processo aberto. Ela prepara os arquivos para a build allowlisted do CTF e o patch acontece em memória quando o jogo inicia e carrega o proxy.

Build:

```powershell
dotnet publish tools\CTFInstallerGui\CTFInstallerGui.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Uso:

1. feche a build CTF;
2. abra `CTFInstallerGui.exe` como administrador;
3. clique em `Detectar build`;
4. selecione a pasta `payload`/build contendo `vcruntime140_1.dll` e `ctf_patch_module.dll`;
5. clique em `Instalar/atualizar`;
6. abra a build CTF.

