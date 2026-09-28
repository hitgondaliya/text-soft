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
    private static IntPtr _frame, _document;

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
            _frame = IntPtr.Zero;
            return false;
        }
    }

    /// <summary>
    /// The selection in the Word document the typist is looking at, or null.
    ///
    /// Asked of the document window itself first. Word hands out its object model
    /// through the window (OBJID_NATIVEOM), which needs nothing registered and
    /// can only ever be the Word in front. The running object table, which this
    /// used to rely on alone, is empty more often than not: Word only enters
    /// itself there once it has lost the focus at least once, so a Word opened
    /// and typed into straight away was "not running" - and every letter then
    /// went in as plain characters, in whatever font was at the caret.
    /// </summary>
    private static dynamic? SelectionInFront()
    {
        IntPtr front = GetForegroundWindow();
        if (front == IntPtr.Zero) { LastRefusal = "no window is in front"; return null; }

        IntPtr document = DocumentWindowOf(front);
        if (document != IntPtr.Zero)
        {
            Guid iid = IID_IDispatch;
            if (AccessibleObjectFromWindow(document, OBJID_NATIVEOM, ref iid, out object? window) == 0 && window is not null)
                return ((dynamic)window).Selection;
            LastRefusal = "the document window would not hand over Word";
        }
        else
        {
            LastRefusal = "the Word in front has no document open";
        }

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

    /// <summary>
    /// The window Word draws the document in (class "_WwG"), inside the Word
    /// frame that is in front. Remembered per frame: finding it walks the frame's
    /// child windows, and the frame in front rarely changes between two keys.
    /// </summary>
    private static IntPtr DocumentWindowOf(IntPtr front)
    {
        if (front == _frame && _document != IntPtr.Zero && IsWindow(_document)) return _document;

        _frame = front;
        _document = IntPtr.Zero;
        if (ClassOf(front) != "OpusApp") return IntPtr.Zero;

        IntPtr found = IntPtr.Zero;
        EnumChildWindows(front, (child, _) =>
        {
            if (ClassOf(child) == "_WwG" && IsWindowVisible(child)) { found = child; return false; }
            return true;
        }, IntPtr.Zero);
        _document = found;
        return found;
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

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid,
        [MarshalAs(UnmanagedType.IDispatch)] out object? ppvObject);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(in Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}
