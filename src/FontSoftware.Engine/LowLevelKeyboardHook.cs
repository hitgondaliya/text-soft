using System.Runtime.InteropServices;
using static FontSoftware.Engine.NativeMethods;

namespace FontSoftware.Engine;

/// <summary>Arguments for a single intercepted key event.</summary>
public sealed class HookKeyEventArgs : EventArgs
{
    public int VirtualKey { get; init; }
    public bool IsKeyDown { get; init; }

    /// <summary>True if the OS reports this event as synthesized (injected).</summary>
    public bool Injected { get; init; }

    /// <summary>
    /// One of ours: the key-up the injector sends for a Ctrl the typist is still
    /// holding (see InputInjector.LiftSignature). Always also <see cref="Injected"/>.
    /// </summary>
    public bool Lift { get; init; }

    /// <summary>
    /// Set to true to swallow the original keystroke so it never reaches the
    /// focused app (FR-3: "original key suppressed").
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Installs a system-wide low-level keyboard hook (WH_KEYBOARD_LL) and raises an
/// event for every key. Must be installed and disposed on a thread that pumps a
/// Windows message loop - in the app, a thread of its own (see KeyboardThread).
///
/// Reliability (FRD §NFR): the hook is fully released on Dispose; if the process
/// exits, Windows tears the hook down, so it cannot permanently block input.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    // Keep the delegate alive for the hook's lifetime, or the GC will collect it
    // and the callback will crash.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _disposed;

    // When the hook last saw each key go down - what the watchdog checks the
    // keyboard against (see MissedAKey).
    private readonly long[] _lastDown = new long[256];

    // The keys a typist types with: letters, digits, space and punctuation.
    private static readonly int[] Watched = Enumerable.Range('A', 26)
        .Concat(Enumerable.Range('0', 10))
        .Concat(new[] { 0x20, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE })
        .ToArray();

    public event EventHandler<HookKeyEventArgs>? KeyEvent;

    public LowLevelKeyboardHook()
    {
        _proc = HookCallback;
    }

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>Install the hook. Idempotent.</summary>
    public void Install()
    {
        if (_hook != IntPtr.Zero)
            return;

        IntPtr module = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, module, 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    /// <summary>
    /// True when a key is down right now that this hook never saw go down - which
    /// happens only once Windows has stopped calling it (a hook that answers too
    /// slowly is removed without notice). A key held down auto-repeats, and every
    /// repeat is a key-down the hook sees, so a held key does not count.
    /// </summary>
    public bool MissedAKey()
    {
        long now = Environment.TickCount64;
        foreach (int vk in Watched)
            if ((GetAsyncKeyState(vk) & 0x8000) != 0 && now - Volatile.Read(ref _lastDown[vk]) > 1000)
                return true;
        return false;
    }

    /// <summary>
    /// Put the hook back after Windows dropped it. On the thread that installed it,
    /// like <see cref="Install"/>. The keys down at this moment went past the old
    /// hook; they are not missed again.
    /// </summary>
    public void Reinstall()
    {
        Uninstall();
        Install();
        long now = Environment.TickCount64;
        foreach (int vk in Watched)
            if ((GetAsyncKeyState(vk) & 0x8000) != 0) Volatile.Write(ref _lastDown[vk], now);
    }

    /// <summary>Remove the hook. Safe to call when not installed.</summary>
    public void Uninstall()
    {
        if (_hook == IntPtr.Zero)
            return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam)!;
        int msg = wParam.ToInt32();
        bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool isUp = msg is WM_KEYUP or WM_SYSKEYUP;

        if (isDown || isUp)
        {
            // Ours, and only ours. Every event the injector sends carries its
            // signature, so that alone stops the engine reading its own output.
            // Judging by the system's "injected" flag instead disowned everyone
            // else's synthesized keys too - the Windows on-screen keyboard, a
            // remapper, a remote session - and the product simply did not type for
            // anyone using one.
            bool lift = data.dwExtraInfo == InputInjector.LiftSignature;
            bool injected = lift || data.dwExtraInfo == InputInjector.InjectionSignature;

            if (isDown && data.vkCode < 256) Volatile.Write(ref _lastDown[data.vkCode], Environment.TickCount64);

            var args = new HookKeyEventArgs
            {
                VirtualKey = (int)data.vkCode,
                IsKeyDown = isDown,
                Injected = injected,
                Lift = lift
            };

            try
            {
                KeyEvent?.Invoke(this, args);
            }
            catch
            {
                // A subscriber must never be able to break the global hook and
                // freeze the keyboard for the whole OS. Swallow and pass through.
            }

            if (args.Handled)
                return new IntPtr(1); // suppress original keystroke
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Uninstall();
        _disposed = true;
    }
}
