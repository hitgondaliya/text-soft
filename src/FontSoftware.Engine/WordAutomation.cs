using System.Runtime.InteropServices;

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

    /// <summary>
    /// Delete <paramref name="backspaces"/> characters before the caret, then insert
    /// <paramref name="text"/> in <paramref name="fontName"/>. False when Word is not
    /// reachable (caller falls back to keystrokes / paste).
    /// </summary>
    public static bool TryType(string text, int backspaces, string fontName)
    {
        if (WordClsid == Guid.Empty || DateTime.UtcNow < _nextTry) return false;
        try
        {
            if (GetActiveObject(WordClsid, IntPtr.Zero, out object app) != 0 || app is null)
            {
                _nextTry = DateTime.UtcNow.AddSeconds(5); // no running Word to talk to; don't retry per key
                return false;
            }
            dynamic word = app;
            dynamic sel = word.Selection;
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
        catch
        {
            // Word busy (dialog open, RPC rejected) — let the keystroke path handle it.
            return false;
        }
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

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(in Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}
