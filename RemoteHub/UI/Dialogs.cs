using System.Text.RegularExpressions;

namespace RemoteHub.UI;

// =====================================================================================================
// Settings: bastion, auto-type, display, security
// =====================================================================================================
internal sealed class SettingsDialog : DialogBase
{
    private readonly Store _store;
    private readonly TextBox _host, _primaryUser, _primaryPass, _auth, _template, _sequence;
    private readonly NumericUpDown _port, _delay, _autoLock;
    private readonly CheckBox _nla, _interactiveTarget, _autoFill, _autoType, _detect, _smart, _dynamic, _clipboard, _drives, _audio, _winKeys, _usKbd, _confirmClose, _startFull, _diag;
    private readonly ComboBox _authLevel, _colorDepth, _resolution;
    private readonly Label _preview;

    public SettingsDialog(Store store) : base("Settings", 640, 620)
    {
        _store = store;
        var b = store.Data.Bastion;
        var s = store.Data.Settings;

        Section("Step 1 — bastion (WALLIX) login");
        _host = Row("Address", Theme.TextBox(b.Host), "IP or hostname of the bastion you connect to with mstsc (step 1).");
        _port = Row("Port", Theme.Number(b.Port, 1, 65535));
        _primaryUser = Row("Primary login", Theme.TextBox(b.PrimaryUser), "Your bastion username (step 1).");
        _primaryPass = PasswordRow("Primary password", b.PrimaryPassword);
        _auth = Row("Authorization", Theme.TextBox(b.Authorization),
            "Value of the \"Authorization\" column in the WALLIX list (e.g. Sanay-Shahmoradi). Optional.");
        _template = Row("Login format", Theme.TextBox(b.LoginTemplate),
            "How the direct-target login is built. {target} {auth} {user}. Empty parts are removed.");
        _template.Font = Theme.Mono;
        _preview = Row("Example", new Label { AutoSize = true, ForeColor = Theme.Green, Font = Theme.Mono, Margin = new Padding(0, Theme.S(6), 0, 0) });
        _interactiveTarget = Row("On WALLIX login page", Theme.Check("Type the full target string here too (to skip the machine list)", b.InteractiveLoginUsesTarget),
            "Off: types just your primary login on the bastion's own login page, then you pick from the list. On: types the full Interactive@… string there, to go straight to the machine.");
        _nla = Row("Security", Theme.Check("Use NLA / CredSSP for the bastion login", b.UseNla));
        _authLevel = Row("Server certificate", Theme.Combo());
        _authLevel.Items.AddRange(["Connect, pin key on first use (recommended)", "Require a valid certificate", "Warn me (Windows dialog)"]);
        _authLevel.SelectedIndex = Math.Clamp(b.AuthLevel, 0, 2);

        Section("Saved logins (the password list)");
        var credBtn = Theme.Button("🔑 Edit saved logins…", primary: true);
        credBtn.Click += (_, _) => { using var dlg = new CredentialsDialog(_store); dlg.ShowDialog(this); };
        Row("Logins", credBtn,
            "Add your machine logins here. In a session, press Ctrl+Alt+K (or the ⌨ button), pick one, and it is typed on the current screen. Saved encrypted.");
        _autoFill = Row("Auto-fill", Theme.Check("Also try to fill automatically right after connecting (uses screen detection)", s.AutoFillOnConnect),
            "Off (recommended): you pick the login with Ctrl+Alt+K. On: RemoteHub tries to detect the login screen and fill it by itself.");
        _autoType = Row("", Theme.Check("  …and for that, type the machine login on the \"Target Information Required\" screen", s.AutoTypeEnabled));
        _detect = Row("", Theme.Check("  …only when a WALLIX login screen is detected", s.AutoTypeDetectScreen));
        _delay = Row("Delay after connect (ms)", Theme.Number(s.AutoTypeDelayMs, 0, 30000, 250));
        _sequence = Row("Key sequence", Theme.TextBox(s.AutoTypeSequence),
            "How a login is typed. Tokens: {login} {password} {TAB} {ENTER} {SPACE} {DELAY:500}.");
        _sequence.Font = Theme.Mono;

        Section("Display & devices");
        _colorDepth = Row("Colors", Theme.Combo());
        _colorDepth.Items.AddRange(["32-bit", "24-bit", "16-bit", "15-bit"]);
        _colorDepth.SelectedIndex = s.ColorDepth switch { 24 => 1, 16 => 2, 15 => 3, _ => 0 };
        _resolution = Row("Session resolution", Theme.Combo(),
            "Applies to new connections. Screen size = sharp fullscreen; in a window the picture is scaled down.");
        _resolution.Items.AddRange(["Full screen size (recommended)", "Follow window size"]);
        _resolution.SelectedIndex = s.SessionUsesScreenSize ? 0 : 1;
        _dynamic = Row("Resize", Theme.Check("Try to change remote resolution when the window resizes", s.DynamicResize));
        _smart = Row("", Theme.Check("Smart sizing (scale picture when resolution can't change)", s.SmartSizing));
        _clipboard = Row("Redirect", Theme.Check("Clipboard", s.RedirectClipboard));
        _drives = Row("", Theme.Check("Local drives", s.RedirectDrives));
        _audio = Row("", Theme.Check("Play remote audio on this PC", s.PlayAudioLocally));
        _usKbd = Row("Keyboard", Theme.Check("Open sessions with US layout (needed for auto-type)", s.ForceUsKeyboard));
        _winKeys = Row("", Theme.Check("Send Windows-key combos (Alt+Tab, Win) to remote", s.WindowsKeysToRemote),
            "When on, RemoteHub's Ctrl+Alt shortcuts may stop working inside a session.");

        Section("App & security");
        _startFull = Row("Startup", Theme.Check("Start in fullscreen", s.StartFullscreen));
        _confirmClose = Row("Tabs", Theme.Check("Confirm before closing a connected tab", s.ConfirmCloseConnected));
        _autoLock = Row("Auto-lock (minutes)", Theme.Number(s.AutoLockMinutes, 0, 600),
            "Locks the app after inactivity. 0 = never. Needs a master password.");
        var masterBtn = Theme.Button(store.Vault.HasMasterPassword ? "Change / remove master password…" : "Set master password…");
        masterBtn.Click += (_, _) =>
        {
            using var dlg = new MasterPasswordDialog(_store);
            dlg.ShowDialog(this);
            masterBtn.Text = _store.Vault.HasMasterPassword ? "Change / remove master password…" : "Set master password…";
        };
        Row("Master password", masterBtn,
            "Without one, the vault is still encrypted and bound to your Windows account (DPAPI). With one, nobody can open the app on your unlocked PC.");
        var path = Theme.TextBox(Security.Vault.FilePath);
        path.ReadOnly = true;
        Row("Vault file", path);

        _diag = Row("Troubleshooting", Theme.Check("Write a diagnostic log (records screen detection + what is typed; no passwords)", Diag.Enabled));
        var diagBtn = Theme.Button("Open diagnostics folder…");
        diagBtn.Click += (_, _) => Diag.Open();
        Row("", diagBtn, "Turn this on, reproduce the problem once, then open this folder and send the log + cap_*.png images.");

        _template.TextChanged += (_, _) => UpdatePreview();
        _auth.TextChanged += (_, _) => UpdatePreview();
        _primaryUser.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var b = new BastionConfig { LoginTemplate = _template.Text, Authorization = _auth.Text, PrimaryUser = string.IsNullOrWhiteSpace(_primaryUser.Text) ? "user" : _primaryUser.Text };
        _preview.Text = Store.BuildBastionLogin(b, new Machine { Target = "Interactive@NFCore-172.16.170.221:RDP" });
    }

    protected override bool ValidateAndApply()
    {
        var b = _store.Data.Bastion;
        var s = _store.Data.Settings;
        b.Host = _host.Text.Trim();
        b.Port = (int)_port.Value;
        b.PrimaryUser = _primaryUser.Text.Trim();
        b.PrimaryPassword = _primaryPass.Text;
        b.Authorization = _auth.Text.Trim();
        b.LoginTemplate = string.IsNullOrWhiteSpace(_template.Text) ? "{target}:{auth}:{user}" : _template.Text.Trim();
        b.UseNla = _nla.Checked;
        b.InteractiveLoginUsesTarget = _interactiveTarget.Checked;
        b.AuthLevel = _authLevel.SelectedIndex;

        s.AutoFillOnConnect = _autoFill.Checked;
        s.AutoTypeEnabled = _autoType.Checked;
        s.AutoTypeDetectScreen = _detect.Checked;
        s.AutoTypeDelayMs = (int)_delay.Value;
        s.AutoTypeSequence = string.IsNullOrWhiteSpace(_sequence.Text) ? "{login}{TAB}{password}{ENTER}" : _sequence.Text;
        s.ColorDepth = _colorDepth.SelectedIndex switch { 1 => 24, 2 => 16, 3 => 15, _ => 32 };
        s.SessionUsesScreenSize = _resolution.SelectedIndex == 0;
        s.DynamicResize = _dynamic.Checked;
        s.SmartSizing = _smart.Checked;
        s.RedirectClipboard = _clipboard.Checked;
        s.RedirectDrives = _drives.Checked;
        s.PlayAudioLocally = _audio.Checked;
        s.ForceUsKeyboard = _usKbd.Checked;
        s.WindowsKeysToRemote = _winKeys.Checked;
        s.StartFullscreen = _startFull.Checked;
        s.ConfirmCloseConnected = _confirmClose.Checked;
        s.AutoLockMinutes = (int)_autoLock.Value;
        Diag.Enabled = _diag.Checked;
        s.DiagEnabled = _diag.Checked;
        _store.Save();
        return true;
    }
}

// =====================================================================================================
// Credential profiles
// =====================================================================================================
internal sealed class CredentialsDialog : DialogBase
{
    private readonly Store _store;
    private readonly List<CredentialProfile> _items;
    private readonly ListBox _list;
    private readonly TextBox _name, _user, _pass;
    private string? _defaultId;
    private CredentialProfile? _current;
    private bool _loading;

    public CredentialsDialog(Store store) : base("Credential profiles", 600)
    {
        _store = store;
        _defaultId = store.Data.Settings.DefaultCredentialId ?? store.Data.Credentials.FirstOrDefault()?.Id;
        _items = store.Data.Credentials.Select(c => new CredentialProfile { Id = c.Id, Name = c.Name, Username = c.Username, Password = c.Password }).ToList();

        Note("Login/password that RemoteHub types into the bastion's \"Target Information Required\" screen (step 2).\n" +
             "Machines use the ★ default profile unless you pick another one for a machine.");

        _list = new ListBox
        {
            BackColor = Theme.Surface2, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Ui,
            Height = Theme.S(150), IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.S(26),
        };
        _list.DrawItem += DrawProfile;
        Row("Profiles", _list);

        var bar = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, WrapContents = false };
        var add = Theme.Button("New");
        var del = Theme.Button("Delete");
        var def = Theme.Button("★ Make default");
        bar.Controls.AddRange([add, del, def]);
        Row("", bar);

        _name = Row("Profile name", Theme.TextBox());
        _user = Row("Login", Theme.TextBox(), "Exactly what you type in the Login field, e.g. DOMAIN\\user or user@domain.");
        _pass = PasswordRow("Password", "");

        add.Click += (_, _) =>
        {
            var p = new CredentialProfile { Name = _items.Count == 0 ? "Default" : $"Profile {_items.Count + 1}" };
            _items.Add(p);
            if (_items.Count == 1) _defaultId = p.Id;
            Reload(p);
            _name.Focus();
            _name.SelectAll();
        };
        del.Click += (_, _) =>
        {
            if (_current == null) return;
            if (store.Data.Machines.Any(m => m.CredentialId == _current.Id) &&
                MessageBox.Show(this, "Some machines use this profile; they will fall back to the default. Delete?", "Delete profile",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _items.Remove(_current);
            if (_defaultId == _current.Id) _defaultId = _items.FirstOrDefault()?.Id;
            Reload(_items.FirstOrDefault());
        };
        def.Click += (_, _) => { if (_current != null) { _defaultId = _current.Id; _list.Invalidate(); } };

        _list.SelectedIndexChanged += (_, _) => Show(_list.SelectedItem as CredentialProfile);
        _name.TextChanged += (_, _) => { if (!_loading && _current != null) { _current.Name = _name.Text; _list.Invalidate(); } };
        _user.TextChanged += (_, _) => { if (!_loading && _current != null) { _current.Username = _user.Text; _list.Invalidate(); } };
        _pass.TextChanged += (_, _) => { if (!_loading && _current != null) _current.Password = _pass.Text; };

        if (_items.Count == 0) add.PerformClick();
        else Reload(_items.FirstOrDefault(i => i.Id == _defaultId) ?? _items[0]);
    }

    private void DrawProfile(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var p = (CredentialProfile)_list.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(sel ? Theme.AccentDark : Theme.Surface2);
        e.Graphics.FillRectangle(bg, e.Bounds);
        var star = p.Id == _defaultId ? "★ " : "    ";
        TextRenderer.DrawText(e.Graphics, star + p.Name, Theme.UiBold, new Rectangle(e.Bounds.X + Theme.S(6), e.Bounds.Y, e.Bounds.Width / 2, e.Bounds.Height),
            p.Id == _defaultId ? Theme.Gold : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, p.Username, Theme.Ui, new Rectangle(e.Bounds.X + e.Bounds.Width / 2, e.Bounds.Y, e.Bounds.Width / 2 - Theme.S(6), e.Bounds.Height),
            Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void Reload(CredentialProfile? select)
    {
        _list.Items.Clear();
        foreach (var i in _items) _list.Items.Add(i);
        if (select != null) _list.SelectedItem = select;
        else Show(null);
    }

    private void Show(CredentialProfile? p)
    {
        _loading = true;
        _current = p;
        _name.Text = p?.Name ?? "";
        _user.Text = p?.Username ?? "";
        _pass.Text = p?.Password ?? "";
        _name.Enabled = _user.Enabled = _pass.Enabled = p != null;
        _loading = false;
    }

    protected override bool ValidateAndApply()
    {
        foreach (var i in _items.Where(i => string.IsNullOrWhiteSpace(i.Name))) i.Name = i.Username;
        _store.Data.Credentials = _items;
        _store.Data.Settings.DefaultCredentialId = _items.Any(i => i.Id == _defaultId) ? _defaultId : _items.FirstOrDefault()?.Id;
        var ids = _items.Select(i => i.Id).ToHashSet();
        foreach (var m in _store.Data.Machines)
            if (!string.IsNullOrEmpty(m.CredentialId) && m.CredentialId != Machine.NoCredential && !ids.Contains(m.CredentialId))
                m.CredentialId = null;
        _store.Save();
        return true;
    }
}

// =====================================================================================================
// Add / edit machine
// =====================================================================================================
internal sealed class MachineDialog : DialogBase
{
    private readonly Store _store;
    private readonly Machine _machine;
    private readonly bool _isNew;
    private readonly TextBox _name, _ip, _target, _notes;
    private readonly ComboBox _mode, _group, _cred, _autoType;
    private readonly CheckBox _fav;
    private bool _targetEdited;

    public Machine Machine => _machine;

    public MachineDialog(Store store, Machine? machine) : base(machine == null ? "Add machine" : "Edit machine", 600)
    {
        _store = store;
        _isNew = machine == null;
        _machine = machine ?? new Machine();

        _name = Row("Name", Theme.TextBox(_machine.Name));
        _ip = Row("IP / host", Theme.TextBox(_machine.Ip), "For direct mode you can add a port: 10.0.0.5:3390");
        _mode = Row("Connect", Theme.Combo());
        _mode.Items.AddRange(["Through bastion (WALLIX)", "Direct RDP (no bastion)"]);
        _mode.SelectedIndex = (int)_machine.Mode;
        _target = Row("WALLIX target", Theme.TextBox(_machine.Target),
            "Exactly as shown in the WALLIX list, e.g. Interactive@NFCore-172.16.170.221:RDP. Leave empty to open the WALLIX list.");
        _target.Font = Theme.Mono;
        _targetEdited = !string.IsNullOrEmpty(_machine.Target) && _machine.Target != Machine.MakeTarget(_machine.Name, _machine.Ip);

        _group = Row("Group", Theme.Combo(editable: true));
        foreach (var g in store.Groups) _group.Items.Add(g);
        _group.Text = _machine.Group;

        _cred = Row("Credentials", Theme.Combo());
        _cred.Items.Add("★ Default profile");
        foreach (var c in store.Data.Credentials) _cred.Items.Add(c);
        _cred.Items.Add("None (I type them myself)");
        _cred.SelectedIndex = _machine.CredentialId switch
        {
            null or "" => 0,
            Machine.NoCredential => _cred.Items.Count - 1,
            var id => Math.Max(0, store.Data.Credentials.FindIndex(c => c.Id == id) + 1),
        };

        _autoType = Row("Auto-type", Theme.Combo());
        _autoType.Items.AddRange(["Automatic (Interactive@ targets)", "Always", "Never"]);
        _autoType.SelectedIndex = (int)_machine.AutoType;

        _fav = Row("", Theme.Check("★ Favorite", _machine.Favorite));
        _notes = Row("Notes", Theme.TextBox(_machine.Notes));
        _notes.Multiline = true;
        _notes.Height = Theme.S(60);

        _name.TextChanged += (_, _) => AutoTarget();
        _ip.TextChanged += (_, _) => AutoTarget();
        _target.TextChanged += (_, _) => { if (_target.Focused) _targetEdited = true; };
        _mode.SelectedIndexChanged += (_, _) => _target.Enabled = _mode.SelectedIndex == 0;
        _target.Enabled = _mode.SelectedIndex == 0;
        if (_isNew) OkButton.Text = "Add";
    }

    private void AutoTarget()
    {
        if (_targetEdited || string.IsNullOrWhiteSpace(_name.Text)) return;
        _target.Text = Machine.MakeTarget(_name.Text.Trim(), _ip.Text.Trim());
    }

    protected override bool ValidateAndApply()
    {
        if (string.IsNullOrWhiteSpace(_name.Text)) { Warn(this, "Please enter a name."); return false; }
        if (_mode.SelectedIndex == 1 && string.IsNullOrWhiteSpace(_ip.Text)) { Warn(this, "Direct mode needs an IP or host."); return false; }

        _machine.Name = _name.Text.Trim();
        _machine.Ip = _ip.Text.Trim();
        _machine.Mode = (ConnectionMode)_mode.SelectedIndex;
        _machine.Target = _target.Text.Trim();
        _machine.Group = _group.Text.Trim();
        _machine.CredentialId = _cred.SelectedIndex switch
        {
            0 => null,
            var i when i == _cred.Items.Count - 1 => Machine.NoCredential,
            var i => ((CredentialProfile)_cred.Items[i]!).Id,
        };
        _machine.AutoType = (AutoTypeMode)_autoType.SelectedIndex;
        _machine.Favorite = _fav.Checked;
        _machine.Notes = _notes.Text;
        if (_isNew) _store.Data.Machines.Add(_machine);
        _store.Save();
        return true;
    }
}

// =====================================================================================================
// Import from the WALLIX list (paste)
// =====================================================================================================
internal sealed class ImportDialog : DialogBase
{
    private static readonly Regex TargetRx = new(
        @"(?<target>(?<account>[^\s@]+)@(?<device>[^\s:@]+):(?<proto>[A-Za-z0-9_]+))", RegexOptions.Compiled);
    private static readonly Regex IpSuffix = new(@"^(?<name>.+?)-(?<ip>\d{1,3}(?:\.\d{1,3}){3})$", RegexOptions.Compiled);

    private readonly Store _store;
    private readonly TextBox _text;
    private readonly ComboBox _group, _cred;
    private readonly Label _status;
    private List<Machine> _parsed = new();

    public int Imported { get; private set; }

    public ImportDialog(Store store) : base("Import machines", 760)
    {
        _store = store;
        Note("Paste the WALLIX target list (copy the rows, or type one per line). Every line containing something like\n" +
             "Interactive@NFCore-172.16.170.221:RDP becomes a machine. Existing targets are skipped.");
        _text = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, WordWrap = false,
            BackColor = Theme.Surface2, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Mono, Height = Theme.S(260),
        };
        Theme.UseDarkScrollbars(_text);
        Row("Targets", _text);
        _group = Row("Group (optional)", Theme.Combo(editable: true), "Leave empty to auto-group by name prefix (NF…, IRPT…, PTN-…).");
        foreach (var g in store.Groups) _group.Items.Add(g);
        _cred = Row("Credentials", Theme.Combo());
        _cred.Items.Add("★ Default profile");
        foreach (var c in store.Data.Credentials) _cred.Items.Add(c);
        _cred.SelectedIndex = 0;
        _status = Row("", Theme.Label("Nothing to import yet.", dim: true));
        _text.TextChanged += (_, _) => Parse();
        OkButton.Text = "Import";
    }

    private void Parse()
    {
        var existing = _store.Data.Machines.Select(m => m.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _parsed = new();
        int skippedProto = 0, dup = 0;
        foreach (Match m in TargetRx.Matches(_text.Text))
        {
            var target = m.Groups["target"].Value;
            if (!m.Groups["proto"].Value.Equals("RDP", StringComparison.OrdinalIgnoreCase)) { skippedProto++; continue; }
            if (existing.Contains(target) || !seen.Add(target)) { dup++; continue; }
            var device = m.Groups["device"].Value;
            string name = device, ip = "";
            var ipm = IpSuffix.Match(device);
            if (ipm.Success) { name = ipm.Groups["name"].Value; ip = ipm.Groups["ip"].Value; }
            _parsed.Add(new Machine { Name = name, Ip = ip, Target = target, Group = GuessGroup(name) });
        }
        _status.Text = $"{_parsed.Count} new machine(s)" +
                       (dup > 0 ? $", {dup} already present" : "") +
                       (skippedProto > 0 ? $", {skippedProto} non-RDP skipped" : "");
        _status.ForeColor = _parsed.Count > 0 ? Theme.Green : Theme.TextDim;
    }

    internal static string GuessGroup(string name)
    {
        if (name.StartsWith("PTN", StringComparison.OrdinalIgnoreCase)) return "PTN";
        foreach (var k in new[] { "IRPT", "CSDI", "NF", "RC" })
            if (name.StartsWith(k, StringComparison.Ordinal)) return k;

        // Leading run of capitals ("NFCore" -> "NF"), else the first capitalised word ("IrenexAPI" -> "Irenex").
        var caps = Regex.Match(name, @"^[A-Z]+").Value;
        if (caps.Length < name.Length && caps.Length > 0 && char.IsLower(name[caps.Length])) caps = caps[..^1];
        if (caps.Length >= 2) return caps;
        var word = Regex.Match(name, @"^[A-Z][a-z]+");
        return word.Success ? word.Value : "Other";
    }

    protected override bool ValidateAndApply()
    {
        Parse();
        if (_parsed.Count == 0) { Warn(this, "No new WALLIX targets found in the text."); return false; }
        string? credId = _cred.SelectedIndex > 0 ? ((CredentialProfile)_cred.SelectedItem!).Id : null;
        foreach (var m in _parsed)
        {
            if (!string.IsNullOrWhiteSpace(_group.Text)) m.Group = _group.Text.Trim();
            m.CredentialId = credId;
            _store.Data.Machines.Add(m);
        }
        Imported = _parsed.Count;
        _store.Save();
        return true;
    }
}

// =====================================================================================================
// Master password (set / change / remove) and unlock
// =====================================================================================================
internal sealed class MasterPasswordDialog : DialogBase
{
    private readonly Store _store;
    private readonly TextBox? _current;
    private readonly TextBox _new1, _new2;

    public MasterPasswordDialog(Store store) : base("Master password", 520)
    {
        _store = store;
        Note("The master password is never stored; it unlocks the vault key (PBKDF2-SHA256, 600k rounds + AES-GCM).\n" +
             "If you forget it, the saved data cannot be recovered.");
        if (store.Vault.HasMasterPassword) _current = PasswordRow("Current", "");
        _new1 = PasswordRow("New password", "", store.Vault.HasMasterPassword ? "Leave both empty to remove the master password." : null);
        _new2 = PasswordRow("Repeat", "");
    }

    protected override bool ValidateAndApply()
    {
        if (_current != null && !_store.Vault.VerifyMasterPassword(_current.Text)) { Warn(this, "Current password is wrong."); return false; }
        if (_new1.Text != _new2.Text) { Warn(this, "The new passwords don't match."); return false; }
        if (_new1.Text.Length > 0 && _new1.Text.Length < 6) { Warn(this, "Use at least 6 characters."); return false; }
        if (_new1.Text.Length == 0 && _current == null) { Warn(this, "Enter a password."); return false; }
        _store.Vault.SetMasterPassword(_new1.Text, _store.Data);
        return true;
    }
}

internal sealed class UnlockDialog : DialogBase
{
    private readonly TextBox _pass;
    private readonly Func<string, bool> _tryUnlock;
    private int _attempts;

    public UnlockDialog(Func<string, bool> tryUnlock) : base($"{App.Name} — Unlock", 460)
    {
        _tryUnlock = tryUnlock;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Note("Enter your master password to open the vault.");
        _pass = PasswordRow("Master password", "");
        OkButton.Text = "Unlock";
        CancelBtn.Text = "Exit";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _pass.Focus();
    }

    protected override bool ValidateAndApply()
    {
        Cursor = Cursors.WaitCursor;
        bool ok = _tryUnlock(_pass.Text);
        Cursor = Cursors.Default;
        if (ok) return true;
        _attempts++;
        _pass.SelectAll();
        if (_attempts >= 5) Thread.Sleep(2000 * (_attempts - 4)); // slow down guessing
        Warn(this, "Wrong password.");
        return false;
    }
}
