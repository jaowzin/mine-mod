# Minecraft Bedrock CTF Clean Module

Projeto C++ mínimo e auditável para o desafio autorizado:

- CTF-ID: `MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF`
- Package family esperada: `Microsoft.MinecraftUWP_8wekyb3d8bbwe`
- Arquitetura esperada: `x64`
- Versão: detectada automaticamente a partir do pacote instalado no computador do usuário.
- Sem rede, sem updater, sem telemetria, sem persistência, sem mexer em conta/loja real.

## O que mudou nesta branch

A instalação e os guards em runtime não dependem mais de uma versão fixa como:

```text
MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe
```

Agora o projeto aceita a versão atualmente instalada desde que o pacote continue pertencendo à família CTF esperada:

```text
Microsoft.MinecraftUWP_<versao_instalada>_x64__8wekyb3d8bbwe
```

Isso cobre updates do CTF, como a nova versão 21.31, sem precisar editar manualmente o número da versão em vários arquivos.

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

Módulo local do CTF. Ele só aplica patches quando todos os controles abaixo passam:

1. o marcador do CTF existe;
2. o processo está dentro da package family allowlisted;
3. a arquitetura do pacote é x64;
4. `ENABLE_PATCHES=1` está no marcador;
5. existe assinatura definida em `src/module/user_patches.h`;
6. cada assinatura bate exatamente uma vez.

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

Agora não precisa mais informar a pasta versionada do WindowsApps. O script detecta a versão instalada automaticamente:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install_from_build.ps1 `
  -BuildDir ".\build"
```

O script procura um pacote instalado que bata com:

```text
Microsoft.MinecraftUWP_<versao_instalada>_x64__8wekyb3d8bbwe
```

Se o CTF usar uma instalação alternativa ou você precisar apontar manualmente para uma pasta específica, ainda dá para passar `-AppFolder`, mas a pasta precisa continuar batendo com a família/arquitetura permitida:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install_from_build.ps1 `
  -BuildDir ".\build" `
  -AppFolder "C:\Program Files\WindowsApps\Microsoft.MinecraftUWP_<versao_instalada>_x64__8wekyb3d8bbwe"
```

O script copia:

```text
vcruntime140_1.dll      -> pasta da build CTF
vcruntime140_2.dll      -> pasta da build CTF, vindo do VCLibs UWPDesktop x64 limpo
ctf_patch_module.dll    -> %APPDATA%\Minecraft Bedrock\mods
```

## Remover

Também não precisa mais informar a versão:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\uninstall_manual.ps1
```

Fallback manual:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\uninstall_manual.ps1 `
  -AppFolder "C:\Program Files\WindowsApps\Microsoft.MinecraftUWP_<versao_instalada>_x64__8wekyb3d8bbwe"
```

## Como adicionar patches

Edite:

```text
src/module/user_patches.h
```

A estrutura aceita pattern/mask, com `x` para byte exato e `?` para wildcard. Não deixe patches genéricos. Use assinaturas únicas da build do CTF e valide tudo no ambiente autorizado do evento.

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

## Interface Windows opcional

Este pacote também inclui `tools/CTFInstallerGui`, uma interface WinForms para instalar/remover o payload sem rodar instaladores de terceiros.

Observação: a GUI ainda pode precisar de uma atualização separada caso você queira que ela acompanhe o mesmo comportamento dinâmico dos scripts. O caminho recomendado para a versão 21.31 nesta branch é usar os scripts PowerShell acima.

## Limites

Este projeto é para laboratório autorizado do CTF. Não use em Minecraft oficial, Store real, pagamento real, conta Microsoft real ou servidor público.
