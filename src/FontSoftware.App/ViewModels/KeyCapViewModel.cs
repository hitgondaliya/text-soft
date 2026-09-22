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

    private bool _isSelected;
    /// <summary>True for the key the keyboard designer is currently assigning.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public KeyCapViewModel(int virtualKey, string label)
    {
        VirtualKey = virtualKey;
        Label = label;
        _display = label;
    }
}

/// <summary>
/// Which layer of the keyboard the viewer shows. A vendor keyboard has four: the
/// two printed on the keycaps, the keyboard's own third layer on Ctrl+Space, and
/// the symbols every keyboard of the script shares on Ctrl+Shift+1.
/// </summary>
public enum KeyLayer
{
    Normal,
    Shift,
    Escape,
    Symbols
}
