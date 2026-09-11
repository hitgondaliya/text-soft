using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FontSoftware.App.Views;

/// <summary>
/// Brief bottom-right toast shown when typing is toggled (FR-3: "an on-screen
/// notification names the active language and font"). Fades in, holds, fades out.
/// </summary>
public partial class ToggleNotification : Window
{
    private readonly DispatcherTimer _timer;

    public ToggleNotification()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _timer.Tick += (_, _) => { _timer.Stop(); FadeOut(); };
        Loaded += (_, _) => PositionBottomRight();
    }

    /// <summary>Show the toast with the current state and active profile.</summary>
    public void Show(bool enabled, string languageName, string fontName)
    {
        StatusText.Text = enabled ? "Typing ON" : "Typing OFF";
        ProfileText.Text = enabled ? $"{languageName} · {fontName}" : "English";
        ProfileText.Visibility = string.IsNullOrWhiteSpace(ProfileText.Text)
            ? Visibility.Collapsed : Visibility.Visible;

        PositionBottomRight();
        Opacity = 0;
        base.Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        _timer.Stop();
        _timer.Start();
    }

    private void FadeOut()
    {
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, fade);
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 24;
        Top = area.Bottom - ActualHeight - 24;
    }
}
