// TypeProbe - press keys at the running product for real and read back what it
// typed. Unit tests drive the engine's own methods; this drives Windows, with the
// app running, the hook installed and a real window receiving the text, which is
// the only way to answer "does Ctrl+Space work" rather than "does my test pass".
//
//   typeprobe "^ v" "k" ...
// Each argument is one keystroke: a bare character, or modifiers before it -
// ^ = Ctrl, + = Shift, and "caps" for the toggle key. Reads Notepad at the end.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

const int VK_CAPITAL = 0x14, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_SPACE = 0x20;

[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] static extern void mouse_event(uint f, int x, int y, uint d, IntPtr extra);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string? cls, string? title);
[DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
[DllImport("user32.dll")] static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string? cls, string? title);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr wp, StringBuilder lp);

// --any drives whatever keyboard software is running - Shree-Lipi, to see what
// it does with the same keys - instead of insisting on ours.
bool any = args.Contains("--any");
if (!any && Process.GetProcessesByName("TextSoft").Length == 0)
{
    Console.Error.WriteLine("TextSoft is not running - start it first, this is a test of the real thing.");
    return 1;
}

// Somewhere to type. Notepad is the plain case; Word is the one that matters,
// because a legacy font reaches it by a different road - through the clipboard.
bool word = args.Contains("--word");
string? shot = args.FirstOrDefault(a => a.StartsWith("--shot="))?.Substring(7);
args = args.Where(a => a != "--word" && a != "--any" && !a.StartsWith("--shot=")).ToArray();
// /w opens Word on a blank document rather than its start screen, which has
// nowhere to type.
var start = word ? new ProcessStartInfo("winword.exe", "/w") : new ProcessStartInfo("notepad.exe");
start.UseShellExecute = true;
var pad = Process.Start(start)!;
Thread.Sleep(word ? 14000 : 1500);
pad.Refresh();

// Get our own window out of the way first - it is on top, and every attempt to
// focus the target lands on us instead.
foreach (var ts in Process.GetProcessesByName("TextSoft").Concat(Process.GetProcessesByName("SLNXT")))
    if (ts.MainWindowHandle != IntPtr.Zero) ShowWindow(ts.MainWindowHandle, 6);   // SW_MINIMIZE
Thread.Sleep(600);

// Word's own frame window, which is there before the process reports a main one.
IntPtr target = FindWindowW(word ? "OpusApp" : "Notepad", null);
if (target == IntPtr.Zero) target = pad.MainWindowHandle;
ShowWindow(target, 9);   // SW_RESTORE
SetForegroundWindow(target);
Thread.Sleep(600);

// SetForegroundWindow is a request, not an order - Windows refuses it to a
// process that has not been touched. Clicking in the window is not refused.
if (GetWindowRect(target, out RECT wr))
{
    SetCursorPos(wr.Left + (wr.Right - wr.Left) / 2, wr.Top + (wr.Bottom - wr.Top) * 2 / 3);
    Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);   // left down
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);   // left up
    Thread.Sleep(700);
}

static INPUT Key(ushort vk, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } } };
static void Send(INPUT i) { SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>()); Thread.Sleep(60); }

static ushort Named(string name) => name.ToLowerInvariant() switch
{
    "ctrl" => VK_CONTROL, "shift" => VK_SHIFT, "alt" => 0x12, "space" => VK_SPACE,
    "caps" => VK_CAPITAL, "enter" => 0x0D, "back" => 0x08, "esc" => 0x1B,
    var f when f.Length > 1 && f[0] == 'f' && int.TryParse(f[1..], out int n) => (ushort)(0x6F + n),
    var c when c.Length == 1 => VkOf(c[0]),
    _ => throw new ArgumentException("no such key: " + name)
};

static ushort VkOf(char c) => char.IsAsciiLetter(c) ? (ushort)char.ToUpperInvariant(c)
                            : c == ' ' ? (ushort)VK_SPACE : (ushort)c;

// Whoever has focus is who the engine will type into. Say so, because typing
// into the wrong window looks exactly like not typing at all.
GetWindowThreadProcessId(GetForegroundWindow(), out int fg);
Console.WriteLine($"focus: {(fg == 0 ? "?" : Process.GetProcessById(fg).ProcessName)}");

foreach (string stroke in args)
{
    // {ctrl down} ... {ctrl up} holds a key across other strokes, which is the only
    // way to ask whether Ctrl+Space then V works with Ctrl still held.
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
    if (stroke.StartsWith("wait="))
    {
        Thread.Sleep(int.Parse(stroke[5..]));
        continue;
    }
    if (stroke.Equals("caps", StringComparison.OrdinalIgnoreCase))
    {
        Send(Key(VK_CAPITAL, false)); Send(Key(VK_CAPITAL, true));
        Thread.Sleep(400);
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
Thread.Sleep(800);

// What landed. Notepad's own edit control has changed class more than once, so
// ask the window for its text the way a person would: select all, copy, read the
// clipboard. Ctrl+key is a shortcut, which the engine passes through untouched.
// Put a marker on the clipboard first. If the copy leaves it there, the document
// was empty - otherwise an empty document reads back as whatever was copied last,
// which is how a stale clipboard can look like a successful test.
const string sentinel = "<<nothing was typed>>";
var seed = new Thread(() => { try { System.Windows.Forms.Clipboard.SetText(sentinel); } catch { } });
seed.SetApartmentState(ApartmentState.STA); seed.Start(); seed.Join();

foreach (var k in new[] { Key(VK_CONTROL, false), Key((ushort)'A', false), Key((ushort)'A', true),
                          Key((ushort)'C', false), Key((ushort)'C', true), Key(VK_CONTROL, true) }) Send(k);
Thread.Sleep(600);

string got = string.Empty;
var reader = new Thread(() => { try { got = System.Windows.Forms.Clipboard.GetText(); } catch { } });
reader.SetApartmentState(ApartmentState.STA);
reader.Start();
reader.Join();

// A picture of the window is the last word on what the typist can see.
if (shot is not null && GetWindowRect(target, out RECT sr))
{
    int w2 = sr.Right - sr.Left, h2 = sr.Bottom - sr.Top;
    using var bmp = new System.Drawing.Bitmap(w2, h2);
    using (var g = System.Drawing.Graphics.FromImage(bmp))
        g.CopyFromScreen(sr.Left, sr.Top, 0, 0, new System.Drawing.Size(w2, h2));
    bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
    Console.WriteLine($"picture: {shot}");
}

Console.WriteLine($"typed: {JsonQuote(got)}");
Console.WriteLine($"       {string.Join(' ', got.Select(ch => ((int)ch).ToString("X4")))}");
try { pad.Kill(); } catch { }
return 0;

static string JsonQuote(string s) => "\"" + s.Replace("\r", "\r").Replace("\n", "\n") + "\"";

[StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }
[StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
