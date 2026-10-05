using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using FontSoftware.App.Services;
using FontSoftware.App.ViewModels;
using FontSoftware.Core.Config;
using FontSoftware.Core.Diagnostics;

namespace FontSoftware.App.Views;

public partial class MainWindow : Window
{
    private readonly FileLog _log = new();
    private MainViewModel? _vm;
    private ToggleNotification? _toast;

    // System tray: closing the window hides it here so typing keeps working
    // system-wide. The app only really exits from the tray menu (or Exit()).
    private WinForms.NotifyIcon? _tray;
    private bool _reallyExit;
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += (_, _) => OnStateChanged();
        DpiChanged += (_, _) => OnStateChanged();
        FitToWorkArea();
    }

    /// <summary>
    /// Open no larger than the screen, centred on it. The title strip is the app's own,
    /// so a window taller than the screen (780 on a 1366x768 laptop, whose work area is
    /// 728) would open with its logo and its close button above the top edge.
    /// </summary>
    private void FitToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, wa.Width);
        MinHeight = Math.Min(MinHeight, wa.Height);
        Width = Math.Min(Width, wa.Width);
        Height = Math.Min(Height, wa.Height);
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + (wa.Height - Height) / 2;
    }

    // ---- The app's own title bar ----
    //
    // The window keeps its frame (WindowChrome), so a maximised window still extends
    // past the screen edge by the width of the resize border that Windows no longer
    // draws. Pad the content by exactly that much, or the caption buttons and the
    // sidebar's top edge end up off-screen.

    private void OnStateChanged()
    {
        bool max = WindowState == WindowState.Maximized;
        Root.Margin = max ? MaximizedInset() : new Thickness(0);
        MaxButton.Content = max ? "\uE923" : "\uE922";   // restore / maximize glyphs
        MaxButton.ToolTip = max ? "Restore" : "Maximize";
        System.Windows.Automation.AutomationProperties.SetName(MaxButton, max ? "Restore" : "Maximize");
    }

    private Thickness MaximizedInset()
    {
        // The frame at THIS window's DPI: plain GetSystemMetrics answers for the primary
        // monitor, which is wrong for a window maximised on a monitor scaled differently.
        uint dpi = 96;
        try { dpi = GetDpiForWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle); } catch { }
        if (dpi == 0) dpi = (uint)Math.Round(96 * VisualTreeHelper.GetDpi(this).DpiScaleX);
        double scale = dpi / 96.0;
        int Metric(int index)
        {
            try { return GetSystemMetricsForDpi(index, dpi); }
            catch (EntryPointNotFoundException) { return GetSystemMetrics(index); }   // before Windows 10 1607
        }
        double x = (Metric(SM_CXSIZEFRAME) + Metric(SM_CXPADDEDBORDER)) / scale;
        double y = (Metric(SM_CYSIZEFRAME) + Metric(SM_CXPADDEDBORDER)) / scale;
        return new Thickness(x, y, x, y);
    }

    private const int SM_CXSIZEFRAME = 32, SM_CYSIZEFRAME = 33, SM_CXPADDEDBORDER = 92;
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    // Same as the old title-bar X: hide to the tray (see OnClosing).
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _log.Info("Application starting.");
            var assets = new AssetService();
            var bundle = assets.Load();

            var store = new SettingsStore();
            var settings = store.Load();

            _vm = new MainViewModel(bundle, settings, store, _log, new FontResolver(assets.ConfigRoot));
            _vm.ToggleNotified += ShowToast;
            DataContext = _vm;

            SetupTray();
            _vm.Start(); // install the global hook now that the pump is running
        }
        catch (ConfigException ex)
        {
            // FR-1: invalid/missing assets handled gracefully with a clear message.
            _log.Error("Asset load failed.", ex);
            ShowError($"Could not load language assets: {ex.Message}");
        }
        catch (Exception ex)
        {
            _log.Error("Startup failed.", ex);
            ShowError($"Startup error: {ex.Message}");
        }
    }

    // ---- System tray ----

    private void SetupTray()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _tray = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "TextSoft — Beyond Typing",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info is not null)
                return new System.Drawing.Icon(info.Stream);
        }
        catch { /* fall through to a stock icon */ }
        return System.Drawing.SystemIcons.Application;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;   // pop to front, then release so it behaves normally
        Topmost = false;
    }

    /// <summary>Really quit: release the hook and shut the app down.</summary>
    private void Exit()
    {
        _reallyExit = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // X on the window minimizes to the tray instead of quitting, so the
        // global typing hook stays active. Real exit comes from the tray menu.
        if (_reallyExit)
            return;

        e.Cancel = true;
        Hide();

        if (!_trayHintShown)
        {
            _trayHintShown = true;
            _tray?.ShowBalloonTip(2500, "TextSoft is still running",
                "TextSoft is in the tray so typing keeps working. Right-click the tray icon to exit.",
                WinForms.ToolTipIcon.Info);
        }
    }

    private void ShowToast(bool enabled, string language, string font)
    {
        _toast ??= new ToggleNotification();
        _toast.Show(enabled, language, font);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        _toast?.Close();
        _vm?.Dispose();
    }
}
