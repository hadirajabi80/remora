using System.Drawing.Drawing2D;

namespace RemoteHub.UI;

internal enum TabStatus { Connecting, Connected, Disconnected }

internal sealed class TabItem
{
    public string Title { get; set; } = "";
    public string ToolTip { get; set; } = "";
    public TabStatus Status { get; set; }
    public object? Tag { get; set; }
    internal Rectangle Bounds;
    internal Rectangle CloseBounds;
}

/// <summary>Browser-style tab strip: drag to reorder, middle-click to close, status dot per session.</summary>
internal sealed class TabStrip : Control
{
    private readonly List<TabItem> _tabs = new();
    private TabItem? _selected, _hover, _dragging;
    private bool _hoverClose, _hoverAdd;
    private Point _dragStart;
    private bool _dragActive;
    private Rectangle _addBounds;
    private readonly ToolTip _tip = new() { InitialDelay = 600 };
    private TabItem? _tipFor;
    private DragGhost? _ghost;

    public IReadOnlyList<TabItem> Tabs => _tabs;

    public event EventHandler? SelectedChanged;
    public event EventHandler<TabItem>? CloseRequested;
    public event EventHandler<TabItem>? ContextRequested;
    public event EventHandler? AddRequested;
    /// <summary>Fired when a tab is dragged off the strip and dropped; Point is the screen drop location.</summary>
    public event EventHandler<(TabItem tab, Point screen)>? DetachRequested;

    public TabStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Theme.Bg;
        Height = Theme.S(40);
        Font = Theme.Ui;
    }

    public TabItem? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Add(TabItem tab, bool select = true)
    {
        _tabs.Add(tab);
        if (select) Selected = tab; else Invalidate();
    }

    public void Remove(TabItem tab)
    {
        int idx = _tabs.IndexOf(tab);
        if (idx < 0) return;
        _tabs.RemoveAt(idx);
        if (_hover == tab) _hover = null;
        if (_selected == tab)
            Selected = _tabs.Count == 0 ? null : _tabs[Math.Min(idx, _tabs.Count - 1)];
        else
            Invalidate();
    }

    public void SelectOffset(int delta)
    {
        if (_tabs.Count == 0) return;
        int i = _selected == null ? 0 : _tabs.IndexOf(_selected);
        Selected = _tabs[((i + delta) % _tabs.Count + _tabs.Count) % _tabs.Count];
    }

    public void SelectIndex(int index)
    {
        if (index >= 0 && index < _tabs.Count) Selected = _tabs[index];
    }

    public void Refresh(TabItem tab) => Invalidate();

    private void LayoutTabs()
    {
        int x = Theme.S(4);
        int h = Height - Theme.S(6);
        int addW = Theme.S(34);
        int avail = Width - x - addW - Theme.S(8);
        int w = _tabs.Count == 0 ? 0 : Math.Clamp(avail / _tabs.Count, Theme.S(70), Theme.S(230));
        foreach (var t in _tabs)
        {
            t.Bounds = new Rectangle(x, Theme.S(6), w - Theme.S(2), h);
            int cs = Theme.S(18);
            t.CloseBounds = new Rectangle(t.Bounds.Right - cs - Theme.S(8), t.Bounds.Top + (h - cs) / 2, cs, cs);
            x += w;
        }
        _addBounds = new Rectangle(Math.Min(x + Theme.S(2), Width - addW), Theme.S(8), addW - Theme.S(4), h - Theme.S(4));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        LayoutTabs();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        using (var line = new Pen(Theme.Border))
            g.DrawLine(line, 0, Height - 1, Width, Height - 1);

        foreach (var t in _tabs)
        {
            bool sel = t == _selected;
            bool hov = t == _hover;
            var r = t.Bounds;

            if (sel || hov)
            {
                using var path = TopRounded(r, Theme.S(7));
                using var b = new SolidBrush(sel ? Theme.Surface2 : Theme.Surface);
                g.FillPath(b, path);
                if (sel)
                {
                    using var accent = new SolidBrush(Theme.Accent);
                    g.FillRectangle(accent, r.X + Theme.S(8), r.Y, r.Width - Theme.S(16), Theme.S(2));
                }
            }
            else
            {
                using var sep = new Pen(Theme.Border);
                g.DrawLine(sep, r.Right, r.Top + Theme.S(9), r.Right, r.Bottom - Theme.S(9));
            }

            int dot = Theme.S(8);
            var dotRect = new Rectangle(r.X + Theme.S(12), r.Y + (r.Height - dot) / 2, dot, dot);
            using (var db = new SolidBrush(t.Status switch
                   {
                       TabStatus.Connected => Theme.Green,
                       TabStatus.Connecting => Theme.Yellow,
                       _ => Theme.Red,
                   }))
                g.FillEllipse(db, dotRect);

            bool showClose = sel || hov;
            int textRight = showClose ? t.CloseBounds.Left - Theme.S(4) : r.Right - Theme.S(10);
            var textRect = new Rectangle(dotRect.Right + Theme.S(8), r.Y, Math.Max(0, textRight - dotRect.Right - Theme.S(8)), r.Height);
            TextRenderer.DrawText(g, t.Title, sel ? Theme.UiBold : Theme.Ui, textRect, sel ? Theme.Text : Theme.TextDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (showClose)
            {
                var cr = t.CloseBounds;
                if (hov && _hoverClose)
                {
                    using var cb = new SolidBrush(Theme.Hover);
                    g.FillEllipse(cb, cr);
                }
                using var xp = new Pen(hov && _hoverClose ? Theme.Text : Theme.TextDim, Theme.S(1) * 1.4f);
                int pad = Theme.S(5);
                g.DrawLine(xp, cr.Left + pad, cr.Top + pad, cr.Right - pad, cr.Bottom - pad);
                g.DrawLine(xp, cr.Right - pad, cr.Top + pad, cr.Left + pad, cr.Bottom - pad);
            }
        }

        // "+" button
        if (_hoverAdd)
        {
            using var hb = new SolidBrush(Theme.Hover);
            using var p = Theme.RoundRect(_addBounds, Theme.S(6));
            g.FillPath(hb, p);
        }
        TextRenderer.DrawText(g, Theme.IcoAdd, Theme.Icon, _addBounds, Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath TopRounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddLine(r.Right, r.Y + radius, r.Right, r.Bottom);
        p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        p.CloseFigure();
        return p;
    }

    private TabItem? HitTest(Point p) => _tabs.FirstOrDefault(t => t.Bounds.Contains(p));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging != null && e.Button == MouseButtons.Left)
        {
            if (!_dragActive && (Math.Abs(e.X - _dragStart.X) > Theme.S(6) || Math.Abs(e.Y - _dragStart.Y) > Theme.S(6)))
            {
                _dragActive = true;
                _ghost ??= new DragGhost();
            }
            if (_dragActive)
            {
                bool detachZone = e.Y > Height + Theme.S(24) || e.Y < -Theme.S(16);
                _ghost?.Update(PointToScreen(e.Location), _dragging.Title, _dragging.Status, detachZone && _tabs.Count > 1);

                if (!detachZone)
                {
                    var over = HitTest(new Point(e.X, Height / 2));
                    if (over != null && over != _dragging)
                    {
                        int from = _tabs.IndexOf(_dragging), to = _tabs.IndexOf(over);
                        _tabs.RemoveAt(from);
                        _tabs.Insert(to, _dragging);
                        Invalidate();
                    }
                }
                return;
            }
        }

        var h = HitTest(e.Location);
        bool hc = h != null && h.CloseBounds.Contains(e.Location);
        bool ha = _addBounds.Contains(e.Location);
        if (h != _hover || hc != _hoverClose || ha != _hoverAdd)
        {
            _hover = h; _hoverClose = hc; _hoverAdd = ha;
            Invalidate();
        }
        if (h != _tipFor)
        {
            _tipFor = h;
            _tip.SetToolTip(this, h?.ToolTip ?? "");
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null; _hoverClose = false; _hoverAdd = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var t = HitTest(e.Location);
        if (e.Button == MouseButtons.Left)
        {
            if (_addBounds.Contains(e.Location)) { AddRequested?.Invoke(this, EventArgs.Empty); return; }
            if (t == null) return;
            if (t.CloseBounds.Contains(e.Location) && (t == _selected || t == _hover)) { CloseRequested?.Invoke(this, t); return; }
            Selected = t;
            _dragging = t; _dragStart = e.Location; _dragActive = false;
        }
        else if (e.Button == MouseButtons.Middle && t != null)
        {
            CloseRequested?.Invoke(this, t);
        }
        else if (e.Button == MouseButtons.Right && t != null)
        {
            Selected = t;
            ContextRequested?.Invoke(this, t);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _ghost?.Dispose();
        _ghost = null;
        // Dragging a tab well outside the strip tears it off into its own window.
        if (_dragging != null && _dragActive && _tabs.Count > 1 && (e.Y > Height + Theme.S(30) || e.Y < -Theme.S(20)))
            DetachRequested?.Invoke(this, (_dragging, PointToScreen(e.Location)));
        _dragging = null; _dragActive = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) == null && !_addBounds.Contains(e.Location))
            AddRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tip.Dispose(); _ghost?.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>A small floating preview of the tab that follows the cursor while dragging, for the tear-off feel.</summary>
internal sealed class DragGhost : Form
{
    private string _title = "";
    private TabStatus _status;
    private bool _detach;

    public DragGhost()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Opacity = 0.9;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // lets the rounded corners show through
        Size = new Size(Theme.S(230), Theme.S(58));
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000020 /* WS_EX_TRANSPARENT (click-through) */;
            return cp;
        }
    }

    public void Update(Point cursorScreen, string title, TabStatus status, bool detach)
    {
        bool changed = title != _title || status != _status || detach != _detach;
        _title = title; _status = status; _detach = detach;
        Height = detach ? Theme.S(58) : Theme.S(40);
        Location = new Point(cursorScreen.X + Theme.S(12), cursorScreen.Y + Theme.S(14));
        if (!Visible) Show();
        if (changed) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(TransparencyKey);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundRect(r, Theme.S(8)))
        {
            using var fill = new SolidBrush(Theme.Surface2);
            g.FillPath(fill, path);
            using var pen = new Pen(_detach ? Theme.Accent : Theme.Border, _detach ? 2f : 1f);
            g.DrawPath(pen, path);
        }

        int dot = Theme.S(8), rowH = Theme.S(40);
        using (var db = new SolidBrush(_status switch { TabStatus.Connected => Theme.Green, TabStatus.Connecting => Theme.Yellow, _ => Theme.Red }))
            g.FillEllipse(db, Theme.S(12), (rowH - dot) / 2, dot, dot);
        TextRenderer.DrawText(g, _title, Theme.UiBold, new Rectangle(Theme.S(28), 0, Width - Theme.S(36), rowH), Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (_detach)
            TextRenderer.DrawText(g, "  Release to open in new window", Theme.Small, new Rectangle(Theme.S(12), rowH - Theme.S(4), Width - Theme.S(20), Theme.S(18)),
                Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}
