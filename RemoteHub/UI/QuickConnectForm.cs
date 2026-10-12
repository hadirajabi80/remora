namespace RemoteHub.UI;

/// <summary>Command-palette style "type a few letters, press Enter" connector (Ctrl+Alt+P).</summary>
internal sealed class QuickConnectForm : Form
{
    private readonly Store _store;
    private readonly TextBox _search;
    private readonly ListBox _list;

    public Machine? Result { get; private set; }

    public QuickConnectForm(Store store)
    {
        _store = store;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Accent;
        Padding = new Padding(1);
        Size = new Size(Theme.S(600), Theme.S(440));
        KeyPreview = true;

        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(Theme.S(10)) };
        _search = new TextBox
        {
            Dock = DockStyle.Top, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 14f), PlaceholderText = "Connect to…  type a name, or any IP / host to open it directly",
        };
        var sep = new Panel { Dock = DockStyle.Top, Height = Theme.S(10), BackColor = Theme.Surface };
        _list = new ListBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.S(34), IntegralHeight = false, Font = Theme.Ui,
        };
        Theme.UseDarkScrollbars(_list);
        _list.DrawItem += DrawItem;
        _list.DoubleClick += (_, _) => Accept();
        inner.Controls.Add(_list);
        inner.Controls.Add(sep);
        inner.Controls.Add(_search);
        Controls.Add(inner);

        _search.TextChanged += (_, _) => Filter();
        Filter();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        _search.Focus();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (DialogResult == DialogResult.None) { DialogResult = DialogResult.Cancel; Close(); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); return true;
            case Keys.Enter: Accept(); return true;
            case Keys.Down: MoveSelection(1); return true;
            case Keys.Up: MoveSelection(-1); return true;
            case Keys.PageDown: MoveSelection(8); return true;
            case Keys.PageUp: MoveSelection(-8); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void MoveSelection(int d)
    {
        if (_list.Items.Count == 0) return;
        _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + d, 0, _list.Items.Count - 1);
    }

    private void Accept()
    {
        if (_list.SelectedItem is Machine m)
        {
            Result = m;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void Filter()
    {
        var tokens = _search.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var items = _store.Data.Machines
            .Where(m =>
            {
                var hay = $"{m.Name} {m.Ip} {m.Group} {m.Target}".ToLowerInvariant();
                return tokens.All(hay.Contains);
            })
            .OrderByDescending(m => tokens.Length > 0 && m.Name.StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.Favorite)
            .ThenByDescending(m => m.LastUsed ?? DateTime.MinValue)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _list.BeginUpdate();
        _list.Items.Clear();

        // Ad-hoc: let the user type any IP / host[:port] and connect directly, even if it's not saved.
        var typed = _search.Text.Trim();
        if (LooksLikeHost(typed) && !items.Any(m => m.Ip.Equals(typed, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(typed, StringComparison.OrdinalIgnoreCase)))
            _list.Items.Add(new Machine { Name = typed, Ip = typed, Mode = ConnectionMode.Direct, Target = "", CredentialId = Machine.NoCredential, IsAdHoc = true });

        foreach (var m in items) _list.Items.Add(m);
        _list.EndUpdate();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private static bool LooksLikeHost(string s)
    {
        if (s.Length < 3 || s.Contains(' ')) return false;
        var host = s.Split(':')[0];
        // an IPv4-ish address, or a hostname with a dot (foo.bar) — enough to avoid matching plain search words
        if (System.Net.IPAddress.TryParse(host, out _)) return true;
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var m = (Machine)_list.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(sel ? Theme.AccentDark : Theme.Surface))
            e.Graphics.FillRectangle(bg, e.Bounds);
        var r = e.Bounds;
        int x = r.X + Theme.S(10);
        if (m.IsAdHoc)
        {
            TextRenderer.DrawText(e.Graphics, "  Connect to " + m.Ip, Theme.UiBold, new Point(x, r.Y + (r.Height - Theme.S(18)) / 2), Theme.Accent);
            TextRenderer.DrawText(e.Graphics, "new, direct RDP", Theme.Small, new Rectangle(r.Right - Theme.S(160), r.Y, Theme.S(150), r.Height),
                sel ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            return;
        }
        var name = (m.Favorite ? "★ " : "") + m.Name;
        var nameSize = TextRenderer.MeasureText(name, Theme.UiBold);
        TextRenderer.DrawText(e.Graphics, name, Theme.UiBold, new Point(x, r.Y + (r.Height - nameSize.Height) / 2), m.Favorite ? Theme.Gold : Theme.Text);
        TextRenderer.DrawText(e.Graphics, m.Ip, Theme.Ui, new Rectangle(x + nameSize.Width + Theme.S(8), r.Y, Theme.S(200), r.Height),
            sel ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, m.Mode == ConnectionMode.Direct ? "direct" : m.Group, Theme.Small,
            new Rectangle(r.Right - Theme.S(160), r.Y, Theme.S(150), r.Height), sel ? Theme.Text : Theme.TextDim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class InputDialog : DialogBase
{
    private readonly TextBox _box;
    public string Value => _box.Text.Trim();

    public InputDialog(string title, string label, string value) : base(title, 480)
    {
        _box = Row(label, Theme.TextBox(value));
        OkButton.Text = "OK";
    }
}
