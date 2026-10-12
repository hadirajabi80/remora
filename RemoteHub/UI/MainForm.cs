using System.Runtime.InteropServices;

namespace RemoteHub.UI;

internal sealed class MainForm : Form
{
    private readonly Store _store;
    private readonly ToolTip _tt = new();

    private readonly Panel _top;
    private readonly TabStrip _tabs;
    private readonly Panel _sidebar;
    private readonly Splitter _splitter;
    private readonly Panel _content;
    private readonly Panel _welcome;
    private readonly TreeView _tree;
    private readonly TextBox _search;
    private readonly Label _machineCount;
    private readonly Label _btnFullscreen;

    private readonly Label _toast;
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 5000 };
    private readonly System.Windows.Forms.Timer _revealTimer = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer _idleTimer = new() { Interval = 15000 };

    private readonly Panel _lock;
    private readonly TextBox _lockPass;
    private bool _locked;

    private bool _fullscreen;
    private Rectangle _savedBounds;
    private FormWindowState _savedState;
    private bool _sidebarWasVisible = true;
    private bool _menuOpen;
    private MiniFsBar? _mini;

    private readonly HashSet<string> _collapsedGroups = new(StringComparer.OrdinalIgnoreCase);

    public MainForm(Store store)
    {
        _store = store;
        AutoScaleMode = AutoScaleMode.None;
        Text = App.Name;
        Icon = Theme.AppIcon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        KeyPreview = true;
        MinimumSize = new Size(Theme.S(900), Theme.S(560));
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Size = new Size(Math.Min(Theme.S(1500), wa.Width - 80), Math.Min(Theme.S(920), wa.Height - 60));
        StartPosition = FormStartPosition.CenterScreen;

        // ---------------- top bar: [sidebar] [tabs ...........] [actions]
        _top = new Panel { Dock = DockStyle.Top, Height = Theme.S(42), BackColor = Theme.Bg };
        _tabs = new TabStrip { Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Bg,
            Padding = new Padding(0, Theme.S(4), Theme.S(4), 0),
        };
        var btnSidebar = Theme.IconButton(Theme.IcoSidebar, "Show / hide machine list  (Ctrl+Alt+B)", _tt);
        btnSidebar.Dock = DockStyle.Left;
        btnSidebar.Click += (_, _) => ToggleSidebar();
        var btnBastion = Theme.IconButton(Theme.IcoBastion, "Open the WALLIX list (selector)", _tt);
        btnBastion.Click += (_, _) => OpenSelector();
        var btnType = Theme.IconButton(Theme.IcoKeyboard, "Fill a login on the current screen  (Ctrl+Alt+K)", _tt);
        btnType.Click += (_, _) => ShowFillPicker();
        _btnFullscreen = Theme.IconButton(Theme.IcoFullscreen, "Fullscreen  (Ctrl+Alt+Enter / F11)", _tt);
        _btnFullscreen.Click += (_, _) => ToggleFullscreen();
        var btnLock = Theme.IconButton(Theme.IcoLock, "Lock  (Ctrl+Alt+L)", _tt);
        btnLock.Click += (_, _) => Lock();
        var btnCreds = Theme.IconButton(Theme.IcoKey, "Step 2 login + password (typed into \"Target Information Required\")", _tt);
        btnCreds.Click += (_, _) => ShowCredentials();
        var btnSettings = Theme.IconButton(Theme.IcoSettings, "Settings (bastion / step 1 login)", _tt);
        btnSettings.Click += (_, _) => ShowSettings();
        actions.Controls.AddRange([btnBastion, btnType, _btnFullscreen, btnLock, btnCreds, btnSettings]);
        _top.Controls.Add(_tabs);
        _top.Controls.Add(actions);
        _top.Controls.Add(btnSidebar);

        // ---------------- sidebar
        _sidebar = new Panel { Dock = DockStyle.Left, Width = Theme.S(Math.Clamp(store.Data.Settings.SidebarWidth, 200, 600)), BackColor = Theme.Surface };
        var header = new Panel { Dock = DockStyle.Top, Height = Theme.S(44), BackColor = Theme.Surface, Padding = new Padding(Theme.S(14), Theme.S(12), Theme.S(10), 0) };
        var title = new Label { Text = "Machines", Font = Theme.Heading, ForeColor = Theme.Text, AutoSize = true, Dock = DockStyle.Left };
        _machineCount = new Label { Font = Theme.Small, ForeColor = Theme.TextDim, AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(0, Theme.S(3), 0, 0) };
        header.Controls.Add(title);
        header.Controls.Add(_machineCount);

        var searchWrap = new Panel { Dock = DockStyle.Top, Height = Theme.S(42), BackColor = Theme.Surface, Padding = new Padding(Theme.S(10), Theme.S(4), Theme.S(10), Theme.S(8)) };
        var searchBox = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface2, Padding = new Padding(Theme.S(8), Theme.S(6), Theme.S(6), 0) };
        var searchIcon = new Label { Text = Theme.IcoSearch, Font = Theme.Icon, ForeColor = Theme.TextDim, Dock = DockStyle.Left, Width = Theme.S(24) };
        _search = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface2, ForeColor = Theme.Text, PlaceholderText = "Search  (Ctrl+F)" };
        searchBox.Controls.Add(_search);
        searchBox.Controls.Add(searchIcon);
        searchWrap.Controls.Add(searchBox);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
            FullRowSelect = true, ShowLines = false, ShowPlusMinus = false, ShowRootLines = false, HideSelection = false,
            ItemHeight = Theme.S(30), DrawMode = TreeViewDrawMode.OwnerDrawAll, Indent = Theme.S(12), Font = Theme.Ui,
        };
        Theme.UseDarkScrollbars(_tree);
        _tree.DrawNode += DrawNode;
        _tree.NodeMouseDoubleClick += (_, e) => { if (e.Node?.Tag is Machine m) Open(m, forceNew: true); };
        _tree.NodeMouseClick += TreeClick;
        _tree.KeyDown += TreeKeyDown;
        _tree.AfterCollapse += (_, e) => { if (e.Node?.Tag is string g) _collapsedGroups.Add(g); };
        _tree.AfterExpand += (_, e) => { if (e.Node?.Tag is string g) _collapsedGroups.Remove(g); };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Theme.S(52), BackColor = Theme.Surface, Padding = new Padding(Theme.S(8), Theme.S(8), Theme.S(8), Theme.S(8)), WrapContents = false };
        var btnAdd = Theme.Button("＋ Add", primary: true);
        btnAdd.Click += (_, _) => EditMachine(null);
        var btnImport = Theme.Button("Import list");
        btnImport.Click += (_, _) => ShowImport();
        bottom.Controls.AddRange([btnAdd, btnImport]);

        _sidebar.Controls.Add(_tree);
        _sidebar.Controls.Add(bottom);
        _sidebar.Controls.Add(searchWrap);
        _sidebar.Controls.Add(header);

        _splitter = new Splitter { Dock = DockStyle.Left, Width = Theme.S(3), BackColor = Theme.Border, MinSize = Theme.S(200) };
        _splitter.SplitterMoved += (_, _) =>
        {
            _store.Data.Settings.SidebarWidth = (int)(_sidebar.Width / Theme.Scale);
            try { _store.Save(notify: false); } catch { /* ignore */ }
        };

        // ---------------- content
        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        _welcome = BuildWelcome();
        _content.Controls.Add(_welcome);

        Controls.Add(_content);
        Controls.Add(_splitter);
        Controls.Add(_sidebar);
        Controls.Add(_top);

        // ---------------- toast
        _toast = new Label
        {
            BackColor = Theme.Surface2, ForeColor = Theme.Text, Font = Theme.Ui, AutoSize = true, Visible = false,
            Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(14), Theme.S(10)), MaximumSize = new Size(Theme.S(480), 0),
        };
        _toast.Click += (_, _) => _toast.Visible = false;
        Controls.Add(_toast);
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _toast.Visible = false; };

        // ---------------- lock screen
        (_lock, _lockPass) = BuildLockScreen();
        Controls.Add(_lock);

        // ---------------- wiring
        _tabs.SelectedChanged += (_, _) => ShowSelected();
        _tabs.CloseRequested += (_, t) => CloseTab(t);
        _tabs.ContextRequested += (_, t) => TabMenu(t);
        _tabs.AddRequested += (_, _) => QuickConnect();
        _tabs.DetachRequested += (_, e) => DetachTab(e.tab, e.screen);
        _search.TextChanged += (_, _) => RebuildTree();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && _tree.Nodes.Count > 0) { _tree.Focus(); _tree.SelectedNode = FirstMachineNode(); e.Handled = true; }
            if (e.KeyCode == Keys.Enter && FirstMachineNode()?.Tag is Machine m) { Open(m, forceNew: true); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Escape) { _search.Clear(); e.SuppressKeyPress = true; }
        };
        _store.Changed += (_, _) => RebuildTree();
        _revealTimer.Tick += RevealTick;
        _idleTimer.Tick += IdleTick;
        _idleTimer.Start();

        RebuildTree();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_store.Data.Settings.StartFullscreen) ToggleFullscreen();
        if (string.IsNullOrWhiteSpace(_store.Data.Bastion.Host))
            BeginInvoke(() => Toast("Welcome! Start with ⚙ Settings (bastion address + primary login), then 🔑 credential profiles, then Import list."));
    }

    // =========================================================================================== sessions

    private readonly List<SessionWindow> _detached = new();

    private IEnumerable<SessionView> TabSessions => _tabs.Tabs.Select(t => (SessionView)t.Tag!);
    private IEnumerable<SessionView> Sessions => TabSessions.Concat(_detached.Select(w => w.View));
    private SessionView? CurrentSession => _tabs.Selected?.Tag as SessionView;

    private void Open(Machine m, bool forceNew = false)
    {
        if (_locked) return;
        if (string.IsNullOrWhiteSpace(_store.Data.Bastion.Host) && m.Mode == ConnectionMode.Bastion)
        {
            Toast("First enter the bastion address and your step-1 login (⚙ Settings).");
            ShowSettings();
            if (string.IsNullOrWhiteSpace(_store.Data.Bastion.Host)) return;
        }
        if ((m.IsInteractiveTarget || string.IsNullOrEmpty(m.Target)) && m.Mode == ConnectionMode.Bastion &&
            m.CredentialId != Machine.NoCredential && _store.Data.Credentials.Count == 0)
        {
            var answer = MessageBox.Show(this,
                "You haven't saved the login + password for step 2 (the \"Target Information Required\" screen) yet.\n\n" +
                "Add them now? RemoteHub will then fill that screen for you automatically.",
                "Step 2 login", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes) ShowCredentials();
        }
        if (!forceNew)
        {
            var existing = Sessions.FirstOrDefault(s => s.Machine == m);
            if (existing != null) { _tabs.Selected = existing.Tab; return; }
        }

        var view = new SessionView(_store, m) { Visible = false };
        view.StatusChanged += (_, _) => { _tabs.Invalidate(); _tree.Invalidate(); };
        view.Notify += (_, text) => Toast(text);
        view.CloseTabRequested += (_, _) => CloseTab(view.Tab, confirm: false);
        view.Closed += (_, _) =>
        {
            var win = _detached.FirstOrDefault(w => w.View == view);
            if (win != null) { _detached.Remove(win); win.ReleaseView(); win.Close(); }
            _content.Controls.Remove(view);
            view.Dispose();
            _tree.Invalidate();
        };
        // Number duplicate tabs so two sessions to the same machine are distinguishable.
        int dupes = Sessions.Count(s => s.Machine.Name == m.Name);
        if (dupes > 0) view.Tab.Title = $"{m.Name} {dupes + 1}";

        _content.Controls.Add(view);
        _tabs.Add(view.Tab, select: true);
        _content.PerformLayout();
        view.Connect();
    }

    private void OpenSelector()
    {
        if (string.IsNullOrWhiteSpace(_store.Data.Bastion.Host)) { ShowSettings(); return; }
        Open(new Machine { Name = "WALLIX list", Target = "", AutoType = AutoTypeMode.Always }, forceNew: true);
    }

    private void ShowSelected()
    {
        var sel = CurrentSession;
        if (sel != null)
        {
            sel.Visible = true;
            sel.BringToFront();
        }
        foreach (var s in Sessions)
            if (s != sel) s.Visible = false;
        _welcome.Visible = sel == null;
        if (_welcome.Visible) _welcome.BringToFront();
        Text = sel != null ? $"{sel.Machine.Name} — {App.Name}" : App.Name;
        sel?.FocusRemote();
        _tree.Invalidate();
    }

    private void CloseTab(TabItem t, bool confirm = true)
    {
        var v = (SessionView)t.Tag!;
        if (confirm && v.IsConnected && _store.Data.Settings.ConfirmCloseConnected &&
            MessageBox.Show(this, $"Disconnect from {v.Machine.Name}?", "Close tab", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _tabs.Remove(t);
        v.Visible = false;
        v.CloseSession();
    }

    private void TabMenu(TabItem t)
    {
        var v = (SessionView)t.Tag!;
        var menu = Theme.Menu();
        menu.Items.Add("Reconnect  (Ctrl+Alt+R)", null, (_, _) => v.Connect());
        menu.Items.Add("Fill a login…  (Ctrl+Alt+K)", null, (_, _) => ShowFillPicker());
        menu.Items.Add("Open in separate window", null, (_, _) => DetachTab(t));
        menu.Items.Add("Open another session", null, (_, _) => Open(v.Machine, forceNew: true));
        if (_store.Data.Machines.Contains(v.Machine))
            menu.Items.Add("Edit machine…", null, (_, _) => EditMachine(v.Machine));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Disconnect", null, (_, _) => v.Disconnect()).Enabled = v.IsConnected;
        menu.Items.Add("Close  (Ctrl+Alt+W)", null, (_, _) => CloseTab(t));
        menu.Items.Add("Close other tabs", null, (_, _) =>
        {
            foreach (var other in _tabs.Tabs.Where(x => x != t).ToList()) CloseTab(other, confirm: false);
        });
        ShowMenu(menu, _tabs, _tabs.PointToClient(Cursor.Position));
    }

    private void ShowMenu(ContextMenuStrip menu, Control c, Point p)
    {
        _menuOpen = true;
        menu.Closed += (_, _) => { _menuOpen = false; BeginInvoke(menu.Dispose); };
        menu.Show(c, p);
    }

    // =========================================================================================== detach / reattach

    /// <summary>Tears a tab off into its own window (drag out, or the tab menu), for use on another monitor.</summary>
    private void DetachTab(TabItem t, Point? dropScreen = null)
    {
        if (_fullscreen) ToggleFullscreen();
        var v = (SessionView)t.Tag!;
        _tabs.Remove(t);
        _content.Controls.Remove(v);

        var win = new SessionWindow(v, (sv, owner) => ShowFillPickerFor(sv, owner), _tt);
        win.DockBackRequested += (_, sv) => ReattachSession(sv);
        win.CloseSessionRequested += (_, sv) =>
        {
            var w = _detached.FirstOrDefault(x => x.View == sv);
            if (w != null) _detached.Remove(w);
            if (sv.IsConnected && _store.Data.Settings.ConfirmCloseConnected &&
                MessageBox.Show(win, $"Disconnect from {sv.Machine.Name}?", "Close session", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                // user canceled: re-dock instead of closing
                BeginInvoke(() => ReattachSession(sv));
                return;
            }
            sv.CloseSession();
        };
        _detached.Add(win);

        // Place the new window on the monitor under the drop point, or offset from the main window.
        var target = dropScreen != null ? Screen.FromPoint(dropScreen.Value) : NextMonitor();
        win.StartPosition = FormStartPosition.Manual;
        win.Location = new Point(
            target.WorkingArea.X + (target.WorkingArea.Width - win.Width) / 2,
            target.WorkingArea.Y + (target.WorkingArea.Height - win.Height) / 2);
        win.Show();
        win.Activate();
        _tree.Invalidate();
    }

    private Screen NextMonitor()
    {
        var mine = Screen.FromControl(this);
        return Screen.AllScreens.FirstOrDefault(s => !s.Equals(mine)) ?? mine;
    }

    private void ReattachSession(SessionView v)
    {
        var win = _detached.FirstOrDefault(w => w.View == v);
        if (win == null) return;
        _detached.Remove(win);
        win.ReleaseView();          // detach the view so closing the window won't dispose it
        win.Close();

        v.Visible = false;
        _content.Controls.Add(v);
        _tabs.Add(v.Tab, select: true);
        _content.PerformLayout();
        _tree.Invalidate();
    }

    // =========================================================================================== machine list

    private void RebuildTree()
    {
        var machines = _store.Data.Machines;
        _machineCount.Text = machines.Count.ToString();
        var filter = _search.Text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var selected = _tree.SelectedNode?.Tag as Machine;

        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        TreeNode AddGroup(string key, string text, IEnumerable<Machine> items)
        {
            var list = items.ToList();
            var g = new TreeNode($"{text}") { Tag = key, Name = list.Count.ToString() };
            foreach (var m in list) g.Nodes.Add(new TreeNode(m.Name) { Tag = m });
            _tree.Nodes.Add(g);
            if (!_collapsedGroups.Contains(key)) g.Expand();
            return g;
        }

        if (filter.Length > 0)
        {
            var hits = machines.Where(m =>
            {
                var hay = $"{m.Name} {m.Ip} {m.Group} {m.Target}".ToLowerInvariant();
                return filter.All(hay.Contains);
            }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
            var g = AddGroup("results", "Results", hits);
            g.Expand();
        }
        else
        {
            var favs = machines.Where(m => m.Favorite).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (favs.Count > 0) AddGroup("★fav", "★ Favorites", favs);
            var recent = machines.Where(m => m.LastUsed != null).OrderByDescending(m => m.LastUsed).Take(6).ToList();
            if (recent.Count > 0) AddGroup("⏱recent", "Recent", recent);
            foreach (var grp in machines.GroupBy(m => string.IsNullOrWhiteSpace(m.Group) ? "Ungrouped" : m.Group, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key == "Ungrouped" ? 1 : 0).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                AddGroup(grp.Key, grp.Key, grp.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));
        }

        if (selected != null)
            _tree.SelectedNode = AllNodes().FirstOrDefault(n => n.Tag == selected);
        _tree.EndUpdate();
    }

    private IEnumerable<TreeNode> AllNodes()
    {
        foreach (TreeNode g in _tree.Nodes)
        {
            yield return g;
            foreach (TreeNode n in g.Nodes) yield return n;
        }
    }

    private TreeNode? FirstMachineNode() => AllNodes().FirstOrDefault(n => n.Tag is Machine);

    private void DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null || e.Bounds.Height <= 0) return;
        var g = e.Graphics;
        var r = new Rectangle(0, e.Bounds.Y, _tree.ClientSize.Width, e.Bounds.Height);
        bool sel = (e.State & TreeNodeStates.Selected) != 0;
        bool hot = (e.State & TreeNodeStates.Hot) != 0;

        using (var bg = new SolidBrush(sel ? (_tree.Focused ? Theme.AccentDark : Theme.Hover) : Theme.Surface))
            g.FillRectangle(bg, r);

        if (e.Node.Tag is string)
        {
            var arrow = e.Node.IsExpanded ? "" : "";
            TextRenderer.DrawText(g, arrow, new Font("Segoe Fluent Icons", 7.5f), new Rectangle(Theme.S(10), r.Y, Theme.S(16), r.Height), Theme.TextDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            var text = e.Node.Text.ToUpperInvariant();
            var color = e.Node.Text.StartsWith('★') ? Theme.Gold : Theme.TextDim;
            TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(Theme.S(30), r.Y, r.Width - Theme.S(70), r.Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, e.Node.Name, Theme.Small, new Rectangle(r.Right - Theme.S(48), r.Y, Theme.S(38), r.Height), Theme.TextDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            return;
        }

        if (e.Node.Tag is not Machine m) return;
        int x = Theme.S(30);
        var open = Sessions.Where(s => s.Machine == m).ToList();
        if (open.Count > 0)
        {
            var color = open.Any(s => s.Tab.Status == TabStatus.Connected) ? Theme.Green
                : open.Any(s => s.Tab.Status == TabStatus.Connecting) ? Theme.Yellow : Theme.Red;
            using var b = new SolidBrush(color);
            int d = Theme.S(7);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(b, Theme.S(16), r.Y + (r.Height - d) / 2, d, d);
        }
        var nameSize = TextRenderer.MeasureText(g, m.Name, Theme.Ui);
        TextRenderer.DrawText(g, m.Name, Theme.Ui, new Rectangle(x, r.Y, r.Width - x - Theme.S(10), r.Height), Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        int ipX = x + nameSize.Width + Theme.S(2);
        var tag = m.Mode == ConnectionMode.Direct ? $"{m.Ip}  ⇢" : m.Ip;
        if (ipX < r.Width - Theme.S(60))
            TextRenderer.DrawText(g, tag, Theme.Small, new Rectangle(ipX, r.Y, r.Width - ipX - Theme.S(26), r.Height),
                sel ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (m.Favorite)
            TextRenderer.DrawText(g, "★", Theme.Small, new Rectangle(r.Right - Theme.S(24), r.Y, Theme.S(18), r.Height), Theme.Gold,
                TextFormatFlags.VerticalCenter);
    }

    private void TreeClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node == null) return;
        _tree.SelectedNode = e.Node;
        if (e.Button == MouseButtons.Left && e.Node.Tag is string)
        {
            if (e.Node.IsExpanded) e.Node.Collapse(); else e.Node.Expand();
            return;
        }
        if (e.Button != MouseButtons.Right) return;

        var menu = Theme.Menu();
        if (e.Node.Tag is Machine m)
        {
            menu.Items.Add("Connect (new tab)", null, (_, _) => Open(m, forceNew: true));
            var existing = Sessions.FirstOrDefault(s => s.Machine == m);
            if (existing != null)
                menu.Items.Add("Switch to open session", null, (_, _) => { _tabs.Selected = existing.Tab; });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Edit…", null, (_, _) => EditMachine(m));
            menu.Items.Add(m.Favorite ? "Remove from favorites" : "★ Add to favorites", null, (_, _) => { m.Favorite = !m.Favorite; _store.Save(); });
            menu.Items.Add("Copy IP", null, (_, _) => { if (!string.IsNullOrEmpty(m.Ip)) Clipboard.SetText(m.Ip); }).Enabled = !string.IsNullOrEmpty(m.Ip);
            menu.Items.Add("Copy WALLIX target", null, (_, _) => { if (!string.IsNullOrEmpty(m.Target)) Clipboard.SetText(m.Target); }).Enabled = !string.IsNullOrEmpty(m.Target);
            var cred = new ToolStripMenuItem("Credentials");
            AddCredItem(cred, "★ Default profile", null, m);
            foreach (var c in _store.Data.Credentials) AddCredItem(cred, c.Name, c.Id, m);
            AddCredItem(cred, "None (type myself)", Machine.NoCredential, m);
            menu.Items.Add(cred);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, (_, _) =>
            {
                if (MessageBox.Show(this, $"Delete {m.Name}?", "Delete machine", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _store.Data.Machines.Remove(m);
                _store.Save();
            });
        }
        else if (e.Node.Tag is string key && key != "results" && !key.StartsWith('★') && !key.StartsWith('⏱'))
        {
            var members = _store.Data.Machines.Where(x => string.Equals(string.IsNullOrWhiteSpace(x.Group) ? "Ungrouped" : x.Group, key, StringComparison.OrdinalIgnoreCase)).ToList();
            menu.Items.Add($"Connect all ({members.Count})", null, (_, _) =>
            {
                if (members.Count > 5 && MessageBox.Show(this, $"Open {members.Count} sessions?", "Connect all", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                foreach (var x in members) Open(x);
            });
            menu.Items.Add("Rename group…", null, (_, _) =>
            {
                using var dlg = new InputDialog("Rename group", "Name", key == "Ungrouped" ? "" : key);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                foreach (var x in members) x.Group = dlg.Value;
                _store.Save();
            });
            menu.Items.Add("Delete all machines in group…", null, (_, _) =>
            {
                if (MessageBox.Show(this, $"Delete {members.Count} machines in {key}?", "Delete group", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                foreach (var x in members) _store.Data.Machines.Remove(x);
                _store.Save();
            });
        }
        if (menu.Items.Count > 0) ShowMenu(menu, _tree, e.Location);
        else menu.Dispose();
    }

    private void AddCredItem(ToolStripMenuItem parent, string text, string? id, Machine m)
    {
        var item = new ToolStripMenuItem(text) { Checked = (m.CredentialId ?? "") == (id ?? "") };
        item.Click += (_, _) => { m.CredentialId = id; _store.Save(); };
        parent.DropDownItems.Add(item);
    }

    private void TreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tree.SelectedNode?.Tag is not Machine m) return;
        if (e.KeyCode == Keys.Enter) { Open(m); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.F2) EditMachine(m);
        else if (e.KeyCode == Keys.Delete)
        {
            if (MessageBox.Show(this, $"Delete {m.Name}?", "Delete machine", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _store.Data.Machines.Remove(m);
                _store.Save();
            }
        }
    }

    // =========================================================================================== dialogs

    private void EditMachine(Machine? m)
    {
        using var dlg = new MachineDialog(_store, m);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var s in Sessions.Where(s => s.Machine == dlg.Machine)) s.Tab.Title = dlg.Machine.Name;
            _tabs.Invalidate();
        }
    }

    private void ShowSettings()
    {
        using var dlg = new SettingsDialog(_store);
        dlg.ShowDialog(this);
    }

    private void ShowCredentials()
    {
        using var dlg = new CredentialsDialog(_store);
        dlg.ShowDialog(this);
    }

    private void ShowImport()
    {
        using var dlg = new ImportDialog(_store);
        if (dlg.ShowDialog(this) == DialogResult.OK) Toast($"Imported {dlg.Imported} machine(s).");
    }

    private void QuickConnect()
    {
        if (_locked) return;
        using var qc = new QuickConnectForm(_store);
        var origin = PointToScreen(new Point((ClientSize.Width - qc.Width) / 2, Theme.S(60)));
        qc.Location = origin;
        if (qc.ShowDialog(this) == DialogResult.OK && qc.Result != null) Open(qc.Result, forceNew: true);
    }

    private void ShowFillPicker() => ShowFillPickerFor(CurrentSession, this);

    /// <summary>The credential list: pick one and it is typed into whatever screen the session currently shows.</summary>
    public void ShowFillPickerFor(SessionView? session, Form owner)
    {
        if (_locked) return;
        if (session == null || !session.IsConnected) { Toast("Open a session first, then pick a login to fill."); return; }

        var entries = new List<FillEntry>();
        var b = _store.Data.Bastion;
        if (!string.IsNullOrWhiteSpace(b.PrimaryUser) && !string.IsNullOrEmpty(b.PrimaryPassword))
            entries.Add(new FillEntry($"{b.PrimaryUser}", "bastion / step 1", b.PrimaryUser, b.PrimaryPassword, IsBastion: true));
        foreach (var c in _store.Data.Credentials)
            entries.Add(new FillEntry(string.IsNullOrWhiteSpace(c.Name) ? c.Username : c.Name, c.Username, c.Username, c.Password));

        if (entries.Count == 0)
        {
            if (MessageBox.Show(owner, "No logins saved yet. Add them now?", "Fill login", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                ShowCredentials();
            return;
        }

        using var picker = new CredentialPicker(entries);
        picker.Location = owner.PointToScreen(new Point((owner.ClientSize.Width - picker.Width) / 2, Theme.S(60)));
        if (picker.ShowDialog(owner) == DialogResult.OK && picker.Result is { } f)
            session.FillLogin(f.Login, f.Password);
    }

    // =========================================================================================== fullscreen

    private void ToggleFullscreen()
    {
        if (_locked) return;
        SuspendLayout();
        if (!_fullscreen)
        {
            _fullscreen = true;
            _savedState = WindowState;
            _savedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _sidebarWasVisible = _sidebar.Visible;
            _sidebar.Visible = false;
            _splitter.Visible = false;
            _top.Visible = false;
            _top.Dock = DockStyle.None;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            _btnFullscreen.Text = Theme.IcoBackToWindow;
            _revealTimer.Start();
            _mini ??= new MiniFsBar(() => ToggleFullscreen(), () => { if (_tabs.Selected != null) CloseTab(_tabs.Selected); });
            _mini.ShowOn(Screen.FromControl(this), CurrentSession?.Machine.Name ?? App.Name, CurrentSession?.Tab.Status ?? TabStatus.Connected);
            Toast("Fullscreen — small controls are at the top (drag to move). Ctrl+Alt+Enter to exit.");
        }
        else
        {
            _fullscreen = false;
            _mini?.HideBar();
            _revealTimer.Stop();
            FormBorderStyle = FormBorderStyle.Sizable;
            _top.Dock = DockStyle.Top;
            _top.SendToBack(); // keep docking order: top bar first, then sidebar, then content
            _top.Visible = true;
            _sidebar.Visible = _sidebarWasVisible;
            _splitter.Visible = _sidebarWasVisible;
            Bounds = _savedBounds;
            WindowState = _savedState;
            _btnFullscreen.Text = Theme.IcoFullscreen;
        }
        ResumeLayout(true);
        CurrentSession?.FocusRemote();
    }

    private void RevealTick(object? sender, EventArgs e)
    {
        if (!_fullscreen || _locked) return;
        var p = Cursor.Position;
        var b = Bounds;
        if (!_top.Visible)
        {
            if (p.Y <= b.Top + 1 && p.X >= b.Left && p.X < b.Right && ActiveForm == this)
            {
                _top.Bounds = new Rectangle(0, 0, ClientSize.Width, Theme.S(42));
                _top.Visible = true;
                _top.BringToFront();
            }
        }
        else if (!_menuOpen)
        {
            var zone = RectangleToScreen(new Rectangle(0, 0, ClientSize.Width, _top.Height + Theme.S(40)));
            if (!zone.Contains(p)) _top.Visible = false;
        }
    }

    private void ToggleSidebar()
    {
        if (_fullscreen) { _sidebarWasVisible = !_sidebarWasVisible; return; }
        _sidebar.Visible = !_sidebar.Visible;
        _splitter.Visible = _sidebar.Visible;
    }

    // =========================================================================================== lock

    private (Panel, TextBox) BuildLockScreen()
    {
        var panel = new Panel { BackColor = Theme.Bg, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        var card = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Anchor = AnchorStyles.None, BackColor = Theme.Bg };
        var icon = new Label { Text = Theme.IcoLock, Font = new Font("Segoe Fluent Icons", 36f), ForeColor = Theme.Accent, AutoSize = true, Anchor = AnchorStyles.None };
        var t = new Label { Text = $"{App.Name} is locked", Font = Theme.Title, ForeColor = Theme.Text, AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.S(10), 0, Theme.S(4)) };
        var sub = new Label { Text = "Sessions stay connected in the background.", ForeColor = Theme.TextDim, AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, Theme.S(16)) };
        var pass = Theme.TextBox(password: true);
        pass.Width = Theme.S(300);
        pass.Anchor = AnchorStyles.None;
        pass.PlaceholderText = "Master password";
        var unlock = Theme.Button("Unlock", primary: true, width: 300);
        unlock.Anchor = AnchorStyles.None;
        unlock.Margin = new Padding(0, Theme.S(10), 0, 0);
        unlock.Click += (_, _) => TryUnlock();
        pass.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { TryUnlock(); e.SuppressKeyPress = true; } };
        card.Controls.AddRange([icon, t, sub, pass, unlock]);
        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Theme.Bg };
        center.Controls.Add(card);
        panel.Controls.Add(center);
        return (panel, pass);
    }

    private void Lock()
    {
        if (_locked) return;
        if (!_store.Vault.HasMasterPassword)
        {
            Toast("Set a master password first (⚙ Settings → Master password).");
            return;
        }
        _locked = true;
        _toast.Visible = false;
        _content.Visible = false;
        if (_fullscreen) _top.Visible = false;
        _lock.Bounds = ClientRectangle;
        _lock.Visible = true;
        _lock.BringToFront();
        _lockPass.Clear();
        _lockPass.Focus();
    }

    private void TryUnlock()
    {
        if (_store.Vault.VerifyMasterPassword(_lockPass.Text))
        {
            _locked = false;
            _lock.Visible = false;
            _content.Visible = true;
            _lockPass.Clear();
            CurrentSession?.FocusRemote();
        }
        else
        {
            _lockPass.SelectAll();
            Toast("Wrong password.");
        }
    }

    private void IdleTick(object? sender, EventArgs e)
    {
        int minutes = _store.Data.Settings.AutoLockMinutes;
        if (_locked || minutes <= 0 || !_store.Vault.HasMasterPassword) return;
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return;
        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime); // both wrap at 49.7 days
        if (idleMs > minutes * 60_000L) Lock();
    }

    // =========================================================================================== toast & welcome

    private void Toast(string text)
    {
        _toast.Text = text;
        _toast.Visible = true;
        _toast.Location = new Point(ClientSize.Width - _toast.Width - Theme.S(18), ClientSize.Height - _toast.Height - Theme.S(18));
        _toast.BringToFront();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_lock != null && _lock.Visible) _lock.Bounds = ClientRectangle;
        if (_toast != null && _toast.Visible)
            _toast.Location = new Point(ClientSize.Width - _toast.Width - Theme.S(18), ClientSize.Height - _toast.Height - Theme.S(18));
    }

    private Panel BuildWelcome()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        var card = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Anchor = AnchorStyles.None, BackColor = Theme.Bg };
        var logo = new PictureBox { Image = Theme.AppIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(Theme.S(64), Theme.S(64)), Anchor = AnchorStyles.None };
        var t = new Label { Text = App.Name, Font = new Font("Segoe UI Semibold", 24f), ForeColor = Theme.Text, AutoSize = true, Anchor = AnchorStyles.None };
        var ver = new Label
        {
            Text = $"v{Application.ProductVersion.Split('+')[0]}  ·  manual fill (Ctrl+Alt+K)", ForeColor = Theme.Accent, Font = Theme.Small,
            AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.S(2), 0, 0),
        };
        var sub = new Label
        {
            Text = "Double-click a machine, or press Ctrl+Alt+P and type its name.", ForeColor = Theme.TextDim, AutoSize = true,
            Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.S(4), 0, Theme.S(18)),
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, BackColor = Theme.Bg, WrapContents = false };
        var b2 = Theme.Button("① Step 1: bastion login");
        b2.Click += (_, _) => ShowSettings();
        var b3 = Theme.Button("② Step 2: machine login + password");
        b3.Click += (_, _) => ShowCredentials();
        var b4 = Theme.Button("③ Import machines");
        b4.Click += (_, _) => ShowImport();
        var b1 = Theme.Button("Quick connect", primary: true);
        b1.Click += (_, _) => QuickConnect();
        buttons.Controls.AddRange([b2, b3, b4, b1]);

        var keys = new Label
        {
            AutoSize = true, ForeColor = Theme.TextDim, Font = Theme.Mono, Anchor = AnchorStyles.None, Margin = new Padding(0, Theme.S(26), 0, 0),
            Text =
                "Ctrl+Alt+P        quick connect\n" +
                "Ctrl+Alt+Enter    fullscreen on / off\n" +
                "Ctrl+Alt+PgDn/Up  next / previous tab\n" +
                "Ctrl+Alt+1…9      jump to tab\n" +
                "Ctrl+Alt+K        pick a login to fill on screen\n" +
                "Ctrl+Alt+R        reconnect\n" +
                "Ctrl+Alt+W        close tab\n" +
                "Ctrl+Alt+B        show / hide machine list\n" +
                "Ctrl+Alt+L        lock",
        };
        card.Controls.AddRange([logo, t, ver, sub, buttons, keys]);
        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Theme.Bg };
        center.Controls.Add(card);
        panel.Controls.Add(center);
        return panel;
    }

    // =========================================================================================== keyboard

    private enum Hk
    {
        Fullscreen = 1, Quick, NextTab, PrevTab, TypeCreds, CloseTab, Sidebar, Lock, Reconnect,
        Tab1 = 11, Tab9 = 19,
    }

    private bool _hotkeysOn;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RegisterHotkeys(true);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        RegisterHotkeys(false); // never steal Ctrl+Alt combos from other apps
    }

    private void RegisterHotkeys(bool on)
    {
        if (on == _hotkeysOn || !IsHandleCreated) return;
        _hotkeysOn = on;
        var map = new (Hk id, Keys key)[]
        {
            (Hk.Fullscreen, Keys.Enter), (Hk.Quick, Keys.P), (Hk.NextTab, Keys.PageDown), (Hk.PrevTab, Keys.PageUp),
            (Hk.TypeCreds, Keys.K), (Hk.CloseTab, Keys.W), (Hk.Sidebar, Keys.B), (Hk.Lock, Keys.L), (Hk.Reconnect, Keys.R),
        };
        foreach (var (id, key) in map)
        {
            if (on) RegisterHotKey(Handle, (int)id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)key);
            else UnregisterHotKey(Handle, (int)id);
        }
        for (int i = 0; i < 9; i++)
        {
            if (on) RegisterHotKey(Handle, (int)Hk.Tab1 + i, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)(Keys.D1 + i));
            else UnregisterHotKey(Handle, (int)Hk.Tab1 + i);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            HandleHotkey((Hk)(int)m.WParam);
            return;
        }
        base.WndProc(ref m);
    }

    private void HandleHotkey(Hk id)
    {
        if (_locked) return;
        switch (id)
        {
            case Hk.Fullscreen: ToggleFullscreen(); break;
            case Hk.Quick: QuickConnect(); break;
            case Hk.NextTab: _tabs.SelectOffset(1); break;
            case Hk.PrevTab: _tabs.SelectOffset(-1); break;
            case Hk.TypeCreds: ShowFillPicker(); break;
            case Hk.CloseTab: if (_tabs.Selected != null) CloseTab(_tabs.Selected); break;
            case Hk.Sidebar: ToggleSidebar(); break;
            case Hk.Lock: Lock(); break;
            case Hk.Reconnect: CurrentSession?.Connect(); break;
            case >= Hk.Tab1 and <= Hk.Tab9: _tabs.SelectIndex(id - Hk.Tab1); break;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_locked) return base.ProcessCmdKey(ref msg, keyData);
        switch (keyData)
        {
            case Keys.F11: ToggleFullscreen(); return true;
            case Keys.Control | Keys.Tab: _tabs.SelectOffset(1); return true;
            case Keys.Control | Keys.Shift | Keys.Tab: _tabs.SelectOffset(-1); return true;
            case Keys.Control | Keys.F: if (!_sidebar.Visible) ToggleSidebar(); _search.Focus(); _search.SelectAll(); return true;
            case Keys.Control | Keys.N: EditMachine(null); return true;
            case Keys.Control | Keys.K: QuickConnect(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // =========================================================================================== shutdown

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        int connected = Sessions.Count(s => s.IsConnected);
        if (connected > 0 && e.CloseReason == CloseReason.UserClosing &&
            MessageBox.Show(this, $"Disconnect {connected} session(s) and exit?", App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        RegisterHotkeys(false);
        foreach (var s in Sessions.ToList()) s.Disconnect();
        foreach (var w in _detached.ToList()) { w.ReleaseView(); w.Close(); }
        _detached.Clear();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toastTimer.Dispose();
            _revealTimer.Dispose();
            _idleTimer.Dispose();
            _tt.Dispose();
            _mini?.Dispose();
        }
        base.Dispose(disposing);
    }

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
