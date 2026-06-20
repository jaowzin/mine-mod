# Correção da GUI para versão dinâmica do CTF

A GUI atual ainda está presa à build antiga:

```csharp
private const string ExpectedPackage = "MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe";
```

Por isso ela não detecta a build atual do CTF:

```text
Microsoft.MinecraftUWP_1.26.3101.0_x64__8wekyb3d8bbwe
```

## Objetivo

Alterar `tools/CTFInstallerGui/MainForm.cs` para aceitar qualquer versão instalada dentro do mesmo padrão de package family usado no CTF:

```text
Microsoft.MinecraftUWP_<versao>_x64__8wekyb3d8bbwe
```

## Troca 1: constantes

Substituir a constante fixa antiga por:

```csharp
private const string ExpectedPackageName = "Microsoft.MinecraftUWP";
private const string ExpectedPackageFamilySuffix = "8wekyb3d8bbwe";
private const string ExpectedPackageArchitecture = "x64";
```

## Troca 2: DetectCtfPackage

A função `DetectCtfPackage` deve usar `Get-AppxPackage -Name Microsoft.MinecraftUWP` e filtrar por regex de versão dinâmica:

```powershell
$pattern = '^' + [regex]::Escape('Microsoft.MinecraftUWP') + '_[^_]+_x64__8wekyb3d8bbwe$'
$pkg = Get-AppxPackage -Name Microsoft.MinecraftUWP -ErrorAction SilentlyContinue |
  Where-Object {
    $_.PackageFullName -match $pattern -and
    $_.InstallLocation -and
    (Test-Path $_.InstallLocation)
  } |
  Sort-Object Version -Descending |
  Select-Object -First 1 Name, PackageFullName, InstallLocation, Version
```

## Troca 3: IsAllowedPackage

Substituir a validação fixa por:

```csharp
private static bool IsAllowedPackage(string packageFullName, string installLocation)
{
    string suffix = "_" + ExpectedPackageArchitecture + "__" + ExpectedPackageFamilySuffix;
    return packageFullName.StartsWith(ExpectedPackageName + "_", StringComparison.OrdinalIgnoreCase)
        && packageFullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        && installLocation.Contains(ExpectedPackageName + "_", StringComparison.OrdinalIgnoreCase)
        && installLocation.Contains(suffix, StringComparison.OrdinalIgnoreCase);
}
```

## Resultado esperado

Após recompilar a GUI pelo workflow, o instalador deve detectar:

```text
Microsoft.MinecraftUWP_1.26.3101.0_x64__8wekyb3d8bbwe
```

e futuras versões do mesmo package family do CTF.
