using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CTFInstallerGui;

public sealed class MainForm : Form
{
    private const string ExpectedPackageName = "Microsoft.MinecraftUWP";
    private const string ExpectedPackageFamilySuffix = "8wekyb3d8bbwe";
    private const string ExpectedPackageArchitecture = "x64";
    private const string MarkerDirName = "MinecraftBedrockCTF";
    private const string MarkerFileName = "ALLOW_CTF.txt";
    private const string CtfId = "MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF";
    private const string CtfSha = "06ca408f52e98204f93da63aee16bb6b751b0e3256bdcb6095d3dada1ba55c0e";

    private readonly TextBox _packageBox = new();
    private readonly TextBox _appFolderBox = new();
    private readonly TextBox _payloadBox = new();
    private readonly TextBox _modsBox = new();
    private readonly RichTextBox _logBox = new();
    private readonly CheckBox _enablePatchesBox = new();

    private readonly Label _buildStatus = new();
    private readonly Label _payloadStatus = new();
    private readonly Label _markerStatus = new();
    private readonly Label _installStatus = new();
    private readonly Label _adminStatus = new();

    private readonly Button _installButton;
    private readonly Button _removeButton;
    private readonly Button _launchButton;

    private readonly Color _bg = Color.FromArgb(15, 23, 42);
    private readonly Color _card = Color.FromArgb(30, 41, 59);
    private readonly Color _card2 = Color.FromArgb(51, 65, 85);
    private readonly Color _text = Color.FromArgb(241, 245, 249);
    private readonly Color _muted = Color.FromArgb(148, 163, 184);
    private readonly Color _accent = Color.FromArgb(56, 189, 248);
    private readonly Color _green = Color.FromArgb(34, 197, 94);
    private readonly Color _yellow = Color.FromArgb(250, 204, 21);
    private readonly Color _red = Color.FromArgb(248, 113, 113);

    private string MarkerDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        MarkerDirName);

    private string MarkerPath => Path.Combine(MarkerDir, MarkerFileName);

    public MainForm()
    {
        Text = "Minecraft Bedrock CTF — Clean Installer";
        MinimumSize = new Size(1120, 860);
        Size = new Size(1180, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _bg;
        ForeColor = _text;
        Font = new Font("Segoe UI", 9.5f);
        ApplyAppIcon();

        _installButton = MakeButton("Instalar / atualizar", Install, primary: true);
        _removeButton = MakeButton("Remover patch", Uninstall);
        _launchButton = MakeButton("Iniciar jogo", LaunchCtfBuild, primary: true);

        Controls.Add(BuildLayout());

        SetPathText(_modsBox, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Minecraft Bedrock",
            "mods"));

        Shown += (_, _) => AutoDetectEverything();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
            BackColor = _bg
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildStatusCards(), 0, 1);
        root.Controls.Add(BuildMainCards(), 0, 2);
        root.Controls.Add(BuildLogCard(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);

        return root;
    }

    private Control BuildHeader()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 92,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = _bg,
            Padding = new Padding(0, 0, 0, 14)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));

        var iconBox = new PictureBox
        {
            Size = new Size(46, 46),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Margin = new Padding(0, 8, 12, 0)
        };

        try
        {
            iconBox.Image = Icon.ToBitmap();
        }
        catch
        {
            iconBox.Visible = false;
        }

        var textPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = _bg,
            Margin = new Padding(0)
        };
        textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var title = new Label
        {
            Text = "Clean CTF Installer",
            Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
            ForeColor = _text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };

        var subtitle = new Label
        {
            Text = "Instala o proxy e o módulo limpo para a build allowlisted do CTF. Payload detectado automaticamente.",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = _muted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };

        textPanel.Controls.Add(title, 0, 0);
        textPanel.Controls.Add(subtitle, 0, 1);

        var badge = new Label
        {
            Text = "CTF ONLY",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(7, 89, 133),
            BackColor = Color.FromArgb(186, 230, 253),
            Dock = DockStyle.Top,
            Height = 30,
            Margin = new Padding(0, 12, 0, 0)
        };

        grid.Controls.Add(iconBox, 0, 0);
        grid.Controls.Add(textPanel, 1, 0);
        grid.Controls.Add(badge, 2, 0);

        return grid;
    }

    private Control BuildStatusCards()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 5,
            RowCount = 1,
            Height = 92,
            BackColor = _bg,
            Padding = new Padding(0, 0, 0, 12)
        };

        for (int i = 0; i < 5; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        grid.Controls.Add(StatusTile("Build", _buildStatus), 0, 0);
        grid.Controls.Add(StatusTile("Payload", _payloadStatus), 1, 0);
        grid.Controls.Add(StatusTile("Marcador", _markerStatus), 2, 0);
        grid.Controls.Add(StatusTile("Instalação", _installStatus), 3, 0);
        grid.Controls.Add(StatusTile("Admin", _adminStatus), 4, 0);

        SetStatus(_buildStatus, "aguardando", StatusKind.Waiting);
        SetStatus(_payloadStatus, "aguardando", StatusKind.Waiting);
        SetStatus(_markerStatus, "aguardando", StatusKind.Waiting);
        SetStatus(_installStatus, "aguardando", StatusKind.Waiting);
        SetStatus(_adminStatus, "verificando", StatusKind.Waiting);

        return grid;
    }

    private Control StatusTile(string title, Label value)
    {
        var panel = new Panel
        {
            BackColor = _card,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(14, 10, 14, 10)
        };

        var titleLabel = new Label
        {
            Text = title.ToUpperInvariant(),
            ForeColor = _muted,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 10)
        };

        value.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        value.AutoSize = true;
        value.Location = new Point(14, 38);

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(value);
        return panel;
    }

    private Control BuildMainCards()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 390,
            BackColor = _bg,
            Padding = new Padding(0, 0, 0, 12)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        grid.Controls.Add(BuildPathsCard(), 0, 0);
        grid.Controls.Add(BuildActionsCard(), 1, 0);

        return grid;
    }

    private Control BuildPathsCard()
    {
        var (card, content) = MakeCard("Caminhos detectados", "O payload é achado sozinho ao lado da GUI: release\\payload.");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(0, 8, 0, 0)
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));

        for (int i = 0; i < 4; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        AddPathRow(table, "Package", _packageBox, null);
        AddPathRow(table, "Build CTF", _appFolderBox, null);
        AddPathRow(table, "Payload", _payloadBox, ChoosePayloadManually);
        AddPathRow(table, "Mods", _modsBox, BrowseMods);

        _packageBox.ReadOnly = true;
        _appFolderBox.ReadOnly = true;
        _payloadBox.ReadOnly = true;

        content.Controls.Add(table);
        return card;
    }

    private Control BuildActionsCard()
    {
        var (card, content) = MakeCard("Ações", "Feche o jogo antes de instalar, atualizar ou remover.");

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = _card
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        void AddAction(Button button, int row)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 4, 0, 4);
            button.AutoSize = false;
            panel.Controls.Add(button, 0, row);
        }

        AddAction(MakeButton("Detectar tudo automaticamente", AutoDetectEverything), 0);
        AddAction(MakeButton("Criar / atualizar marcador", () => CreateMarker(_enablePatchesBox.Checked)), 1);
        AddAction(_installButton, 2);
        AddAction(_removeButton, 3);
        AddAction(_launchButton, 4);
        AddAction(MakeButton("Abrir pasta de logs", OpenLogsFolder), 5);

        _enablePatchesBox.Text = "Ativar patch em memória (ENABLE_PATCHES=1)";
        _enablePatchesBox.Checked = true;
        _enablePatchesBox.AutoSize = false;
        _enablePatchesBox.Dock = DockStyle.Fill;
        _enablePatchesBox.ForeColor = _text;
        _enablePatchesBox.Padding = new Padding(4, 8, 0, 0);
        panel.Controls.Add(_enablePatchesBox, 0, 6);

        var note = new Label
        {
            Text = "Payload: automático. Use Manual somente se mover a pasta.",
            ForeColor = _muted,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(note, 0, 7);

        content.Controls.Add(panel);
        return card;
    }

    private Control BuildLogCard()
    {
        var (card, content) = MakeCard("Log", "Mensagens locais do instalador. O módulo escreve logs em %LOCALAPPDATA%\\MinecraftBedrockCTF.");

        _logBox.Dock = DockStyle.Fill;
        _logBox.ReadOnly = true;
        _logBox.WordWrap = false;
        _logBox.ScrollBars = RichTextBoxScrollBars.Both;
        _logBox.Font = new Font("Consolas", 9.5f);
        _logBox.BackColor = Color.FromArgb(2, 6, 23);
        _logBox.ForeColor = Color.FromArgb(203, 213, 225);
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.Margin = new Padding(0, 8, 0, 0);

        content.Controls.Add(_logBox);
        return card;
    }

    private Control BuildFooter()
    {
        var label = new Label
        {
            Text = "Escopo: somente " + CtfId + ". Não modifica xgameruntime.dll no disco; o patch é em memória dentro da build autorizada.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = _muted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        return label;
    }

    private (Panel Card, Panel Content) MakeCard(string title, string subtitle)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _card,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 12, 0)
        };

        var titleLabel = new Label
        {
            Text = title,
            ForeColor = _text,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 12)
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            ForeColor = _muted,
            Font = new Font("Segoe UI", 9.2f),
            AutoSize = true,
            Location = new Point(17, 42),
            MaximumSize = new Size(720, 0)
        };

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 62, 0, 0),
            BackColor = _card
        };

        card.Controls.Add(content);
        card.Controls.Add(titleLabel);
        card.Controls.Add(subtitleLabel);

        return (card, content);
    }

    private void AddPathRow(TableLayoutPanel panel, string label, TextBox box, Action? browse)
    {
        StyleTextBox(box);
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 8, 8, 8);
        box.Height = 30;

        panel.Controls.Add(new Label
        {
            Text = label,
            ForeColor = _muted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 0)
        });

        panel.Controls.Add(box);

        if (browse is null)
        {
            panel.Controls.Add(new Label());
        }
        else
        {
            var btn = MakeButton(label.Equals("Payload", StringComparison.OrdinalIgnoreCase) ? "Manual" : "Selecionar", browse);
            btn.Dock = DockStyle.Fill;
            btn.Margin = new Padding(0, 8, 0, 8);
            btn.AutoSize = false;
            panel.Controls.Add(btn);
        }
    }

    private void StyleTextBox(TextBox box)
    {
        box.BackColor = _card2;
        box.ForeColor = _text;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Segoe UI", 9.5f);
    }

    private Button MakeButton(string text, Action action, bool primary = false, bool fullWidth = false)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Width = fullWidth ? 310 : 140,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? _accent : _card2,
            ForeColor = primary ? Color.FromArgb(8, 47, 73) : _text,
            Font = new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold),
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(4, 5, 4, 5),
            Cursor = Cursors.Hand
        };

        btn.FlatAppearance.BorderColor = primary ? _accent : Color.FromArgb(71, 85, 105);
        btn.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(125, 211, 252) : Color.FromArgb(71, 85, 105);

        btn.Click += (_, _) =>
        {
            try
            {
                UseWaitCursor = true;
                action();
                RefreshStatuses();
            }
            catch (Exception ex)
            {
                Log("ERRO: " + ex.Message);
                RefreshStatuses();
                MessageBox.Show(ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        };

        return btn;
    }

    private void AutoDetectEverything()
    {
        Log("Rodando detecção automática...");

        DetectPayload(throwOnFail: false);

        try { DetectCtfPackage(throwOnFail: false); }
        catch (Exception ex) { Log("Build ainda não detectada: " + ex.Message); }

        RefreshStatuses();
    }

    private bool DetectPayload(bool throwOnFail)
    {
        string? found = FindPayloadDirectory();
        if (found is not null)
        {
            SetPathText(_payloadBox, found);
            Log("Payload detectado automaticamente: " + found);
            return true;
        }

        string fallback = GuessLikelyPayloadFolder();
        SetPathText(_payloadBox, fallback);
        Log("Payload não encontrado automaticamente. Pasta provável: " + fallback);

        if (throwOnFail)
            throw new FileNotFoundException("Não achei automaticamente vcruntime140_1.dll e ctf_patch_module.dll. Baixe o artifact compilado e mantenha a pasta 'payload' ao lado da pasta 'gui'.");

        return false;
    }

    private string? FindPayloadDirectory()
    {
        var dirs = new List<string>();

        void AddDir(string? d)
        {
            if (!string.IsNullOrWhiteSpace(d) &&
                Directory.Exists(d) &&
                !dirs.Contains(d, StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(d);
            }
        }

        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string current = Directory.GetCurrentDirectory();
        string? parent = Directory.GetParent(baseDir)?.FullName;
        string? grandParent = parent is null ? null : Directory.GetParent(parent)?.FullName;

        AddDir(Environment.GetEnvironmentVariable("CTF_PAYLOAD_DIR"));
        AddDir(Path.Combine(baseDir, "payload"));
        AddDir(parent is null ? null : Path.Combine(parent, "payload"));
        AddDir(grandParent is null ? null : Path.Combine(grandParent, "payload"));
        AddDir(Path.Combine(current, "payload"));
        AddDir(baseDir);
        AddDir(parent);
        AddDir(current);

        string? cursor = baseDir;
        for (int i = 0; i < 6 && cursor is not null; i++)
        {
            AddDir(Path.Combine(cursor, "payload"));
            AddDir(cursor);
            cursor = Directory.GetParent(cursor)?.FullName;
        }

        foreach (string d in dirs)
        {
            if (IsPayloadDir(d))
                return d;
        }

        foreach (string d in dirs)
        {
            try
            {
                string? found = Directory.EnumerateDirectories(d, "*", SearchOption.AllDirectories)
                    .Take(250)
                    .FirstOrDefault(IsPayloadDir);
                if (found is not null)
                    return found;
            }
            catch
            {
                // Ignore inaccessible folders.
            }
        }

        return null;
    }

    private static bool IsPayloadDir(string dir)
    {
        return File.Exists(Path.Combine(dir, "vcruntime140_1.dll")) &&
               File.Exists(Path.Combine(dir, "ctf_patch_module.dll"));
    }

    private string GuessLikelyPayloadFolder()
    {
        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string? parent = Directory.GetParent(baseDir)?.FullName;
        return parent is null ? Path.Combine(baseDir, "payload") : Path.Combine(parent, "payload");
    }

    private void ChoosePayloadManually()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Fallback manual: selecione a pasta que contém vcruntime140_1.dll e ctf_patch_module.dll"
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SetPathText(_payloadBox, dlg.SelectedPath);
            if (IsPayloadDir(dlg.SelectedPath))
                Log("Payload manual válido: " + dlg.SelectedPath);
            else
                Log("Aviso: a pasta escolhida não contém as duas DLLs esperadas.");
        }
    }

    private void BrowseMods()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Selecione a pasta mods do ambiente CTF"
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
            SetPathText(_modsBox, dlg.SelectedPath);
    }

    private void DetectCtfPackage(bool throwOnFail = true)
    {
        Log("Detectando pacote CTF allowlisted sem fixar versão...");
        string script = @"
$pattern = '^Microsoft\.MinecraftUWP_[^_]+_x64__8wekyb3d8bbwe$'
$pkg = Get-AppxPackage -Name Microsoft.MinecraftUWP -ErrorAction SilentlyContinue |
  Where-Object {
    $_.PackageFullName -match $pattern -and
    $_.InstallLocation -and
    (Test-Path $_.InstallLocation)
  } |
  Sort-Object Version -Descending |
  Select-Object -First 1 Name, PackageFullName, InstallLocation, Version
if (!$pkg) { exit 2 }
$pkg | ConvertTo-Json -Compress
";
        string output = RunPowerShell(script);
        if (string.IsNullOrWhiteSpace(output))
        {
            if (throwOnFail)
                throw new InvalidOperationException("Build CTF não encontrada via Get-AppxPackage.");
            return;
        }

        using var doc = JsonDocument.Parse(output);
        SetPathText(_packageBox, doc.RootElement.GetProperty("PackageFullName").GetString() ?? "");
        SetPathText(_appFolderBox, doc.RootElement.GetProperty("InstallLocation").GetString() ?? "");

        if (!IsAllowedPackage(_packageBox.Text, _appFolderBox.Text))
            throw new InvalidOperationException("Pacote encontrado não bate com a allowlist dinâmica do CTF.");

        Log("Build detectada: " + _packageBox.Text);
        if (doc.RootElement.TryGetProperty("Version", out JsonElement version))
            Log("Versão detectada: " + version.ToString());
        Log("Pasta da build: " + _appFolderBox.Text);
    }

    private void CreateMarker(bool enable)
    {
        Directory.CreateDirectory(MarkerDir);

        string text =
            "CTF-ID=" + CtfId + "\r\n" +
            "CTF-SHA256=" + CtfSha + "\r\n" +
            "ENABLE_PATCHES=" + (enable ? "1" : "0") + "\r\n";

        File.WriteAllText(MarkerPath, text, Encoding.ASCII);
        Log("Marcador criado/atualizado: " + MarkerPath);
        Log("ENABLE_PATCHES=" + (enable ? "1" : "0"));
    }

    private void Install()
    {
        EnsureGameClosed();
        EnsureDetected();

        if (!IsPayloadDir(_payloadBox.Text.Trim()))
            DetectPayload(throwOnFail: true);

        string appFolder = _appFolderBox.Text.Trim();
        string payload = _payloadBox.Text.Trim();
        string mods = _modsBox.Text.Trim();

        string proxySrc = Path.Combine(payload, "vcruntime140_1.dll");
        string moduleSrc = Path.Combine(payload, "ctf_patch_module.dll");

        if (!File.Exists(proxySrc))
            throw new FileNotFoundException("Não achei vcruntime140_1.dll no payload detectado.", proxySrc);
        if (!File.Exists(moduleSrc))
            throw new FileNotFoundException("Não achei ctf_patch_module.dll no payload detectado.", moduleSrc);

        Log("Payload confirmado:");
        Log("  proxy : " + proxySrc);
        Log("  módulo: " + moduleSrc);

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

        Log("Instalação concluída.");
        Log("  proxy  -> " + proxyDst);
        Log("  runtime-> " + runtimeDst);
        Log("  módulo -> " + moduleDst);
        Log("Agora abra a build CTF. O patch acontece em memória quando o jogo carregar.");
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
        Log("Iniciando via shell:AppsFolder...");
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
        string suffix = "_" + ExpectedPackageArchitecture + "__" + ExpectedPackageFamilySuffix;
        return packageFullName.StartsWith(ExpectedPackageName + "_", StringComparison.OrdinalIgnoreCase)
            && packageFullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && installLocation.Contains(ExpectedPackageName + "_", StringComparison.OrdinalIgnoreCase)
            && installLocation.Contains(suffix, StringComparison.OrdinalIgnoreCase);
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
                catch
                {
                    return false;
                }
            })
            .Select(p => p.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
        if (!File.Exists(path))
            return;

        string backup = path + ".ctfbackup." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(path, backup, overwrite: false);
        Log("Backup: " + backup);
    }

    private void WriteInstallState(params string[] paths)
    {
        var state = new List<InstallFileState>();
        foreach (string path in paths)
        {
            if (!File.Exists(path))
                continue;

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

    private void RefreshStatuses()
    {
        bool buildOk = IsAllowedPackage(_packageBox.Text.Trim(), _appFolderBox.Text.Trim());
        bool payloadOk = IsPayloadDir(_payloadBox.Text.Trim());
        bool markerOk = File.Exists(MarkerPath);
        bool installOk = buildOk &&
                         File.Exists(Path.Combine(_appFolderBox.Text.Trim(), "vcruntime140_1.dll")) &&
                         File.Exists(Path.Combine(_appFolderBox.Text.Trim(), "vcruntime140_2.dll")) &&
                         File.Exists(Path.Combine(_modsBox.Text.Trim(), "ctf_patch_module.dll"));

        SetStatus(_buildStatus, buildOk ? "OK" : "não detectada", buildOk ? StatusKind.Ok : StatusKind.Warning);
        SetStatus(_payloadStatus, payloadOk ? "OK automático" : "não achado", payloadOk ? StatusKind.Ok : StatusKind.Warning);
        SetStatus(_markerStatus, markerOk ? "OK" : "pendente", markerOk ? StatusKind.Ok : StatusKind.Warning);
        SetStatus(_installStatus, installOk ? "instalado" : "não instalado", installOk ? StatusKind.Ok : StatusKind.Waiting);
        SetStatus(_adminStatus, IsRunningAsAdmin() ? "elevado" : "normal", IsRunningAsAdmin() ? StatusKind.Ok : StatusKind.Warning);
    }

    private void SetStatus(Label label, string text, StatusKind kind)
    {
        label.Text = text;
        label.ForeColor = kind switch
        {
            StatusKind.Ok => _green,
            StatusKind.Warning => _yellow,
            StatusKind.Error => _red,
            _ => _muted
        };
    }


    private void ApplyAppIcon()
    {
        try
        {
            Icon? extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null)
            {
                Icon = (Icon)extracted.Clone();
                extracted.Dispose();
            }
        }
        catch
        {
            // Icon is cosmetic only.
        }
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void SetPathText(TextBox box, string text)
    {
        box.Text = text;
        box.SelectionStart = 0;
        box.SelectionLength = 0;
        box.ScrollToCaret();
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
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private enum StatusKind
    {
        Waiting,
        Ok,
        Warning,
        Error
    }

    private sealed record InstallFileState(string Path, string Sha256);
}
