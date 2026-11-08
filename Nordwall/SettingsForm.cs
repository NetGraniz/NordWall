using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Nordwall;

internal sealed class SettingsForm : Form
{
    private static readonly Color Surface = Color.FromArgb(21, 25, 33);
    private static readonly Color TextColor = Color.FromArgb(225, 235, 244);
    private static readonly Color Muted = Color.FromArgb(145, 158, 173);

    private readonly DisplayPreferences _draft;
    private Screen[] _screens;
    private readonly System.Windows.Forms.Timer _displayRefresh;
    private readonly MonitorMapControl _map;
    private readonly ComboBox _wallpaperChoice;
    private readonly Label _selectionTitle;
    private readonly RadioButton _separateRadio;
    private readonly RadioButton _spanRadio;
    private readonly CheckBox _autoPause;
    private readonly CheckBox _launchAtSignIn;
    private readonly Action<DisplayPreferences> _onApply;
    private readonly Action _onExit;
    private bool _loadingChoice;

    public SettingsForm(DisplayPreferences settings, Screen[] screens,
        string wallpapersDirectory, Icon icon,
        Action<DisplayPreferences> onApply, Action onExit)
    {
        _draft = settings.Clone();
        _screens = screens;
        _onApply = onApply;
        _onExit = onExit;

        Text = "Nordwall — Settings";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(800, 610);
        BackColor = Surface;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10F);

        Controls.Add(MakeLabel("Nordwall", 25, 17, 750, 38, 23F, FontStyle.Bold));
        Controls.Add(MakeLabel("Animated wallpaper settings", 27, 56, 750, 25, 10F, color: Muted));

        _separateRadio = new RadioButton
        {
            Text = "Separate wallpapers",
            Location = new Point(27, 102),
            Size = new Size(210, 33),
            ForeColor = TextColor,
            Checked = _draft.Mode == WallpaperMode.Separate
        };
        _spanRadio = new RadioButton
        {
            Text = "Span across displays",
            Location = new Point(250, 102),
            Size = new Size(235, 33),
            ForeColor = TextColor,
            Checked = _draft.Mode == WallpaperMode.Span
        };
        Controls.Add(_separateRadio);
        Controls.Add(_spanRadio);

        _map = new MonitorMapControl(_screens)
        {
            Bounds = new Rectangle(25, 145, 750, 205),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _map.SelectionChanged += _ => UpdateWallpaperChoice();
        Controls.Add(_map);

        Controls.Add(MakeLabel(
            "Monitor positions are read from Windows. Select a screen above, or arrange screens in Windows.",
            27, 355, 670, 25, 9F, color: Muted));
        var arrange = MakeButton("Arrange displays…", 610, 384, 165, 33);
        arrange.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:display")
                    { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Windows display settings: {ex.Message}",
                    "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        Controls.Add(arrange);

        _selectionTitle = MakeLabel("Wallpaper", 27, 394, 560, 28, 11F, FontStyle.Bold);
        Controls.Add(_selectionTitle);

        _wallpaperChoice = new ComboBox
        {
            Location = new Point(27, 430),
            Size = new Size(748, 33),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(35, 42, 53),
            ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat
        };
        _wallpaperChoice.Items.Add("(No wallpaper)");
        try
        {
            foreach (WallpaperFile file in WallpaperCatalog.GetAll(wallpapersDirectory))
                _wallpaperChoice.Items.Add(file.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Cannot list wallpapers: {ex.Message}",
                "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        _wallpaperChoice.SelectedIndexChanged += (_, _) => StoreWallpaperChoice();
        Controls.Add(_wallpaperChoice);

        _autoPause = new CheckBox
        {
            Text = "Automatically pause when a fullscreen or maximized app is open",
            Bounds = new Rectangle(27, 482, 675, 31),
            Checked = _draft.AutoPauseFullscreen,
            ForeColor = TextColor
        };
        _launchAtSignIn = new CheckBox
        {
            Text = "Launch Nordwall at sign-in",
            Bounds = new Rectangle(27, 518, 400, 31),
            Checked = StartupManager.IsEnabled(),
            ForeColor = TextColor
        };
        Controls.Add(_autoPause);
        Controls.Add(_launchAtSignIn);

        var exit = MakeButton("Exit Nordwall", 27, 562, 140, 34);
        exit.Click += (_, _) => _onExit();
        Controls.Add(exit);

        var cancel = MakeButton("Cancel", 550, 562, 105, 34);
        cancel.Click += (_, _) => Close();
        Controls.Add(cancel);

        var apply = MakeButton("Save settings", 665, 562, 110, 34);
        apply.BackColor = Color.FromArgb(52, 127, 110);
        apply.Click += (_, _) => ApplyAndClose();
        AcceptButton = apply;
        CancelButton = cancel;
        Controls.Add(apply);

        _separateRadio.CheckedChanged += (_, _) => ChangeMode();
        _spanRadio.CheckedChanged += (_, _) => ChangeMode();
        UpdateWallpaperChoice();

        _displayRefresh = new System.Windows.Forms.Timer { Interval = 1600 };
        _displayRefresh.Tick += (_, _) => SyncDisplayMap();
        _displayRefresh.Start();
    }

    private void SyncDisplayMap()
    {
        Screen[] latest = Screen.AllScreens;
        string Describe(Screen s) => $"{s.DeviceName}:{s.Bounds}:{s.Primary}";
        if (latest.Length == _screens.Length &&
            latest.OrderBy(s => s.DeviceName).Select(Describe).SequenceEqual(
                _screens.OrderBy(s => s.DeviceName).Select(Describe)))
            return;

        _screens = latest;
        _map.SetScreens(latest);
        UpdateWallpaperChoice();
    }

    private void ChangeMode()
    {
        _draft.Mode = _spanRadio.Checked ? WallpaperMode.Span : WallpaperMode.Separate;
        UpdateWallpaperChoice();
    }

    private void UpdateWallpaperChoice()
    {
        _loadingChoice = true;
        try
        {
            string? name;
            if (_draft.Mode == WallpaperMode.Span)
            {
                _selectionTitle.Text = "Wallpaper across all displays";
                name = _draft.SpanWallpaper ?? _draft.LastWallpaper;
            }
            else
            {
                string display = _map.SelectedDeviceName;
                _selectionTitle.Text = "Wallpaper for " +
                    (_screens.FirstOrDefault(s => s.DeviceName == display)?.DeviceName ?? display);
                name = _draft.PerMonitorWallpapers.TryGetValue(display, out string? choice)
                    ? choice : _draft.LastWallpaper;
            }

            int index = name is null ? -1 : _wallpaperChoice.Items.IndexOf(name);
            _wallpaperChoice.SelectedIndex = index < 0 ? 0 : index;
        }
        finally
        {
            _loadingChoice = false;
        }
    }

    private void StoreWallpaperChoice()
    {
        if (_loadingChoice)
            return;

        string? value = _wallpaperChoice.SelectedIndex > 0
            ? _wallpaperChoice.SelectedItem?.ToString() : string.Empty;
        if (_draft.Mode == WallpaperMode.Span)
            _draft.SpanWallpaper = value;
        else
            _draft.PerMonitorWallpapers[_map.SelectedDeviceName] = value;
    }

    private void ApplyAndClose()
    {
        _draft.AutoPauseFullscreen = _autoPause.Checked;
        try
        {
            if (_launchAtSignIn.Checked != StartupManager.IsEnabled())
                StartupManager.SetEnabled(_launchAtSignIn.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot change startup settings: {ex.Message}",
                "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _onApply(_draft.Clone());
        Close();
    }

    private static Label MakeLabel(string text, int x, int y, int width, int height,
        float size, FontStyle style = FontStyle.Regular, Color? color = null) => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Font = new Font("Segoe UI", size, style),
            ForeColor = color ?? TextColor,
            AutoEllipsis = true
        };

    private static Button MakeButton(string text, int x, int y, int width, int height) => new()
        {
            Text = text,
            Bounds = new Rectangle(x, y, width, height),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(44, 52, 66),
            ForeColor = TextColor,
            Cursor = Cursors.Hand
        };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _displayRefresh.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MonitorMapControl : Control
{
    private Screen[] _screens;
    private readonly Dictionary<string, RectangleF> _hitBoxes =
        new(StringComparer.OrdinalIgnoreCase);
    public event Action<string>? SelectionChanged;
    public string SelectedDeviceName { get; private set; }

    public MonitorMapControl(Screen[] screens)
    {
        _screens = screens;
        SelectedDeviceName = screens.FirstOrDefault(s => s.Primary)?.DeviceName
            ?? screens.FirstOrDefault()?.DeviceName ?? string.Empty;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(28, 34, 44);
    }

    public void SetScreens(Screen[] screens)
    {
        _screens = screens;
        if (!_screens.Any(s => s.DeviceName.Equals(SelectedDeviceName,
                StringComparison.OrdinalIgnoreCase)))
            SelectedDeviceName = screens.FirstOrDefault(s => s.Primary)?.DeviceName
                ?? screens.FirstOrDefault()?.DeviceName ?? string.Empty;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _hitBoxes.Clear();
        if (_screens.Length == 0)
            return;

        Rectangle union = _screens.Select(s => s.Bounds)
            .Aggregate(Rectangle.Union);
        float scale = Math.Min((Width - 42f) / Math.Max(1, union.Width),
            (Height - 42f) / Math.Max(1, union.Height));
        float mapWidth = union.Width * scale;
        float mapHeight = union.Height * scale;
        float offsetX = (Width - mapWidth) / 2;
        float offsetY = (Height - mapHeight) / 2;

        using var baseBrush = new SolidBrush(Color.FromArgb(58, 68, 83));
        using var selectedBrush = new SolidBrush(Color.FromArgb(48, 116, 104));
        using var borderPen = new Pen(Color.FromArgb(121, 144, 166), 2f);
        using var activePen = new Pen(Color.FromArgb(135, 232, 195), 2.5f);
        using var labelBrush = new SolidBrush(Color.WhiteSmoke);
        using var labelFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        using var detailFont = new Font("Segoe UI", 8f);

        for (int i = 0; i < _screens.Length; i++)
        {
            Screen screen = _screens[i];
            Rectangle bounds = screen.Bounds;
            var rect = new RectangleF(
                offsetX + (bounds.Left - union.Left) * scale,
                offsetY + (bounds.Top - union.Top) * scale,
                bounds.Width * scale, bounds.Height * scale);
            _hitBoxes[screen.DeviceName] = rect;

            bool selected = screen.DeviceName.Equals(SelectedDeviceName,
                StringComparison.OrdinalIgnoreCase);
            e.Graphics.FillRectangle(selected ? selectedBrush : baseBrush, rect);
            e.Graphics.DrawRectangle(selected ? activePen : borderPen,
                rect.X, rect.Y, rect.Width, rect.Height);

            string deviceName = screen.DeviceName;
            int marker = deviceName.LastIndexOf("DISPLAY", StringComparison.OrdinalIgnoreCase);
            string label = marker >= 0 ? deviceName[(marker + 7)..] : (i + 1).ToString();
            using var numberFormat = new StringFormat
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(label, labelFont, labelBrush,
                new RectangleF(rect.X, rect.Y, rect.Width, rect.Height - 11), numberFormat);
            if (rect.Height >= 60 && rect.Width >= 110)
            {
                string description = $"{bounds.Width} × {bounds.Height}" +
                    (screen.Primary ? "  ·  Primary" : "");
                e.Graphics.DrawString(description, detailFont, labelBrush,
                    new RectangleF(rect.X + 4, rect.Bottom - 29, rect.Width - 8, 24),
                    numberFormat);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        foreach ((string device, RectangleF rect) in _hitBoxes)
        {
            if (!rect.Contains(e.X, e.Y))
                continue;
            SelectedDeviceName = device;
            Invalidate();
            SelectionChanged?.Invoke(device);
            break;
        }
    }
}
