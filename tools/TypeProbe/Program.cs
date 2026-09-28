// TypeProbe - press keys at the running product for real and read back what it
// typed. Unit tests drive the engine's own methods; this drives Windows, with the
// app running, the hook installed and a real window receiving the text, which is
// the only way to answer "does Ctrl+Space work" rather than "does my test pass".
//
//   typeprobe [--word [--keep]] [--any] [--shot=file.png] <strokes...>
//
// Each stroke is one keystroke: a bare character, or modifiers before it -
// ^ = Ctrl, + = Shift - or "caps" for the toggle key, or {key down} / {key up} /
// {key} to hold a key across other strokes ({ctrl down} {space} {v} {ctrl up} is
// Ctrl held through Space and V), or wait=ms.
//
// Without --word it types into a window of its own (see Pad) and reads it back.
// --word types into a Word document of this tool's own (textsoft-typing-test.docx),
// never one of the typist's, and reads it back through Word itself, with the font
// of every stretch - no clipboard. --keep leaves it open for the next run.
// --any drives whatever keyboard software is running (Shree-Lipi, to compare).
//
// Before every keystroke it checks the window in front is still the one it is
// typing into, and stops if not: keys meant for a test must never land in a
// window somebody is working in.
using System.Diagnostics;
using System.Runtime.InteropServices;

const int VK_CAPITAL = 0x14, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_SPACE = 0x20;

[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] static extern void mouse_event(uint f, int x, int y, uint d, IntPtr extra);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string? cls, string? title);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string? cls, string? title);
[DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

bool word = args.Contains("--word");
bool keep = args.Contains("--keep");
bool any = args.Contains("--any");
string? shot = args.FirstOrDefault(a => a.StartsWith("--shot="))?.Substring(7);
args = args.Where(a => !a.StartsWith("--")).ToArray();

if (!any && Process.GetProcessesByName("TextSoft").Length == 0)
{
    Console.Error.WriteLine("TextSoft is not running - start it first, this is a test of the real thing.");
    return 1;
}

// ---- somewhere to type ----
IntPtr target;
Pad? pad = null;
if (word)
{
    target = WordDoc.FindOurFrame();
    if (target == IntPtr.Zero)
    {
        var before = WordDoc.AllFrames();
        Process.Start(new ProcessStartInfo("winword.exe", "/w") { UseShellExecute = true });
        // Word is slow to start and slower to be ready; wait for a new frame with a document in it.
        for (int i = 0; i < 90 && target == IntPtr.Zero; i++)
        {
            Thread.Sleep(500);
            target = WordDoc.AllFrames().Except(before).FirstOrDefault(f => WordDoc.Window(f) is not null);
        }
        if (target == IntPtr.Zero) { Console.Error.WriteLine("Word did not open a document."); return 1; }
        Thread.Sleep(1500);
        WordDoc.Claim(target, Path.GetTempPath());
    }
    WordDoc.Clear(target);
}
else
{
    pad = new Pad();
    target = pad.Handle;
}

// Our own window, and Shree's, are on top and take the focus from the target.
foreach (var p in Process.GetProcessesByName("TextSoft").Concat(Process.GetProcessesByName("SLNXT")))
    if (p.MainWindowHandle != IntPtr.Zero) ShowWindow(p.MainWindowHandle, 6);   // SW_MINIMIZE
Thread.Sleep(600);
ShowWindow(target, 9);   // SW_RESTORE
SetForegroundWindow(target);
Thread.Sleep(600);

// SetForegroundWindow is a request Windows may refuse; a click in the page is not
// refused. For Word, the page itself - not the frame, where a recovery pane or the
// ribbon may be.
IntPtr page = word ? DocWindow(target) : target;
if (GetWindowRect(page, out RECT wr))
{
    SetCursorPos(wr.Left + (wr.Right - wr.Left) / 2, wr.Top + (wr.Bottom - wr.Top) / 2);
    Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);   // left down
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);   // left up
    Thread.Sleep(700);
}

GetWindowThreadProcessId(GetForegroundWindow(), out int fg);
Console.WriteLine($"focus: {(fg == 0 ? "?" : Process.GetProcessById(fg).ProcessName)}");

static INPUT Key(ushort vk, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } } };
static void Send(INPUT i) { SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>()); Thread.Sleep(60); }

// Never type into a window that is not ours to type into. If the focus moved -
// the typist clicked somewhere, a dialog opened - let go of anything held and stop.
bool Guard()
{
    if (GetForegroundWindow() == target) return true;
    GetWindowThreadProcessId(GetForegroundWindow(), out int who);
    Console.Error.WriteLine($"STOPPED: the window in front is no longer the test's ({(who == 0 ? "?" : Process.GetProcessById(who).ProcessName)}); nothing more was typed.");
    Send(Key(VK_CONTROL, true)); Send(Key(VK_SHIFT, true)); Send(Key(VK_SPACE, true));
    return false;
}

foreach (string stroke in args)
{
    if (stroke.StartsWith("wait="))
    {
        Thread.Sleep(int.Parse(stroke[5..]));
        continue;
    }
    if (!Guard()) return 3;
    if (stroke.Equals("caps", StringComparison.OrdinalIgnoreCase))
    {
        Send(Key(VK_CAPITAL, false)); Send(Key(VK_CAPITAL, true));
        Thread.Sleep(400);
        continue;
    }
    // {ctrl down} ... {ctrl up} holds a key across other strokes.
    if (stroke.StartsWith('{') && stroke.EndsWith('}'))
    {
        string[] part = stroke.Trim('{', '}').Split(' ');
        ushort vk = Named(part[0]);
        string how = part.Length > 1 ? part[1] : "tap";
        if (how != "up") Send(Key(vk, false));
        if (how != "down") Send(Key(vk, true));
        Thread.Sleep(150);
        continue;
    }
    bool ctrl = stroke.Contains('^'), shift = stroke.Contains('+');
    char c = stroke.TrimStart('^', '+')[0];
    if (ctrl) Send(Key(VK_CONTROL, false));
    if (shift) Send(Key(VK_SHIFT, false));
    Send(Key(VkOf(c), false));
    Send(Key(VkOf(c), true));
    if (shift) Send(Key(VK_SHIFT, true));
    if (ctrl) Send(Key(VK_CONTROL, true));
    Thread.Sleep(200);
}
Thread.Sleep(1200);

// ---- what landed ----
string got;
if (word)
{
    var (text, runs) = WordDoc.Read(target);
    got = text;
    foreach (var (font, part) in runs)
        Console.WriteLine($"  in {font}: {Codes(part)}");
}
else
{
    got = pad!.Text;
}

if (shot is not null && GetWindowRect(target, out RECT sr))
{
    int w2 = sr.Right - sr.Left, h2 = sr.Bottom - sr.Top;
    using var bmp = new System.Drawing.Bitmap(w2, h2);
    using (var g = System.Drawing.Graphics.FromImage(bmp))
        g.CopyFromScreen(sr.Left, sr.Top, 0, 0, new System.Drawing.Size(w2, h2));
    bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
    Console.WriteLine($"picture: {shot}");
}

Console.WriteLine($"typed: {Codes(got)}");
if (word && !keep) WordDoc.Close(target);
pad?.Close();
return 0;

static string Codes(string s) => string.Join(' ', s.Select(ch => ((int)ch).ToString("X4")));

static IntPtr DocWindow(IntPtr frame)
{
    IntPtr found = IntPtr.Zero;
    foreach (var cls in new[] { "_WwF", "_WwB", "_WwG" })
    {
        IntPtr next = FindWindowExW(found == IntPtr.Zero ? frame : found, IntPtr.Zero, cls, null);
        if (next == IntPtr.Zero) break;
        found = next;
    }
    return found == IntPtr.Zero ? frame : found;
}

static ushort Named(string name) => name.ToLowerInvariant() switch
{
    "ctrl" => VK_CONTROL, "shift" => VK_SHIFT, "alt" => 0x12, "space" => VK_SPACE,
    "caps" => VK_CAPITAL, "enter" => 0x0D, "back" => 0x08, "esc" => 0x1B,
    var f when f.Length > 1 && f[0] == 'f' && int.TryParse(f[1..], out int n) => (ushort)(0x6F + n),
    var c when c.Length == 1 => VkOf(c[0]),
    _ => throw new ArgumentException("no such key: " + name)
};

static ushort VkOf(char c) => char.IsAsciiLetter(c) ? (ushort)char.ToUpperInvariant(c)
                            : c == ' ' ? (ushort)VK_SPACE
                            : (ushort)(c switch { ';' => 186, '=' => 187, ',' => 188, '-' => 189, '.' => 190, '/' => 191,
                                                  '`' => 192, '[' => 219, '\\' => 220, ']' => 221, '\'' => 222, _ => (int)c });

[StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }
[StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
