using FontSoftware.App.Infrastructure;

namespace FontSoftware.App.ViewModels;

/// <summary>
/// One key in the on-screen keyboard viewer (FR-2). Shows the mapped script
/// character for the active layout, or the English label when unmapped.
/// </summary>
public sealed class KeyCapViewModel : ObservableObject
{
    public int VirtualKey { get; }

    /// <summary>English label of the physical key (e.g. "A", ";").</summary>
    public string Label { get; }

    private string _display;
    /// <summary>Character currently produced by this key.</summary>
    public string Display
    {
        get => _display;
        set => SetProperty(ref _display, value);
    }

    private bool _isMapped;
    /// <summary>True when the active layout maps this key to a script character.</summary>
    public bool IsMapped
    {
        get => _isMapped;
        set => SetProperty(ref _isMapped, value);
    }

    public KeyCapViewModel(int virtualKey, string label)
    {
        VirtualKey = virtualKey;
        Label = label;
        _display = label;
    }
}
