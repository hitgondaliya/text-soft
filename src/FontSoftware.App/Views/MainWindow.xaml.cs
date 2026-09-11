using System.ComponentModel;
using System.Windows;
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
    }

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
