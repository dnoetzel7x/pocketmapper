using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PocketMapper.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly Label _statusLabel;
    private readonly Label _resolutionLabel;
    private readonly Label _refreshRateLabel;
    private readonly Button _startButton;
    private readonly Button _patternButton;
    private readonly Button _blackoutButton;
    private readonly Button _stopButton;
    private readonly System.Windows.Forms.Timer _displayTimer;

    private Screen? _externalScreen;
    private OutputForm? _outputForm;

    public MainForm()
    {
        Text = "PocketMapper";
        ClientSize = new Size(480, 520);
        MinimumSize = new Size(420, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(17, 17, 17);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);

        var titleLabel = new Label
        {
            Text = "PocketMapper",
            AutoSize = true,
            Font = new Font("Segoe UI", 28F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 20)
        };

        _statusLabel = CreateLabel(15F, FontStyle.Bold);
        _resolutionLabel = CreateLabel();
        _refreshRateLabel = CreateLabel();

        _startButton = CreateButton("START OUTPUT", (_, _) => StartOutput());
        _patternButton = CreateButton("TEST PATTERN", (_, _) => _outputForm?.ShowTestPattern());
        _blackoutButton = CreateButton("BLACKOUT", (_, _) => _outputForm?.Blackout());
        _stopButton = CreateButton("STOP OUTPUT", (_, _) => StopOutput());

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 9,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(_statusLabel, 0, 1);
        layout.Controls.Add(_resolutionLabel, 0, 2);
        layout.Controls.Add(_refreshRateLabel, 0, 3);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 4);
        layout.Controls.Add(_startButton, 0, 5);
        layout.Controls.Add(_patternButton, 0, 6);
        layout.Controls.Add(_blackoutButton, 0, 7);
        layout.Controls.Add(_stopButton, 0, 8);
        Controls.Add(layout);

        _displayTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _displayTimer.Tick += (_, _) => RefreshExternalDisplay();
        _displayTimer.Start();
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;

        FormClosed += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            _displayTimer.Stop();
            _displayTimer.Dispose();
            StopOutput();
        };

        RefreshExternalDisplay();
    }

    private Label CreateLabel(float size = 11F, FontStyle style = FontStyle.Regular) => new()
    {
        AutoSize = true,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", size, style, GraphicsUnit.Point),
        Margin = new Padding(0, 3, 0, 3)
    };

    private Button CreateButton(string text, EventHandler clickHandler)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 4, 0, 4),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += clickHandler;
        return button;
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke((Action)RefreshExternalDisplay);
    }

    private void RefreshExternalDisplay()
    {
        var detectedScreen = Screen.AllScreens.FirstOrDefault(screen => !screen.Primary);

        if (_outputForm is not null)
        {
            if (detectedScreen is null ||
                !string.Equals(detectedScreen.DeviceName, _outputForm.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                StopOutput();
            }
            else
            {
                _outputForm.MoveTo(detectedScreen);
            }
        }

        _externalScreen = detectedScreen;
        var connected = detectedScreen is not null;
        _statusLabel.Text = connected
            ? "External Display: Connected"
            : "External Display: Not Connected";

        if (detectedScreen is null)
        {
            _resolutionLabel.Text = string.Empty;
            _refreshRateLabel.Text = string.Empty;
        }
        else
        {
            _resolutionLabel.Text = $"Resolution: {detectedScreen.Bounds.Width} × {detectedScreen.Bounds.Height}";
            var refreshRate = DisplayInfo.GetRefreshRate(detectedScreen);
            _refreshRateLabel.Text = refreshRate > 0
                ? $"Refresh Rate: {refreshRate} Hz"
                : "Refresh Rate: Unknown";
        }

        UpdateButtons();
    }

    private void StartOutput()
    {
        var screen = _externalScreen;
        if (screen is null)
        {
            return;
        }

        StopOutput();
        _outputForm = new OutputForm(screen);
        _outputForm.FormClosed += (_, _) =>
        {
            _outputForm = null;
            UpdateButtons();
        };
        _outputForm.Show();
        Activate();
        UpdateButtons();
    }

    private void StopOutput()
    {
        if (_outputForm is not null)
        {
            var form = _outputForm;
            _outputForm = null;
            form.Close();
            form.Dispose();
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var connected = _externalScreen is not null;
        var running = _outputForm is not null && !_outputForm.IsDisposed;
        _startButton.Enabled = connected && !running;
        _patternButton.Enabled = connected && running;
        _blackoutButton.Enabled = connected && running;
        _stopButton.Enabled = running;
    }
}

internal sealed class OutputForm : Form
{
    private bool _blackout = true;

    public string DeviceName { get; private set; }

    public OutputForm(Screen screen)
    {
        DeviceName = screen.DeviceName;
        Text = "PocketMapper Output";
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExToolWindow = 0x00000080;
            const int WsExNoActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void MoveTo(Screen screen)
    {
        DeviceName = screen.DeviceName;
        if (Bounds != screen.Bounds)
        {
            Bounds = screen.Bounds;
            Invalidate();
        }
    }

    public void ShowTestPattern()
    {
        _blackout = false;
        Invalidate();
    }

    public void Blackout()
    {
        _blackout = true;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Color.Black);
        if (_blackout || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var margin = Math.Min(ClientSize.Width, ClientSize.Height) * 0.04F;
        var frame = new RectangleF(
            margin,
            margin,
            ClientSize.Width - margin * 2F,
            ClientSize.Height - margin * 2F);

        using var pen = new Pen(Color.White, Math.Max(2F, Math.Min(ClientSize.Width, ClientSize.Height) * 0.002F));
        e.Graphics.DrawRectangle(pen, frame.X, frame.Y, frame.Width, frame.Height);

        for (var index = 1; index <= 3; index++)
        {
            var x = frame.Left + frame.Width * index / 4F;
            var y = frame.Top + frame.Height * index / 4F;
            e.Graphics.DrawLine(pen, x, frame.Top, x, frame.Bottom);
            e.Graphics.DrawLine(pen, frame.Left, y, frame.Right, y);
        }

        var cornerSize = Math.Min(frame.Width, frame.Height) * 0.08F;
        DrawCorner(e.Graphics, pen, frame.Left, frame.Top, cornerSize, 1F, 1F);
        DrawCorner(e.Graphics, pen, frame.Right, frame.Top, cornerSize, -1F, 1F);
        DrawCorner(e.Graphics, pen, frame.Left, frame.Bottom, cornerSize, 1F, -1F);
        DrawCorner(e.Graphics, pen, frame.Right, frame.Bottom, cornerSize, -1F, -1F);

        var centerX = ClientSize.Width / 2F;
        var centerY = ClientSize.Height / 2F;
        var marker = Math.Min(ClientSize.Width, ClientSize.Height) * 0.035F;
        e.Graphics.DrawEllipse(pen, centerX - marker, centerY - marker, marker * 2F, marker * 2F);
        e.Graphics.DrawLine(pen, centerX - marker * 1.5F, centerY, centerX + marker * 1.5F, centerY);
        e.Graphics.DrawLine(pen, centerX, centerY - marker * 1.5F, centerX, centerY + marker * 1.5F);

        var fontSize = Math.Max(16F, Math.Min(ClientSize.Width, ClientSize.Height) * 0.055F);
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        var textArea = new RectangleF(
            frame.Left,
            centerY + frame.Height * 0.13F,
            frame.Width,
            frame.Height * 0.14F);
        e.Graphics.DrawString("POCKETMAPPER TEST", font, brush, textArea, format);
    }

    private static void DrawCorner(
        Graphics graphics,
        Pen pen,
        float x,
        float y,
        float size,
        float directionX,
        float directionY)
    {
        graphics.DrawLine(pen, x, y, x + size * directionX, y);
        graphics.DrawLine(pen, x, y, x, y + size * directionY);
    }
}

internal static class DisplayInfo
{
    private const int VerticalRefresh = 116;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(
        string driver,
        string device,
        string? output,
        IntPtr initData);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr deviceContext, int index);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    public static int GetRefreshRate(Screen screen)
    {
        var deviceContext = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var refreshRate = GetDeviceCaps(deviceContext, VerticalRefresh);
            return refreshRate > 1 ? refreshRate : 0;
        }
        finally
        {
            DeleteDC(deviceContext);
        }
    }
}
