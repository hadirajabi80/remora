namespace RemoteHub.UI;

/// <summary>
/// A session torn off into its own top-level window, so it can live on another monitor. The SessionView
/// (and its live RDP control) is reparented in and out without disconnecting.
/// </summary>
internal sealed class SessionWindow : Form
{
    private readonly SessionView _view;
    private readonly Action<SessionView, Form> _showFill;
    private readonly Panel _bar;
    private readonly Label _title;
    private readonly Label _toast;
    private readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 5000 };
    private bool _fullscreen;
    private Rectangle _savedBounds;
    private FormWindowState _savedState;
    private MiniFsBar? _mini;

    public SessionView View => _view;

    /// <summary>Raised when the user asks to dock this session back into the main window's tabs.</summary>
    public event EventHandler<SessionView>? DockBackRequested;
    /// <summary>Raised when the user closes the window and the session should be closed.</summary>
    public event EventHandler<SessionView>? CloseSessionRequested;

    public SessionWindow(SessionView view, Action<SessionView, Form> showFill, ToolTip tt)
    {
        _view = view;
        _showFill = showFill;
        AutoScaleMode = AutoScaleMode.None;
        Text = view.Machine.Name + " — " + App.Name;
        Icon = Theme.AppIcon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        KeyPreview = true;
        MinimumSize = new Size(Theme.S(640), Theme.S(420));
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Size = new Size(Math.Min(Theme.S(1100), wa.Width - 100), Math.Min(Theme.S(760), wa.Height - 80));
        StartPosition = FormStartPosition.Manual;

        _bar = new Panel { Dock = DockStyle.Top, Height = Theme.S(34), BackColor = Theme.Bg };
        _title = new Label { Dock = DockStyle.Fill, Text = view.Machine.Name, ForeColor = Theme.TextDim, Font = Theme.Small, Padding = new Padding(Theme.S(10), Theme.S(9), 0, 0) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(2), Theme.S(4), 0) };
        var btnFill = Theme.IconButton(Theme.IcoKeyboard, "Fill a login  (Ctrl+Alt+K)", tt);
        btnFill.Click += (_, _) => _showFill(_view, this);
        var btnFull = Theme.IconButton(Theme.IcoFullscreen, "Fullscreen  (F11)", tt);
        btnFull.Click += (_, _) => ToggleFullscreen();
        var btnDock = Theme.IconButton("", "Dock back into tabs", tt); // "return" glyph
        btnDock.Click += (_, _) => DockBackRequested?.Invoke(this, _view);
        actions.Controls.AddRange([btnFill, btnFull, btnDock]);
        _bar.Controls.Add(_title);
        _bar.Controls.Add(actions);

        _toast = new Label
        {
            BackColor = Theme.Surface2, ForeColor = Theme.Text, Font = Theme.Ui, AutoSize = true, Visible = false,
            Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(14), Theme.S(10)), MaximumSize = new Size(Theme.S(480), 0),
        };
        _toast.Click += (_, _) => _toast.Visible = false;
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _toast.Visible = false; };

        view.Dock = DockStyle.Fill;
        view.Visible = true;
        Controls.Add(view);
        Controls.Add(_bar);
        Controls.Add(_toast);
        view.BringToFront();
        _bar.BringToFront();

        view.Notify += OnNotify;
    }

    private void OnNotify(object? sender, string text) => Toast(text);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Theme.UseDarkTitleBar(this);
        _view.FocusRemote();
    }

    /// <summary>Removes the view so the window can close without disposing the (reattached) session.</summary>
    public void ReleaseView()
    {
        _view.Notify -= OnNotify;
        if (Controls.Contains(_view)) Controls.Remove(_view);
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _fullscreen = true;
            _savedState = WindowState;
            _savedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _bar.Visible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            _mini ??= new MiniFsBar(() => ToggleFullscreen(), () => Close());
            _mini.ShowOn(Screen.FromControl(this), _view.Machine.Name, _view.Tab.Status);
        }
        else
        {
            _fullscreen = false;
            _mini?.HideBar();
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _savedBounds;
            WindowState = _savedState;
            _bar.Visible = true;
        }
        _view.FocusRemote();
    }

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
        if (_toast is { Visible: true })
            _toast.Location = new Point(ClientSize.Width - _toast.Width - Theme.S(18), ClientSize.Height - _toast.Height - Theme.S(18));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F11:
            case Keys.Control | Keys.Alt | Keys.Enter: ToggleFullscreen(); return true;
            case Keys.Escape when _fullscreen: ToggleFullscreen(); return true;
            case Keys.Control | Keys.Alt | Keys.K: _showFill(_view, this); return true;
            case Keys.Control | Keys.Alt | Keys.R: _view.Connect(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // If the view is still ours (not being docked back), closing the window closes the session.
        if (Controls.Contains(_view))
        {
            _view.Notify -= OnNotify;
            CloseSessionRequested?.Invoke(this, _view);
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _toastTimer.Dispose(); _mini?.Dispose(); }
        base.Dispose(disposing);
    }
}
