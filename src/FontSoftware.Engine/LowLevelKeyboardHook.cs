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
    /// Set to true to swallow the original keystroke so it never reaches the
    /// focused app (FR-3: "original key suppressed").
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Installs a system-wide low-level keyboard hook (WH_KEYBOARD_LL) and raises an
/// event for every key. Must be created and disposed on a thread that pumps a
/// Windows message loop (the WPF UI thread).
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
            bool injected = data.dwExtraInfo == InputInjector.InjectionSignature;

            var args = new HookKeyEventArgs
            {
                VirtualKey = (int)data.vkCode,
                IsKeyDown = isDown,
                Injected = injected
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
