using System.Runtime.InteropServices;
using FontSoftware.Core.Diagnostics;

namespace FontSoftware.Engine;

/// <summary>
/// Counts mouse button presses, system-wide.
///
/// The engine remembers the word it is typing - the run it may still rewrite,
/// the keystrokes Backspace takes back whole - on the understanding that the
/// caret is where it left it. A click can move the caret without a single key,
/// and a keyboard hook never sees one: Backspace after clicking back into the
/// text then deleted whole keystrokes of the old word from wherever the caret now
/// was. The engine compares this count on every key and forgets the word when it
/// has moved.
///
/// The callback does nothing but count, on a thread that does nothing else, so
/// the mouse never waits on TextSoft - a low-level mouse hook is called for every
/// movement, and one that answers late makes the pointer stutter for everyone.
/// </summary>
public sealed class MouseClickWatch : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207, WM_XBUTTONDOWN = 0x020B;
    private const uint WM_QUIT = 0x0012;

    private readonly IAppLog _log;
    private readonly LowLevelMouseProc _proc;   // kept alive for the hook's lifetime
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private IntPtr _hook;
    private uint _threadId;
    private long _clicks;
    private bool _disposed;

    /// <summary>How many times a mouse button has gone down since this started.</summary>
    public long Clicks => Interlocked.Read(ref _clicks);

    public MouseClickWatch(IAppLog? log = null)
    {
        _log = log ?? NullLog.Instance;
        _proc = Callback;
        _thread = new Thread(Run) { IsBackground = true, Name = "TextSoft mouse clicks", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait(5000);
    }

    private void Run()
    {
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);   // a queue of its own, before WM_QUIT can be posted to it
        _threadId = GetCurrentThreadId();
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            _log.Warn($"Mouse clicks: the hook could not be installed (Win32 error {Marshal.GetLastWin32Error()}); " +
                      "a click back into the text will not end the word being typed.");
        _ready.Set();
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0) { }
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
            Interlocked.Increment(ref _clicks);
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(2000);
        _ready.Dispose();
    }

    // ---- Win32 ----

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
