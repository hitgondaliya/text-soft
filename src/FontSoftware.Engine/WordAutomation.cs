using System.Runtime.InteropServices;
using System.Text;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>
/// Types straight into Microsoft Word through its automation object: set the font
/// on the selection, insert the text. A clipboard paste cannot be trusted there —
/// Word's "pasting from other programs" option often strips the font — but the
/// object model always honours a font set on the selection. Used only when Word
/// is the foreground app; anything else gets keystrokes or an RTF paste.
/// </summary>
internal static class WordAutomation
{
    private static readonly Guid WordClsid = ClsidOf("Word.Application");
    private static DateTime _nextTry = DateTime.MinValue;
    private static IntPtr _checkedWindow;
    private static bool _checkedOwns;

    /// <summary>Why the last attempt to talk to Word did not happen, for the log.</summary>
    public static string LastRefusal { get; private set; } = string.Empty;

    /// <summary>
    /// Delete <paramref name="backspaces"/> characters before the caret, then insert
    /// <paramref name="text"/> in <paramref name="fontName"/>. False when Word is not
    /// reachable (caller falls back to keystrokes / paste).
    /// </summary>
    public static bool TryType(string text, int backspaces, string fontName)
    {
        if (WordClsid == Guid.Empty) { LastRefusal = "Word is not installed"; return false; }
        try
        {
            dynamic? sel = SelectionInFront();
            if (sel is null) return false;

            for (int i = 0; i < backspaces; i++) sel.TypeBackspace();
            if (text.Length > 0)
            {
                // Word files private-use characters under its complex-script font
                // slot (NameBi); Name alone leaves that slot on the theme font and the
                // text shows as boxes. Set both.
                sel.Font.Name = fontName;
                sel.Font.NameBi = fontName;
                sel.TypeText(text);
            }
            return true;
        }
        catch (Exception ex)
        {
            // Word busy (dialog open, RPC rejected) - let the keystroke path handle it.
            LastRefusal = "Word would not answer: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// The selection in the Word document the typist is typing into, or null.
    ///
    /// Only when the keyboard focus is in the page itself. With the focus in the
    /// Navigation search box, the ribbon's font box or a dialog, the keys are for
    /// that box - the document's selection would take them into the text behind
    /// it, and a Backspace would delete from the document.
    ///
    /// Word is then asked through that page window (OBJID_NATIVEOM), which needs
    /// nothing registered and can only be the Word in front. The running object
    /// table, which this used to rely on alone, is empty more often than not:
    /// Word only enters itself there once it has lost the focus, so a Word opened
    /// and typed into straight away was "not running" - and every letter then went
    /// in as plain characters, in whatever font was at the caret.
    /// </summary>
    private static dynamic? SelectionInFront()
    {
        IntPtr front = GetForegroundWindow();
        if (front == IntPtr.Zero) { LastRefusal = "no window is in front"; return null; }

        IntPtr focus = FocusIn(front);
        if (focus == IntPtr.Zero || ClassOf(focus) != "_WwG")
        {
            LastRefusal = "the keyboard focus in Word is not in the page (a search box, the ribbon or a dialog has it)";
            return null;
        }

        Guid iid = IID_IDispatch;
        if (AccessibleObjectFromWindow(focus, OBJID_NATIVEOM, ref iid, out object? window) == 0 && window is not null)
            return ((dynamic)window).Selection;
        LastRefusal = "the page would not hand over Word";

        // The old way, for a Word the window route cannot reach.
        if (DateTime.UtcNow < _nextTry) return null;
        if (GetActiveObject(WordClsid, IntPtr.Zero, out object app) != 0 || app is null)
        {
            _nextTry = DateTime.UtcNow.AddSeconds(5); // no running Word to talk to; don't retry per key
            LastRefusal += "; and no running Word registered itself for automation";
            return null;
        }
        dynamic word = app;
        if (!OwnsForegroundWindow(word))
        {
            LastRefusal = "the Word we can talk to is not the window in front";
            return null;   // a different Word - type into the focused window instead
        }
        return word.Selection;
    }

    /// <summary>The window with the keyboard focus in the thread that owns <paramref name="front"/>.</summary>
    private static IntPtr FocusIn(IntPtr front)
    {
        uint thread = GetWindowThreadProcessId(front, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(thread, ref info) ? info.hwndFocus : IntPtr.Zero;
    }

    private static string ClassOf(IntPtr window)
    {
        var name = new StringBuilder(64);
        return GetClassNameW(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
    }

    /// <summary>
    /// Is the Word we reached the one the typist is looking at?
    ///
    /// GetActiveObject hands back whichever Word registered itself first, which is
    /// not necessarily the window in front: a second Word process, or a hidden one
    /// left behind by a crash, takes every keystroke into a document nobody can
    /// see - and this method used to report success while the typist watched
    /// nothing appear. Comparing the process behind Word's own active window with
    /// the process behind the foreground window settles it; when they differ the
    /// caller falls back to keystrokes, which always go where the focus is.
    ///
    /// The answer is remembered per foreground window: it is a COM round trip, and
    /// the window in front rarely changes between two keystrokes.
    /// </summary>
    private static bool OwnsForegroundWindow(dynamic word)
    {
        IntPtr front = GetForegroundWindow();
        if (front == IntPtr.Zero) return false;
        if (front == _checkedWindow) return _checkedOwns;

        bool owns = false;
        try
        {
            IntPtr wordWindow = new((int)word.ActiveWindow.Hwnd);
            _ = GetWindowThreadProcessId(wordWindow, out uint wordPid);
            _ = GetWindowThreadProcessId(front, out uint frontPid);
            owns = wordPid != 0 && wordPid == frontPid;
        }
        catch
        {
            owns = false;   // no document open, or Word is busy - keystrokes it is
        }

        _checkedWindow = front;
        _checkedOwns = owns;
        return owns;
    }

    private static Guid ClsidOf(string progId)
    {
        try
        {
            return CLSIDFromProgID(progId, out Guid clsid) == 0 ? clsid : Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private const uint OBJID_NATIVEOM = 0xFFFFFFF0;
    private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public int left, top, right, bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid,
        [MarshalAs(UnmanagedType.IDispatch)] out object? ppvObject);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(in Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}
