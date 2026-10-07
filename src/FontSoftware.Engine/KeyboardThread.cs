using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FontSoftware.Core.Diagnostics;

namespace FontSoftware.Engine;

/// <summary>
/// A thread of its own for the keyboard hook - and so for the typing engine, which
/// runs inside the hook's callback.
///
/// Windows gives a low-level hook only so long to answer each key, and from Windows
/// 7 on a hook that misses that limit is removed without notice: "the hook is
/// silently removed without being called. There is no way for the application to
/// know whether the hook is removed." The hook used to live on the UI thread, so
/// anything that kept that thread busy while a key went down - a document being
/// converted, a long file read - could cost the app its hook. From then on every
/// key typed the plain letter on its keycap, the app still said typing was on, and
/// only a restart brought it back. A client's demo machine did exactly that.
///
/// Nothing but the hook and the engine run here, so the UI can be as busy as it
/// likes. And because Windows can still drop a hook (a machine under load), a
/// watchdog looks every 50 ms for a key that is down without the hook
/// having seen it go down, and puts the hook back.
/// </summary>
public sealed class KeyboardThread : IDisposable
{
    private const uint WM_TIMER = 0x0113;
    private const uint WM_QUIT = 0x0012;
    private const uint WM_WORK = 0x8000 + 0x51;   // WM_APP + n: queued work is waiting
    private const uint PM_NOREMOVE = 0;
    private const uint WatchMs = 50;
    private const long ReinstallGapMs = 2000;      // at most one put-back this often
    private const long LogGapMs = 60_000;          // and one line in the log a minute

    private readonly IAppLog _log;
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;
    private IntPtr _timer;
    private long _lastReinstall = long.MinValue / 2;
    private long _lastLogged = long.MinValue / 2;
    private int _dropsSinceLogged;
    private bool _suspect;
    private bool _disposed;

    /// <summary>The hook, installed and called on this thread.</summary>
    public LowLevelKeyboardHook Hook { get; } = new();

    /// <summary>
    /// Called on this thread after the hook was put back: whatever the engine was in
    /// the middle of, the keys typed while it was gone went past it.
    /// </summary>
    public Action? Restored { get; set; }

    /// <summary>How many times the watchdog has put the hook back (for the log and the tests).</summary>
    public int Reinstalls { get; private set; }

    public KeyboardThread(IAppLog? log = null)
    {
        _log = log ?? NullLog.Instance;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "TextSoft keyboard",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    /// <summary>Install the hook and start the watchdog.</summary>
    public void Start() => Invoke(() =>
    {
        Hook.Install();
        if (_timer == IntPtr.Zero) _timer = SetTimer(IntPtr.Zero, IntPtr.Zero, WatchMs, IntPtr.Zero);
    });

    /// <summary>
    /// Run <paramref name="work"/> on this thread and wait for it. Everything that
    /// touches the engine goes through here, so the engine only ever runs on one thread.
    /// </summary>
    public void Invoke(Action work)
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId || _disposed) { work(); return; }

        Exception? failed = null;
        using var done = new ManualResetEventSlim(false);
        _work.Enqueue(() =>
        {
            try { work(); }
            catch (Exception ex) { failed = ex; }
            finally { done.Set(); }
        });
        if (!PostThreadMessage(_threadId, WM_WORK, IntPtr.Zero, IntPtr.Zero))
        {
            // The thread has no queue any more (shutting down): do it here.
            if (_work.TryDequeue(out var mine)) mine();
        }
        if (!done.Wait(5000))
            _log.Warn("Keyboard thread: a change to the typing settings took longer than 5 s to apply.");
        if (failed is not null) throw new InvalidOperationException("Keyboard thread: " + failed.Message, failed);
    }

    private void Run()
    {
        // A thread has a message queue once it asks for a message; the hook and the
        // timer both need one before anything is posted to it.
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        _threadId = GetCurrentThreadId();
        _ready.Set();

        // The hook's callback is called from inside GetMessage, like a sent message.
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_WORK && msg.hwnd == IntPtr.Zero)
            {
                while (_work.TryDequeue(out var work)) work();
                continue;
            }
            if (msg.message == WM_TIMER && msg.hwnd == IntPtr.Zero)
            {
                Watch();
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        while (_work.TryDequeue(out var left)) left();
    }

    /// <summary>The watchdog: a key is down that the hook never saw go down - Windows dropped it.</summary>
    private void Watch()
    {
        if (!Hook.IsInstalled || !Hook.MissedAKey()) { _suspect = false; return; }

        // Seen once, it may only be late: a key that went down while this thread was
        // busy is still waiting to reach the hook, and those calls are made before
        // the next tick comes round. Still missed on that tick, it is not coming.
        if (!_suspect) { _suspect = true; return; }
        _suspect = false;

        long now = Environment.TickCount64;
        if (now - _lastReinstall < ReinstallGapMs) return;
        _lastReinstall = now;

        try
        {
            Hook.Reinstall();
            Reinstalls++;
            _dropsSinceLogged++;
            if (now - _lastLogged >= LogGapMs)
            {
                _log.Warn($"Keyboard hook: a key went down that the hook was never called for - Windows had dropped it; " +
                          $"put it back ({_dropsSinceLogged} time(s) since the last note).");
                _lastLogged = now;
                _dropsSinceLogged = 0;
            }
            Restored?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error("Keyboard hook: could not put it back.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            Invoke(() =>
            {
                if (_timer != IntPtr.Zero) { KillTimer(IntPtr.Zero, _timer); _timer = IntPtr.Zero; }
                Hook.Dispose();
            });
        }
        catch (Exception ex)
        {
            _log.Error("Keyboard thread: could not release the hook cleanly.", ex);
        }
        _disposed = true;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(2000);
        _ready.Dispose();
    }

    // ---- Win32 ----

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

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
