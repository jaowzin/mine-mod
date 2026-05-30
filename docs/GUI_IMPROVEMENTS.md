# GUI improvements

Mudanças desta versão:

- Correção do painel de ações que ficava cortado em resoluções menores.
- Altura mínima da janela aumentada para evitar sobreposição.
- `Payload/build` agora é automático e read-only por padrão.
- Botão **Manual** fica apenas como fallback.
- Adicionado ícone próprio em `tools/CTFInstallerGui/assets/app.ico`.
- `ApplicationIcon` integrado no `.csproj`, então o `.exe` sai com ícone no build.
- O formulário tenta usar o ícone embutido via `Icon.ExtractAssociatedIcon`.
- Status de admin agora é checado de verdade com `WindowsPrincipal`.

Escopo preservado: build allowlisted do CTF, sem modificar `xgameruntime.dll` no disco.
