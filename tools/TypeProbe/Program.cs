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
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
[DllImport("user32.dll")] static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string? cls, string? title);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr wp, StringBuilder lp);

if (Process.GetProcessesByName("TextSoft").Length == 0)
{
    Console.Error.WriteLine("TextSoft is not running - start it first, this is a test of the real thing.");
    return 1;
}

// A plain Notepad to type into, so what lands is text and nothing else.
var pad = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true })!;
Thread.Sleep(1500);
pad.Refresh();
SetForegroundWindow(pad.MainWindowHandle);
Thread.Sleep(600);

static INPUT Key(ushort vk, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } } };
static void Send(INPUT i) { SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>()); Thread.Sleep(60); }

static ushort VkOf(char c) => char.IsAsciiLetter(c) ? (ushort)char.ToUpperInvariant(c)
                            : c == ' ' ? (ushort)VK_SPACE : (ushort)c;

foreach (string stroke in args)
{
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
foreach (var k in new[] { Key(VK_CONTROL, false), Key((ushort)'A', false), Key((ushort)'A', true),
                          Key((ushort)'C', false), Key((ushort)'C', true), Key(VK_CONTROL, true) }) Send(k);
Thread.Sleep(600);

string got = string.Empty;
var reader = new Thread(() => { try { got = System.Windows.Forms.Clipboard.GetText(); } catch { } });
reader.SetApartmentState(ApartmentState.STA);
reader.Start();
reader.Join();

Console.WriteLine($"typed: {JsonQuote(got)}");
Console.WriteLine($"       {string.Join(' ', got.Select(ch => ((int)ch).ToString("X4")))}");
try { pad.Kill(); } catch { }
return 0;

static string JsonQuote(string s) => "\"" + s.Replace("\r", "\r").Replace("\n", "\n") + "\"";

[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }
[StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
