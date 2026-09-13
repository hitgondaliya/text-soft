using System.Runtime.InteropServices;
using System.Text;

namespace FontSoftware.Engine;

/// <summary>
/// Puts a piece of text on the clipboard as RTF (with a named font) plus plain
/// text, so a Ctrl+V into Word / WordPad / Outlook lands the text already in the
/// legacy font — the font-follows-the-layout behaviour Shree-Lipi users expect.
/// Raw Win32 clipboard calls, because the engine assembly has no WPF/WinForms.
/// </summary>
internal static class RichTextClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private static readonly uint CfRtf = RegisterClipboardFormatW("Rich Text Format");

    /// <summary>Build a minimal RTF document: one font, one paragraph, no formatting.</summary>
    public static string BuildRtf(string text, string fontName)
    {
        var sb = new StringBuilder(text.Length * 8 + 64);
        sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil ").Append(Escape(fontName)).Append(";}}");
        sb.Append(@"\uc1\f0 ");
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '{': sb.Append(@"\{"); break;
                case '}': sb.Append(@"\}"); break;
                case '\n': sb.Append(@"\par "); break;
                case '\r': break;
                case '\t': sb.Append(@"\tab "); break;
                default:
                    if (c < 0x80) sb.Append(c);
                    else sb.Append(@"\u").Append((short)c).Append('?'); // signed 16-bit, '?' fallback
                    break;
            }
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\\", "").Replace("{", "").Replace("}", "").Replace(";", "");

    /// <summary>Replace the clipboard with RTF + text. Returns the previous plain text (or null).</summary>
    public static string? Set(string text, string fontName)
    {
        string? previous = null;
        if (!Open()) return null;
        try
        {
            previous = ReadText();
            EmptyClipboard();
            PutBytes(CfRtf, Encoding.ASCII.GetBytes(BuildRtf(text, fontName) + "\0"));
            PutBytes(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + "\0"));
        }
        finally
        {
            CloseClipboard();
        }
        return previous;
    }

    /// <summary>Put plain text back (used to restore what the user had copied).</summary>
    public static void SetText(string text)
    {
        if (!Open()) return;
        try
        {
            EmptyClipboard();
            PutBytes(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + "\0"));
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool Open()
    {
        // Another app may hold the clipboard for a moment; retry briefly.
        for (int i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    private static string? ReadText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        IntPtr h = GetClipboardData(CF_UNICODETEXT);
        if (h == IntPtr.Zero) return null;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(p); }
        finally { GlobalUnlock(h); }
    }

    private static void PutBytes(uint format, byte[] bytes)
    {
        IntPtr h = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (h == IntPtr.Zero) return;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return; }
        Marshal.Copy(bytes, 0, p, bytes.Length);
        GlobalUnlock(h);
        if (SetClipboardData(format, h) == IntPtr.Zero) GlobalFree(h); // ownership passes on success
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string lpszFormat);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr hMem);
}
