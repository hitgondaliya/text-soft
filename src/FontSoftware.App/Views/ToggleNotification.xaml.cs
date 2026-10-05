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
    private static readonly System.Windows.Media.Brush OnBrush = Frozen(0x12, 0xB7, 0x6A);
    private static readonly System.Windows.Media.Brush OffBrush = Frozen(0x9C, 0xA2, 0xAB);

    private static System.Windows.Media.Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

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
        StatusDot.Fill = enabled ? OnBrush : OffBrush;
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
        // The card sits inside an 18px margin that holds its shadow.
        Left = area.Right - ActualWidth - 6;
        Top = area.Bottom - ActualHeight - 6;
    }
}
