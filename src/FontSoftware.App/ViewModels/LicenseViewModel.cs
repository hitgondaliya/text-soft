using System.Windows;
using System.Windows.Threading;
using FontSoftware.App.Infrastructure;
using FontSoftware.Licensing;

namespace FontSoftware.App.ViewModels;

/// <summary>
/// Online licensing panel (full-lock model). Activates a key against the server,
/// shows the current edition/expiry, and drives <see cref="IsActive"/> — which gates
/// the whole app. When not active the app shows the lock overlay bound to this VM.
/// </summary>
public sealed class LicenseViewModel : ObservableObject
{
    private readonly LicenseManager _manager;
    private readonly Dispatcher _dispatcher;

    public LicenseViewModel(LicenseManager manager)
    {
        _manager = manager;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        ActivateCommand = new RelayCommand(() => _ = ActivateAsync(), () => !IsBusy);
        DeactivateCommand = new RelayCommand(() => _ = DeactivateAsync(), () => IsActive && !IsBusy);
        RetryCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsBusy);

        Status = "Checking license…";
    }

    // ---- state ----

    private string _keyInput = string.Empty;
    public string KeyInput { get => _keyInput; set => SetProperty(ref _keyInput, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ActivateCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
                RetryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Developer switch: set the environment variable TEXTSOFT_DEV_UNLOCK=1 to run
    /// the app without activating, so features behind the licence gate can be tested
    /// on a machine with no key. Off unless that variable is set, and it only affects
    /// this view model — <see cref="LicenseManager"/> itself still reports the truth.
    /// </summary>
    public static bool DevUnlock =>
        Environment.GetEnvironmentVariable("TEXTSOFT_DEV_UNLOCK") == "1";

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(EditionText));
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLocked => !IsActive;

    public string EditionText => IsActive
        ? (_manager.Current.Edition == LicenseEdition.Full ? "Full" : "Demo")
        : "Not activated";

    private string _expiryText = "";
    public string ExpiryText { get => _expiryText; private set => SetProperty(ref _expiryText, value); }

    private string _lockReason = "";
    public string LockReason { get => _lockReason; private set => SetProperty(ref _lockReason, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public RelayCommand ActivateCommand { get; }
    public RelayCommand DeactivateCommand { get; }
    public RelayCommand RetryCommand { get; }

    // ---- flow ----

    /// <summary>Called once at startup: load the stored license and re-validate.</summary>
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _manager.LoadAndRefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            RefreshState(initial: true);
        }
    }

    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(KeyInput))
        {
            Status = "Enter your license key.";
            return;
        }

        IsBusy = true;
        Status = "Activating…";
        try
        {
            var result = await _manager.ActivateAsync(KeyInput.Trim(), Environment.MachineName).ConfigureAwait(true);
            Status = MessageFor(result, activating: true);
        }
        finally
        {
            IsBusy = false;
            RefreshState();
        }
    }

    private async Task DeactivateAsync()
    {
        IsBusy = true;
        Status = "Deactivating…";
        try
        {
            await _manager.DeactivateAsync().ConfigureAwait(true);
            Status = "This device has been deactivated.";
        }
        finally
        {
            IsBusy = false;
            RefreshState();
        }
    }

    /// <summary>Re-check with the server (used by the lock screen's Retry and the timer).</summary>
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _manager.HeartbeatAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            RefreshState();
        }
    }

    private void RefreshState(bool initial = false)
    {
        void Apply()
        {
            IsActive = _manager.IsActive || DevUnlock;
            OnPropertyChanged(nameof(EditionText));

            if (IsActive)
            {
                var exp = _manager.Current.ExpiryUtc;
                ExpiryText = exp is { } e ? $"Valid until {e.ToLocalTime():d MMM yyyy}" : "Perpetual license";
                LockReason = "";
                if (initial) Status = DevUnlock && !_manager.IsActive ? "Developer unlock (TEXTSOFT_DEV_UNLOCK) — not a real licence." : "License active on this device.";
            }
            else
            {
                ExpiryText = "";
                LockReason = MessageFor(_manager.LastReason, activating: false);
                if (initial) Status = LockReason;
            }
        }

        if (_dispatcher.CheckAccess()) Apply();
        else _dispatcher.Invoke(Apply);
    }

    private static string MessageFor(LicenseResult r, bool activating) => r switch
    {
        LicenseResult.Valid => activating ? "Activated. Thank you!" : "License active on this device.",
        LicenseResult.NotActivated => "Enter your license key to activate.",
        LicenseResult.Invalid => "That key was not recognized. Check it and try again.",
        LicenseResult.Revoked => "This license has been revoked. Please contact support.",
        LicenseResult.Expired => "This license has expired. Renew it to continue.",
        LicenseResult.DeviceMismatch => "This license is tied to a different device, or the license file is invalid. Re-activate on this PC.",
        LicenseResult.DeviceLimitReached => "This key has reached its device limit. Deactivate another device or contact support.",
        LicenseResult.Tampered => "The license file is invalid. Please re-activate.",
        LicenseResult.ClockRolledBack => "Your system clock looks incorrect. Connect to the internet so we can re-verify.",
        LicenseResult.Offline => "Could not reach the license server, and the offline period has ended. Connect to the internet to continue.",
        LicenseResult.Network => "Could not reach the license server. Check your internet connection and try again.",
        _ => "Could not verify the license."
    };
}
