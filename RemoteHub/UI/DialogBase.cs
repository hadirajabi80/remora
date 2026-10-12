namespace RemoteHub.UI;

/// <summary>Dark, code-built dialog with a label/field grid and OK/Cancel buttons.</summary>
internal class DialogBase : Form
{
    protected readonly TableLayoutPanel Grid;
    protected readonly Button OkButton;
    protected readonly Button CancelBtn;
    protected readonly FlowLayoutPanel ButtonBar;
    private readonly int _fieldWidth;

    public DialogBase(string title, int width = 560, int scrollHeight = 0)
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = title;
        Icon = Theme.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        int labelWidth = Theme.S(165);
        _fieldWidth = Theme.S(width) - labelWidth - Theme.S(48);

        Grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(Theme.S(20), Theme.S(14), Theme.S(20), Theme.S(4)),
            BackColor = Theme.Bg,
        };
        Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _fieldWidth));

        ButtonBar = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(Theme.S(16), Theme.S(10), Theme.S(16), Theme.S(16)),
            Anchor = AnchorStyles.Right,
            BackColor = Theme.Bg,
            WrapContents = false,
        };
        OkButton = Theme.Button("Save", primary: true, width: 100);
        CancelBtn = Theme.Button("Cancel", width: 100);
        CancelBtn.DialogResult = DialogResult.Cancel;
        OkButton.Click += (_, _) =>
        {
            if (ValidateAndApply())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        ButtonBar.Controls.Add(OkButton);
        ButtonBar.Controls.Add(CancelBtn);
        AcceptButton = OkButton;
        CancelButton = CancelBtn;

        var outer = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Bg,
            Location = Point.Empty,
        };
        if (scrollHeight > 0)
        {
            var scroller = new Panel
            {
                AutoScroll = true,
                Size = new Size(Theme.S(width) + SystemInformation.VerticalScrollBarWidth, Theme.S(scrollHeight)),
                BackColor = Theme.Bg,
                Margin = Padding.Empty,
            };
            Theme.UseDarkScrollbars(scroller);
            scroller.Controls.Add(Grid);
            outer.Controls.Add(scroller);
        }
        else
        {
            outer.Controls.Add(Grid);
        }
        outer.Controls.Add(ButtonBar);
        Controls.Add(outer);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this);
    }

    /// <summary>Override to validate and copy values back; return false to keep the dialog open.</summary>
    protected virtual bool ValidateAndApply() => true;

    protected T Row<T>(string label, T control, string? hint = null) where T : Control
    {
        var l = Theme.Label(label);
        Grid.Controls.Add(l);
        control.Width = control is CheckBox or NumericUpDown ? control.Width : _fieldWidth - Theme.S(4);
        control.Margin = new Padding(0, Theme.S(4), 0, Theme.S(4));
        if (hint == null)
        {
            Grid.Controls.Add(control);
        }
        else
        {
            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = Theme.Bg,
            };
            var h = new Label
            {
                Text = hint, AutoSize = true, ForeColor = Theme.TextDim, Font = Theme.Small,
                MaximumSize = new Size(_fieldWidth - Theme.S(4), 0), Margin = new Padding(0, 0, 0, Theme.S(6)),
            };
            stack.Controls.Add(control);
            stack.Controls.Add(h);
            Grid.Controls.Add(stack);
        }
        return control;
    }

    protected void Section(string title)
    {
        var l = new Label
        {
            Text = title, AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Accent,
            Margin = new Padding(0, Theme.S(14), 0, Theme.S(4)),
        };
        Grid.Controls.Add(l);
        Grid.SetColumnSpan(l, 2);
    }

    protected void Note(string text)
    {
        var l = new Label
        {
            Text = text, AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextDim,
            MaximumSize = new Size(Grid.ColumnStyles.Cast<ColumnStyle>().Sum(c => (int)c.Width), 0),
            Margin = new Padding(0, 0, 0, Theme.S(6)),
        };
        Grid.Controls.Add(l);
        Grid.SetColumnSpan(l, 2);
    }

    /// <summary>A password box with an eye toggle.</summary>
    protected TextBox PasswordRow(string label, string value, string? hint = null)
    {
        var tb = Theme.TextBox(value, password: true);
        var panel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty, BackColor = Theme.Bg };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _fieldWidth - Theme.S(44)));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.S(40)));
        tb.Width = _fieldWidth - Theme.S(48);
        tb.Margin = new Padding(0, Theme.S(2), 0, 0);
        var eye = new Button
        {
            Text = "", Font = Theme.Icon, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface2, ForeColor = Theme.TextDim,
            Size = new Size(Theme.S(36), tb.Height + Theme.S(1)), Margin = new Padding(Theme.S(2), Theme.S(2), 0, 0), TabStop = false, Cursor = Cursors.Hand,
        };
        eye.FlatAppearance.BorderColor = Theme.Border;
        eye.Click += (_, _) => tb.UseSystemPasswordChar = !tb.UseSystemPasswordChar;
        panel.Controls.Add(tb);
        panel.Controls.Add(eye);
        Row(label, panel, hint);
        panel.Width = _fieldWidth;
        return tb;
    }

    protected static void Warn(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
