using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// The Word document a test types into, reached through its own window
/// (OBJID_NATIVEOM) - the same road TextSoft takes - so reading it back needs
/// neither the clipboard nor the running object table. It is always a document
/// this tool created and saved under its own name, so nothing here can touch a
/// document of the typist's.
/// </summary>
static class WordDoc
{
    public const string Marker = "textsoft-typing-test";

    /// <summary>The Word frame showing our test document, or zero.</summary>
    public static IntPtr FindOurFrame()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((w, _) =>
        {
            if (Class(w) == "OpusApp" && Title(w).Contains(Marker, StringComparison.OrdinalIgnoreCase)) { found = w; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>Any Word frame at all (a freshly started Word, before we have named its document).</summary>
    public static List<IntPtr> AllFrames()
    {
        var all = new List<IntPtr>();
        EnumWindows((w, _) => { if (Class(w) == "OpusApp") all.Add(w); return true; }, IntPtr.Zero);
        return all;
    }

    public static dynamic? Window(IntPtr frame)
    {
        IntPtr doc = IntPtr.Zero;
        EnumChildWindows(frame, (c, _) => { if (Class(c) == "_WwG") { doc = c; return false; } return true; }, IntPtr.Zero);
        if (doc == IntPtr.Zero) return null;
        Guid iid = new("00020400-0000-0000-C000-000000000046");
        return AccessibleObjectFromWindow(doc, 0xFFFFFFF0, ref iid, out object? w) == 0 ? w : null;
    }

    /// <summary>Name the new document ours, so later runs find it and nothing else.</summary>
    public static void Claim(IntPtr frame, string folder)
    {
        dynamic w = Window(frame) ?? throw new InvalidOperationException("the new Word window has no document");
        string path = Path.Combine(folder, Marker + ".docx");
        if (File.Exists(path)) File.Delete(path);
        w.Document.SaveAs2(path);
    }

    public static void Clear(IntPtr frame)
    {
        dynamic w = Window(frame)!;
        w.Document.Content.Delete();
    }

    /// <summary>What the document holds: its text, and each stretch of it with the font it is in.</summary>
    public static (string Text, List<(string Font, string Text)> Runs) Read(IntPtr frame)
    {
        dynamic w = Window(frame)!;
        dynamic content = w.Document.Content;
        string text = ((string)content.Text).TrimEnd('\r');
        var runs = new List<(string, string)>();
        int n = (int)content.Characters.Count;
        string? font = null;
        var run = new StringBuilder();
        for (int i = 1; i <= n; i++)
        {
            dynamic ch = content.Characters.Item(i);
            string t = (string)ch.Text;
            if (t == "\r") continue;
            // A private-use or legacy character is drawn by the complex-script font
            // slot in Word; report whichever slot actually applies.
            string f = (string)ch.Font.NameBi is { Length: > 0 } bi && t.Any(c => c > 0x7F && c is < 'ऀ' or > '෿') ? bi : (string)ch.Font.Name;
            if (font is not null && f != font) { runs.Add((font, run.ToString())); run.Clear(); }
            font = f;
            run.Append(t);
        }
        if (font is not null) runs.Add((font, run.ToString()));
        return (text, runs);
    }

    /// <summary>Close our document without saving, and Word with it if nothing else is open.</summary>
    public static void Close(IntPtr frame)
    {
        try
        {
            dynamic w = Window(frame)!;
            dynamic app = w.Application;
            w.Document.Close(0);                 // wdDoNotSaveChanges
            if ((int)app.Documents.Count == 0) app.Quit(0);
        }
        catch { /* Word already gone */ }
    }

    public static string Class(IntPtr w)
    {
        var s = new StringBuilder(64);
        return GetClassNameW(w, s, s.Capacity) > 0 ? s.ToString() : string.Empty;
    }

    public static string Title(IntPtr w)
    {
        var s = new StringBuilder(256);
        return GetWindowTextW(w, s, s.Capacity) > 0 ? s.ToString() : string.Empty;
    }

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc f, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object? obj);
}
