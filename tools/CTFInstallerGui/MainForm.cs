using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CTFInstallerGui;

public sealed class MainForm : Form
{
    private const string ExpectedPackage = "MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe";
    private const string MarkerDirName = "MinecraftBedrockCTF";
    private const string MarkerFileName = "ALLOW_CTF.txt";
    private const string CtfId = "MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF";
    private const string CtfSha = "06ca408f52e98204f93da63aee16bb6b751b0e3256bdcb6095d3dada1ba55c0e";

    private readonly TextBox _packageBox = new();
    private readonly TextBox _appFolderBox = new();
    private readonly TextBox _payloadBox = new();
    private readonly TextBox _modsBox = new();
    private readonly TextBox _logBox = new();
    private readonly CheckBox _enablePatchesBox = new();

    private string MarkerDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        MarkerDirName);

    private string MarkerPath => Path.Combine(MarkerDir, MarkerFileName);

    public MainForm()
    {
        Text = "Minecraft Bedrock CTF Clean Installer";
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12)
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Instalador/Launcher limpo para o CTF autorizado",
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            AutoSize = true
        };
        root.Controls.Add(title);

        var note = new Label
        {
            Text = "Não é injector genérico. Ele só prepara a build allowlisted do CTF, copia o proxy/módulo, cria marcador local e deixa o patch em memória para o jogo carregar ao iniciar.",
            AutoSize = true,
            MaximumSize = new Size(920, 0)
        };
        root.Controls.Add(note);

        root.Controls.Add(BuildPathsPanel());
        root.Controls.Add(BuildButtonsPanel());

        _logBox.Multiline = true;
        _logBox.ScrollBars = ScrollBars.Both;
        _logBox.ReadOnly = true;
        _logBox.WordWrap = false;
        _logBox.Font = new Font("Consolas", 9);
        root.Controls.Add(_logBox);

        var footer = new Label
        {
            Text = "Escopo: somente CTF-ID " + CtfId + ". Feche o jogo antes de instalar/remover.",
            AutoSize = true
        };
        root.Controls.Add(footer);

        Controls.Add(root);

        _payloadBox.Text = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        _modsBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Minecraft Bedrock",
            "mods");

        Shown += (_, _) => DetectCtfPackage();
    }

    private Control BuildPathsPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 12, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        AddPathRow(panel, "Package:", _packageBox, null);
        AddPathRow(panel, "Pasta da build:", _appFolderBox, null);
        AddPathRow(panel, "Payload/build:", _payloadBox, BrowsePayload);
        AddPathRow(panel, "Pasta mods:", _modsBox, BrowseMods);

        _packageBox.ReadOnly = true;
        _appFolderBox.ReadOnly = true;
        return panel;
    }

    private static void AddPathRow(TableLayoutPanel panel, string label, TextBox box, Action? browse)
    {
        box.Dock = DockStyle.Fill;

        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        panel.Controls.Add(box);

        if (browse is null)
        {
            panel.Controls.Add(new Label());
        }
        else
        {
            var btn = new Button { Text = "Selecionar", Dock = DockStyle.Fill };
            btn.Click += (_, _) => browse();
            panel.Controls.Add(btn);
        }
    }

    private Control BuildButtonsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 8)
        };

        panel.Controls.Add(MakeButton("Detectar build", DetectCtfPackage));
        panel.Controls.Add(MakeButton("Criar marcador", () => CreateMarker(_enablePatchesBox.Checked)));
        panel.Controls.Add(MakeButton("Instalar/atualizar", Install));
        panel.Controls.Add(MakeButton("Remover", Uninstall));
        panel.Controls.Add(MakeButton("Abrir logs", OpenLogsFolder));
        panel.Controls.Add(MakeButton("Iniciar jogo", LaunchCtfBuild));

        _enablePatchesBox.Text = "ENABLE_PATCHES=1";
        _enablePatchesBox.Checked = true;
        _enablePatchesBox.AutoSize = true;
        _enablePatchesBox.Padding = new Padding(12, 8, 0, 0);
        panel.Controls.Add(_enablePatchesBox);

        return panel;
    }

    private static Button MakeButton(string text, Action action)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(4)
        };
        btn.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        return btn;
    }

    private void BrowsePayload()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Selecione a pasta que contém vcruntime140_1.dll e ctf_patch_module.dll"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _payloadBox.Text = dlg.SelectedPath;
    }

    private void BrowseMods()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Selecione a pasta mods"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _modsBox.Text = dlg.SelectedPath;
    }

    private void DetectCtfPackage()
    {
        Log("Detectando pacote CTF allowlisted...");
        string script = @"
$pkg = Get-AppxPackage | Where-Object {
  $_.PackageFullName -ieq '" + ExpectedPackage + @"'
} | Select-Object -First 1 Name, PackageFullName, InstallLocation
if (!$pkg) { exit 2 }
$pkg | ConvertTo-Json -Compress
";
        string output = RunPowerShell(script);
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Build CTF não encontrada via Get-AppxPackage.");

        using var doc = JsonDocument.Parse(output);
        _packageBox.Text = doc.RootElement.GetProperty("PackageFullName").GetString() ?? "";
        _appFolderBox.Text = doc.RootElement.GetProperty("InstallLocation").GetString() ?? "";

        if (!IsAllowedPackage(_packageBox.Text, _appFolderBox.Text))
            throw new InvalidOperationException("Pacote encontrado não bate com a allowlist do CTF.");

        Log("Pacote detectado: " + _packageBox.Text);
        Log("Pasta: " + _appFolderBox.Text);
    }

    private void CreateMarker(bool enable)
    {
        Directory.CreateDirectory(MarkerDir);
        string text =
            "CTF-ID=" + CtfId + "\r\n" +
            "CTF-SHA256=" + CtfSha + "\r\n" +
            "ENABLE_PATCHES=" + (enable ? "1" : "0") + "\r\n";
        File.WriteAllText(MarkerPath, text, Encoding.ASCII);
        Log("Marcador criado: " + MarkerPath);
        Log("ENABLE_PATCHES=" + (enable ? "1" : "0"));
    }

    private void Install()
    {
        EnsureGameClosed();
        EnsureDetected();

        string appFolder = _appFolderBox.Text.Trim();
        string payload = _payloadBox.Text.Trim();
        string mods = _modsBox.Text.Trim();

        string proxySrc = Path.Combine(payload, "vcruntime140_1.dll");
        string moduleSrc = Path.Combine(payload, "ctf_patch_module.dll");

        // Support GitHub artifact layout: payload can also be parent folder containing build\Release.
        if (!File.Exists(proxySrc))
            proxySrc = Directory.EnumerateFiles(payload, "vcruntime140_1.dll", SearchOption.AllDirectories).FirstOrDefault() ?? proxySrc;
        if (!File.Exists(moduleSrc))
            moduleSrc = Directory.EnumerateFiles(payload, "ctf_patch_module.dll", SearchOption.AllDirectories).FirstOrDefault() ?? moduleSrc;

        if (!File.Exists(proxySrc))
            throw new FileNotFoundException("Não achei vcruntime140_1.dll no payload/build.", proxySrc);
        if (!File.Exists(moduleSrc))
            throw new FileNotFoundException("Não achei ctf_patch_module.dll no payload/build.", moduleSrc);

        string runtimeSrc = FindUwpDesktopRuntime();
        if (!File.Exists(runtimeSrc))
            throw new FileNotFoundException("Não achei o vcruntime140_1.dll UWPDesktop x64 limpo.", runtimeSrc);

        CreateMarker(_enablePatchesBox.Checked);

        Directory.CreateDirectory(mods);

        string proxyDst = Path.Combine(appFolder, "vcruntime140_1.dll");
        string runtimeDst = Path.Combine(appFolder, "vcruntime140_2.dll");
        string moduleDst = Path.Combine(mods, "ctf_patch_module.dll");

        BackupIfExists(proxyDst);
        BackupIfExists(runtimeDst);
        BackupIfExists(moduleDst);

        File.Copy(proxySrc, proxyDst, overwrite: true);
        File.Copy(runtimeSrc, runtimeDst, overwrite: true);
        File.Copy(moduleSrc, moduleDst, overwrite: true);

        WriteInstallState(proxyDst, runtimeDst, moduleDst);

        Log("Instalado:");
        Log("  proxy  -> " + proxyDst);
        Log("  runtime-> " + runtimeDst);
        Log("  módulo -> " + moduleDst);
        Log("Agora inicie a build CTF. O jogo deve estar fechado durante a instalação, mas aberto depois para o patch em memória acontecer.");
    }

    private void Uninstall()
    {
        EnsureGameClosed();
        EnsureDetected();

        string appFolder = _appFolderBox.Text.Trim();
        string mods = _modsBox.Text.Trim();

        string proxyDst = Path.Combine(appFolder, "vcruntime140_1.dll");
        string runtimeDst = Path.Combine(appFolder, "vcruntime140_2.dll");
        string moduleDst = Path.Combine(mods, "ctf_patch_module.dll");

        DeleteIfInstalled(proxyDst);
        DeleteIfInstalled(runtimeDst);
        DeleteIfInstalled(moduleDst);

        Log("Remoção concluída. Backups .ctfbackup ficam preservados.");
    }

    private void LaunchCtfBuild()
    {
        EnsureDetected();
        Log("Tentando iniciar via shell:AppsFolder...");
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = @"shell:AppsFolder\MICROSOFT.MINECRAFTUWP_8wekyb3d8bbwe!App",
            UseShellExecute = true
        });
    }

    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(MarkerDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + MarkerDir + "\"",
            UseShellExecute = true
        });
    }

    private void EnsureDetected()
    {
        if (string.IsNullOrWhiteSpace(_packageBox.Text) || string.IsNullOrWhiteSpace(_appFolderBox.Text))
            DetectCtfPackage();

        if (!IsAllowedPackage(_packageBox.Text, _appFolderBox.Text))
            throw new InvalidOperationException("Recusado: pacote/pasta não bate com a allowlist do CTF.");
    }

    private static bool IsAllowedPackage(string packageFullName, string installLocation)
    {
        return packageFullName.Equals(ExpectedPackage, StringComparison.OrdinalIgnoreCase)
            && installLocation.Contains("MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureGameClosed()
    {
        var procs = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return p.ProcessName.Contains("Minecraft", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .Select(p => p.ProcessName)
            .Distinct()
            .ToArray();

        if (procs.Length > 0)
        {
            throw new InvalidOperationException(
                "Feche a build CTF antes de instalar/remover. Processo aberto: " + string.Join(", ", procs));
        }
    }

    private string FindUwpDesktopRuntime()
    {
        string script = @"
$pkg = Get-AppxPackage Microsoft.VCLibs.140.00.UWPDesktop |
  Where-Object { $_.PackageFullName -like '*_x64__*' } |
  Sort-Object PackageFullName -Descending |
  Select-Object -First 1
if (!$pkg) { exit 3 }
$p = Join-Path $pkg.InstallLocation 'vcruntime140_1.dll'
if (!(Test-Path $p)) { exit 4 }
$p
";
        string path = RunPowerShell(script).Trim();
        Log("Runtime UWPDesktop x64: " + path);
        return path;
    }

    private static string RunPowerShell(string script)
    {
        using var p = new Process();
        p.StartInfo.FileName = "powershell.exe";
        p.StartInfo.ArgumentList.Add("-NoProfile");
        p.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        p.StartInfo.ArgumentList.Add("Bypass");
        p.StartInfo.ArgumentList.Add("-Command");
        p.StartInfo.ArgumentList.Add(script);
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException("PowerShell falhou (" + p.ExitCode + "): " + stderr.Trim());

        return stdout.Trim();
    }

    private void BackupIfExists(string path)
    {
        if (!File.Exists(path)) return;
        string backup = path + ".ctfbackup." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(path, backup, overwrite: false);
        Log("Backup: " + backup);
    }

    private void WriteInstallState(params string[] paths)
    {
        var state = new List<InstallFileState>();
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            state.Add(new InstallFileState(path, Sha256(path)));
        }

        Directory.CreateDirectory(MarkerDir);
        File.WriteAllText(
            Path.Combine(MarkerDir, "install_state.json"),
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    private void DeleteIfInstalled(string path)
    {
        if (!File.Exists(path))
        {
            Log("Não existe: " + path);
            return;
        }

        // Safety guard: delete only known CTF filenames, not arbitrary system DLLs elsewhere.
        string file = Path.GetFileName(path);
        if (!file.Equals("vcruntime140_1.dll", StringComparison.OrdinalIgnoreCase) &&
            !file.Equals("vcruntime140_2.dll", StringComparison.OrdinalIgnoreCase) &&
            !file.Equals("ctf_patch_module.dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Recusado remover arquivo inesperado: " + path);
        }

        File.Delete(path);
        Log("Removido: " + path);
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private void Log(string msg)
    {
        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
    }

    private sealed record InstallFileState(string Path, string Sha256);
}
