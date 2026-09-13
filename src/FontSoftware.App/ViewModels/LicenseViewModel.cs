using System.Windows;
using System.Windows.Threading;
using FontSoftware.App.Infrastructure;
using FontSoftware.App.Services;
using FontSoftware.Licensing;

namespace FontSoftware.App.ViewModels;

/// <summary>Which screen the lock overlay shows.</summary>
public enum LicenseStep
{
    Checking,
    Activate,
    Register,
    Welcome,
    Active
}

/// <summary>
/// Online licensing panel (full-lock model, one key = one device). Drives the
/// three-step overlay — serial+key, registration, welcome — and the licence page,
/// and exposes <see cref="IsActive"/>, which gates the whole app.
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
        RetryCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsBusy);
        RegisterCommand = new RelayCommand(() => _ = RegisterAsync(), () => !IsBusy);
        ContinueCommand = new RelayCommand(() => Step = LicenseStep.Active);
        UseAnotherKeyCommand = new RelayCommand(() => { _manager.Forget(); Step = LicenseStep.Activate; LockReason = "Enter the serial number and key for this PC."; });

        Countries = CountryCatalog.All;
        _selectedCountry = CountryCatalog.Default;
        Status = "Checking licence…";
    }

    // ---- step / gating ----

    private LicenseStep _step = LicenseStep.Checking;
    public LicenseStep Step
    {
        get => _step;
        private set
        {
            if (!SetProperty(ref _step, value)) return;
            OnPropertyChanged(nameof(IsChecking));
            OnPropertyChanged(nameof(IsActivateStep));
            OnPropertyChanged(nameof(IsRegisterStep));
            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsLocked));
            IsActive = value == LicenseStep.Active;
        }
    }

    public bool IsChecking => Step == LicenseStep.Checking;
    public bool IsActivateStep => Step == LicenseStep.Activate;
    public bool IsRegisterStep => Step == LicenseStep.Register;
    public bool IsWelcomeStep => Step == LicenseStep.Welcome;
    /// <summary>Overlay visible. Everything behind it is disabled too.</summary>
    public bool IsLocked => Step != LicenseStep.Active;

    private bool _isActive;
    /// <summary>The app may work: valid, unexpired, registered licence on this PC.</summary>
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(EditionText));
                RefreshLicenceTexts();
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ActivateCommand.RaiseCanExecuteChanged();
                RetryCommand.RaiseCanExecuteChanged();
                RegisterCommand.RaiseCanExecuteChanged();
            }
        }
    }

#if DEBUG
    /// <summary>Debug-only developer switch (TEXTSOFT_DEV_UNLOCK=1). Compiled out of
    /// Release builds so it cannot ship as a backdoor.</summary>
    private static bool DevUnlock => Environment.GetEnvironmentVariable("TEXTSOFT_DEV_UNLOCK") == "1";
#endif

    // ---- activation inputs ----

    private string _serialInput = string.Empty;
    public string SerialInput { get => _serialInput; set => SetProperty(ref _serialInput, value); }

    private string _keyInput = string.Empty;
    public string KeyInput { get => _keyInput; set => SetProperty(ref _keyInput, value); }

    private string _lockTitle = "Activation required";
    public string LockTitle { get => _lockTitle; private set => SetProperty(ref _lockTitle, value); }

    private string _lockReason = "";
    public string LockReason { get => _lockReason; private set => SetProperty(ref _lockReason, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private bool _statusIsError;
    public bool StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }

    // ---- registration inputs ----

    public IReadOnlyList<Country> Countries { get; }

    private Country _selectedCountry;
    public Country SelectedCountry
    {
        get => _selectedCountry;
        set { if (value is not null && SetProperty(ref _selectedCountry, value)) RegPhoneError = null; }
    }

    private string _regName = "";
    public string RegName { get => _regName; set { if (SetProperty(ref _regName, value)) RegNameError = null; } }

    private string _regEmail = "";
    public string RegEmail { get => _regEmail; set { if (SetProperty(ref _regEmail, value)) RegEmailError = null; } }

    private string _regPhone = "";
    public string RegPhone { get => _regPhone; set { if (SetProperty(ref _regPhone, value)) RegPhoneError = null; } }

    private string? _regNameError;
    public string? RegNameError { get => _regNameError; private set => SetProperty(ref _regNameError, value); }

    private string? _regEmailError;
    public string? RegEmailError { get => _regEmailError; private set => SetProperty(ref _regEmailError, value); }

    private string? _regPhoneError;
    public string? RegPhoneError { get => _regPhoneError; private set => SetProperty(ref _regPhoneError, value); }

    // ---- licence page / welcome / banner texts ----

    public string EditionText => IsActive
        ? (_manager.Current.Edition == LicenseEdition.Full ? "Full" : "Demo")
        : "Not activated";

    private string _serialText = "";
    public string SerialText { get => _serialText; private set => SetProperty(ref _serialText, value); }

    private string _expiryText = "";
    public string ExpiryText { get => _expiryText; private set => SetProperty(ref _expiryText, value); }

    private string _daysLeftText = "";
    public string DaysLeftText { get => _daysLeftText; private set => SetProperty(ref _daysLeftText, value); }

    private string _registeredText = "";
    public string RegisteredText { get => _registeredText; private set => SetProperty(ref _registeredText, value); }

    public string DeviceText => Environment.MachineName;

    private string _welcomeTitle = "";
    public string WelcomeTitle { get => _welcomeTitle; private set => SetProperty(ref _welcomeTitle, value); }

    private string _welcomeBody = "";
    public string WelcomeBody { get => _welcomeBody; private set => SetProperty(ref _welcomeBody, value); }

    private bool _showExpiryBanner;
    /// <summary>Title-bar strip: demo licences always, full licences in the last 7 days.</summary>
    public bool ShowExpiryBanner { get => _showExpiryBanner; private set => SetProperty(ref _showExpiryBanner, value); }

    private string _expiryBannerText = "";
    public string ExpiryBannerText { get => _expiryBannerText; private set => SetProperty(ref _expiryBannerText, value); }

    public RelayCommand ActivateCommand { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand RegisterCommand { get; }
    public RelayCommand ContinueCommand { get; }
    public RelayCommand UseAnotherKeyCommand { get; }

    // ---- flow ----

    /// <summary>
    /// Called once at startup. If the installer left a verified serial+key behind,
    /// activate with them straight away; otherwise load the stored licence and
    /// re-validate.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var pending = PendingActivation.TryRead();
            if (pending is not null)
            {
                SerialInput = pending.Serial;
                KeyInput = pending.Key;
                Status = "Activating the licence entered during setup…";
                var r = await _manager.ActivateAsync(pending.Serial, pending.Key, Environment.MachineName).ConfigureAwait(true);
                if (r is LicenseResult.Valid or LicenseResult.RegistrationRequired)
                    PendingActivation.Clear();
                ApplyResult(r, afterActivation: true);
                return;
            }

            var result = await _manager.LoadAndRefreshAsync().ConfigureAwait(true);
            ApplyResult(result, afterActivation: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ActivateAsync()
    {
        string serial = LicenseManager.NormalizeSerial(SerialInput);
        string key = KeyInput.Trim().ToUpperInvariant();
        if (serial.Length == 0 || key.Length == 0)
        {
            SetStatus("Enter both the serial number and the licence key.", error: true);
            return;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(serial, @"^TXS-\d{4}-\d{4}$"))
        {
            SetStatus("The serial number looks like TXS-2026-0001.", error: true);
            return;
        }

        IsBusy = true;
        SetStatus("Activating…");
        try
        {
            var result = await _manager.ActivateAsync(serial, key, Environment.MachineName).ConfigureAwait(true);
            ApplyResult(result, afterActivation: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterAsync()
    {
        RegNameError = RegistrationValidator.NameError(RegName);
        RegEmailError = RegistrationValidator.EmailError(RegEmail);
        RegPhoneError = RegistrationValidator.PhoneError(SelectedCountry.Code, RegPhone, out string national);
        if (RegNameError is not null || RegEmailError is not null || RegPhoneError is not null)
        {
            SetStatus("Please correct the highlighted fields.", error: true);
            return;
        }

        IsBusy = true;
        SetStatus("Saving your details…");
        try
        {
            var info = new RegistrationInfo(RegName.Trim(), RegEmail.Trim(), SelectedCountry.Code, national);
            var result = await _manager.RegisterAsync(info).ConfigureAwait(true);
            if (result == LicenseResult.Rejected && _manager.LastDetail == "phone")
            {
                RegPhoneError = "The server did not accept this mobile number.";
                SetStatus("Please correct the highlighted fields.", error: true);
                Step = LicenseStep.Register;
                return;
            }
            ApplyResult(result, afterActivation: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Local re-evaluation (expiry, grace, clock) without forcing a server call —
    /// runs every minute so a licence that ends while the app is open stops it.
    /// Leaves the overlay alone while the person is mid-activation.
    /// </summary>
    public async Task ReevaluateAsync()
    {
        if (IsBusy || Step is LicenseStep.Checking or LicenseStep.Register or LicenseStep.Welcome) return;
        if (Step == LicenseStep.Activate && _manager.LastReason != LicenseResult.Offline) return;
        IsBusy = true;
        try
        {
            var result = await _manager.LoadAndRefreshAsync().ConfigureAwait(true);
            ApplyResult(result, afterActivation: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-check with the server (used by the lock screen's Retry and the timer).</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy || Step is LicenseStep.Register or LicenseStep.Welcome) return;
        IsBusy = true;
        try
        {
            var result = await _manager.HeartbeatAsync().ConfigureAwait(true);
            ApplyResult(result, afterActivation: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(LicenseResult r, bool afterActivation)
    {
        void Apply()
        {
            switch (r)
            {
                case LicenseResult.Valid:
                    RefreshLicenceTexts();
                    if (afterActivation)
                    {
                        BuildWelcome();
                        LockTitle = "All set";
                        Step = LicenseStep.Welcome;
                    }
                    else
                    {
                        Step = LicenseStep.Active;
                    }
                    SetStatus("Licence active on this PC.");
                    break;

                case LicenseResult.RegistrationRequired:
                    RefreshLicenceTexts();
                    LockTitle = "One last step";
                    LockReason = $"The key is now bound to this PC ({Environment.MachineName}). Tell us who is using it.";
                    Step = LicenseStep.Register;
                    SetStatus("");
                    break;

                default:
#if DEBUG
                    if (DevUnlock)
                    {
                        Step = LicenseStep.Active;
                        SetStatus("Developer unlock (TEXTSOFT_DEV_UNLOCK) — not a real licence.");
                        break;
                    }
#endif
                    // After a failed attempt the headline stays the instruction and the
                    // red status line carries the reason, so it is not said twice.
                    LockTitle = TitleFor(r);
                    LockReason = afterActivation ? MessageFor(LicenseResult.NotActivated, null) : MessageFor(r, _manager.LastDetail);
                    Step = LicenseStep.Activate;
                    SetStatus(afterActivation ? MessageFor(r, _manager.LastDetail) : "", error: afterActivation);
                    RefreshLicenceTexts();
                    break;
            }
        }

        if (_dispatcher.CheckAccess()) Apply();
        else _dispatcher.Invoke(Apply);
    }

    private void SetStatus(string text, bool error = false)
    {
        Status = text;
        StatusIsError = error;
    }

    private void RefreshLicenceTexts()
    {
        var cur = _manager.Current;
        bool have = cur.IsActive || _manager.LastReason == LicenseResult.RegistrationRequired;
        OnPropertyChanged(nameof(EditionText));
        SerialText = have ? cur.Serial : "";
        RegisteredText = cur.IsActive && cur.RegisteredName.Length > 0
            ? $"{cur.RegisteredName} · {cur.RegisteredEmail} · {cur.RegisteredPhone}"
            : (cur.IsActive ? "Registered on this PC" : "");

        if (!have)
        {
            ExpiryText = "";
            DaysLeftText = "";
            ShowExpiryBanner = false;
            ExpiryBannerText = "";
            return;
        }

        var exp = cur.ExpiryUtc;
        int? days = exp is { } e ? Math.Max(0, (int)Math.Ceiling((e - DateTime.UtcNow).TotalDays)) : null;
        ExpiryText = exp is { } x ? $"Valid until {x.ToLocalTime():d MMM yyyy}" : "Perpetual licence — never expires";
        DaysLeftText = days is { } d ? (d == 1 ? "1 day left" : $"{d} days left") : "";

        bool demo = cur.Edition == LicenseEdition.Demo;
        bool soon = days is { } dd && dd <= 7;
        ShowExpiryBanner = cur.IsActive && (demo || soon);
        ExpiryBannerText = !ShowExpiryBanner ? "" :
            days is null ? "Demo licence" :
            demo ? $"Demo licence · {DaysLeftText} · ends {exp!.Value.ToLocalTime():d MMM yyyy}" :
            $"Licence ends in {DaysLeftText.Replace(" left", "")} ({exp!.Value.ToLocalTime():d MMM yyyy}) — renew to keep typing";
    }

    private void BuildWelcome()
    {
        var cur = _manager.Current;
        bool demo = cur.Edition == LicenseEdition.Demo;
        WelcomeTitle = demo ? "Demo licence activated" : "TextSoft is activated";
        var exp = cur.ExpiryUtc;
        string period = exp is { } e
            ? $"It is valid until {e.ToLocalTime():dddd, d MMMM yyyy} — {DaysLeftText}."
            : "It never expires.";
        WelcomeBody = demo
            ? $"This is a demo licence for this PC. {period} When it ends, typing and conversion stop until a full licence is activated."
            : $"Your full licence is bound to this PC ({Environment.MachineName}) under serial {cur.Serial}. {period}";
    }

    private static string TitleFor(LicenseResult r) => r switch
    {
        LicenseResult.NotActivated => "Activate TextSoft",
        LicenseResult.Expired => "Licence expired",
        LicenseResult.Revoked => "Licence revoked",
        LicenseResult.AlreadyBound => "Key in use on another PC",
        LicenseResult.Released => "Licence moved",
        LicenseResult.Offline or LicenseResult.Network or LicenseResult.RateLimited => "Cannot reach the licence server",
        LicenseResult.ClockRolledBack => "Clock check failed",
        _ => "Activation required"
    };

    private static string MessageFor(LicenseResult r, string? detail) => r switch
    {
        LicenseResult.Valid => "Licence active on this PC.",
        LicenseResult.NotActivated => "Enter the serial number and licence key you received with your purchase.",
        LicenseResult.Invalid => "That licence key was not recognised. Check it and try again.",
        LicenseResult.SerialMismatch => "That key does not belong to this serial number. Check both against your purchase.",
        LicenseResult.Revoked => "This licence has been revoked. Please contact support.",
        LicenseResult.Expired => "This licence has expired. Typing and conversion are stopped until it is renewed — contact us to extend it.",
        LicenseResult.AlreadyBound => string.IsNullOrEmpty(detail)
            ? "This key is already activated on another PC. A key works on one PC only. To move it here, contact support."
            : $"This key is already activated on \"{detail}\". A key works on one PC only. To move it here, contact support.",
        LicenseResult.Released => "This licence was moved to another PC by support. Enter a different key to use TextSoft here.",
        LicenseResult.DeviceMismatch => "The licence file does not belong to this PC. Activate again with your serial number and key.",
        LicenseResult.Tampered => "The licence file is damaged. Activate again with your serial number and key.",
        LicenseResult.ClockRolledBack => "Your system clock is earlier than the last verified time. Fix the clock and connect to the internet.",
        LicenseResult.Offline => "The licence could not be re-verified for too long. Connect to the internet and press Retry.",
        LicenseResult.Network => "Could not reach the licence server. Check your internet connection and try again.",
        LicenseResult.RateLimited => "Too many attempts. Wait a few minutes and try again.",
        LicenseResult.Rejected => "The licence server rejected the request. Check the serial number and key.",
        LicenseResult.RegistrationRequired => "Please complete registration.",
        _ => "Could not verify the licence."
    };
}
