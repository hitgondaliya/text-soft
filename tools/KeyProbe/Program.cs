// KeyProbe - what a global keyboard hook can actually see about the modifier keys.
//
// The typing engine reads Ctrl and Shift with GetKeyState. That call answers from
// the calling THREAD's input state, which is only updated as that thread reads key
// messages - and a hook thread never reads the keys another window is receiving.
// GetAsyncKeyState answers from the physical keyboard. This measures the two, from
// inside the hook, while keys are really being pressed.
using System.Runtime.InteropServices;

const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_SPACE = 0x20;

[DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookExW(int id, HookProc fn, IntPtr mod, uint thread);
[DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hk, int code, IntPtr w, IntPtr l);
[DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hk);
[DllImport("kernel32.dll")] static extern IntPtr GetModuleHandleW(string? name);
[DllImport("user32.dll")] static extern short GetKeyState(int vk);
[DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);

var rows = new List<string>();
HookProc proc = (code, w, l) =>
{
    if (code >= 0 && (w == WM_KEYDOWN || w == WM_SYSKEYDOWN))
    {
        int vk = Marshal.ReadInt32(l);
        bool sync = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        bool async = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool sShift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
        bool aShift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        rows.Add($"  key 0x{vk:X2} {(char)(vk is >= 0x20 and < 0x7F ? vk : '.')}   " +
                 $"Ctrl: GetKeyState={sync,-5} GetAsyncKeyState={async,-5}   " +
                 $"Shift: GetKeyState={sShift,-5} GetAsyncKeyState={aShift}");
    }
    return CallNextHookEx(IntPtr.Zero, code, w, l);
};

IntPtr hook = SetWindowsHookExW(WH_KEYBOARD_LL, proc, GetModuleHandleW(null), 0);
if (hook == IntPtr.Zero) { Console.Error.WriteLine("could not install the hook"); return 1; }

static INPUT Key(ushort vk, bool up) => new()
{
    type = 1,
    u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } }
};

// Ctrl down, Space, v, Ctrl up - the escape and the key after it.
var script = new[]
{
    Key(VK_CONTROL, false), Key(VK_SPACE, false), Key(VK_SPACE, true), Key(VK_CONTROL, true),
    Key((ushort)'V', false), Key((ushort)'V', true),
    Key(VK_CONTROL, false), Key(VK_SHIFT, false), Key((ushort)'1', false), Key((ushort)'1', true),
    Key(VK_SHIFT, true), Key(VK_CONTROL, true),
};
foreach (var k in script)
{
    SendInput(1, new[] { k }, Marshal.SizeOf<INPUT>());
    Thread.Sleep(40);
    System.Windows.Forms.Application.DoEvents();
}
Thread.Sleep(300);
System.Windows.Forms.Application.DoEvents();
UnhookWindowsHookEx(hook);

Console.WriteLine("what the hook saw:");
foreach (var r in rows) Console.WriteLine(r);
return 0;

delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);

[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }
[StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
