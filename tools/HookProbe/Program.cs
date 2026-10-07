// HookProbe - does Windows really drop a keyboard hook that answers too slowly, and
// does KeyboardThread's watchdog put it back?
//
// Types a few letters into a window of its own (TypeProbe's Pad). The first key the
// hook sees, it sits on for two seconds - what a busy UI thread used to do to it,
// converting a document - and then counts how many of the next keys still reach it.
// Run once without the watchdog and once with it.
//
//   hookprobe [stall ms, default 6000]
using System.Runtime.InteropServices;
using FontSoftware.Engine;

var pad = new Pad();
SetForegroundWindow(pad.Handle);
Thread.Sleep(600);
// SetForegroundWindow is a request Windows may refuse; a click in the window is not.
if (GetWindowRect(pad.Handle, out RECT r))
{
    SetCursorPos((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
    Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    Thread.Sleep(700);
}

{
    IntPtr fg = GetForegroundWindow();
    GetWindowThreadProcessId(fg, out int who);
    Console.WriteLine($"pad {pad.Handle} front {fg} ({(who == 0 ? "?" : System.Diagnostics.Process.GetProcessById(who).ProcessName)}) rect {r.Left},{r.Top},{r.Right},{r.Bottom}");
}
int stallMs = args.Length > 0 ? int.Parse(args[0]) : 6000;
string duringStall = args.Length > 1 ? args[1] : "IJKLMNOP";
int without = Trial(watchdog: false);
int with = Trial(watchdog: true);
int dropped = Dropped();
pad.Close();
Console.WriteLine(without < 0 || with < 0 || dropped < 0 ? "STOPPED"
    : $"result: slow once - without watchdog {without}/7, with watchdog {with}/7; dropped behind its back - {dropped}/7 reached it");
return 0;

int Trial(bool watchdog)
{
    using var keyboard = new KeyboardThread();
    int seen = 0;
    bool stalled = false;
    keyboard.Hook.KeyEvent += (_, e) =>
    {
        if (!e.IsKeyDown) return;
        if (!stalled) { stalled = true; Thread.Sleep(stallMs); return; }
        Interlocked.Increment(ref seen);
    };
    if (watchdog) keyboard.Start();
    else keyboard.Invoke(keyboard.Hook.Install);

    if (!Tap('A')) return -1;                 // the hook sits on this one...
    foreach (char c in duringStall)           // ...while the typist types on
    {
        if (!Tap(c)) return -1;
        Thread.Sleep(400);
    }
    Thread.Sleep(stallMs);
    int before = seen;
    foreach (char c in "BCDEFGH")
    {
        if (!Tap(c)) return -1;
        Thread.Sleep(250);
    }
    Thread.Sleep(500);
    int after = seen - before;
    Console.WriteLine($"{(watchdog ? "with" : "without")} the watchdog: the hook saw {after} of the 7 keys after it was slow once; " +
                      $"put back {keyboard.Reinstalls} time(s)");
    return after;
}

// The hook taken away behind the app's back - what Windows does to a hook it gives up
// on - and then seven keys: does the watchdog notice and put it back?
int Dropped()
{
    using var keyboard = new KeyboardThread();
    int seen = 0;
    keyboard.Hook.KeyEvent += (_, e) => { if (e.IsKeyDown) Interlocked.Increment(ref seen); };
    keyboard.Start();
    var handle = typeof(LowLevelKeyboardHook).GetField("_hook", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    keyboard.Invoke(() => UnhookWindowsHookEx((IntPtr)handle.GetValue(keyboard.Hook)!));
    foreach (char c in "BCDEFGH")
    {
        if (!Tap(c)) return -1;
        Thread.Sleep(250);
    }
    Thread.Sleep(500);
    Console.WriteLine($"dropped behind its back: the hook saw {seen} of the 7 keys; put back {keyboard.Reinstalls} time(s)");
    return seen;
}

// One key, held as long as a typist holds one. Never into a window that is not ours.
bool Tap(char c)
{
    if (GetForegroundWindow() != pad.Handle)
    {
        Console.Error.WriteLine("STOPPED: the window in front is not the probe's; nothing more was typed.");
        return false;
    }
    Send((ushort)c, false);
    Thread.Sleep(110);
    Send((ushort)c, true);
    return true;
}

static void Send(ushort vk, bool up)
{
    var input = new INPUT { type = 1, ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } };
    SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
}

[DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] static extern void mouse_event(uint f, int x, int y, uint d, IntPtr extra);

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

// INPUT is a union of the mouse, keyboard and hardware structs; the mouse one is
// the largest, so the keyboard struct is padded out to its size.
[StructLayout(LayoutKind.Explicit, Size = 40)]
struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
