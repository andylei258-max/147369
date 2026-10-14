using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TabletExtend.Windows;

internal sealed record KeyChord(uint Modifiers, uint Key)
{
    public static KeyChord Parse(string value)
    {
        uint mods = 0; uint key = 0;
        foreach (var part in value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL": mods |= 2; break;
                case "ALT": mods |= 1; break;
                case "SHIFT": mods |= 4; break;
                case "WIN": mods |= 8; break;
                default:
                    if (key != 0 || !Enum.TryParse<Keys>(part, true, out var parsed) || (uint)parsed > 255 || parsed is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
                        throw new ArgumentException("快捷键示例：Ctrl+Win、Ctrl+Win+K、Ctrl+Alt+F9");
                    key = (uint)parsed; break;
            }
        }
        if (mods == 0 || (key == 0 && mods != 10)) throw new ArgumentException("仅修饰键组合只支持 Ctrl+Win；其他组合需要一个字母或功能键。");
        return new(mods, key);
    }
}

internal sealed class Hotkeys : NativeWindow, IDisposable
{
    private readonly Native.HookProc hookProc;
    private IntPtr hook;
    private readonly Action toggleCross, toggleLock;
    private readonly HashSet<int> down = [];
    private bool armed, used;
    private bool modifierOnly;
    public string? Warning { get; private set; }
    public string Crossing { get; private set; } = "";
    public string Locking { get; private set; } = "";

    public Hotkeys(Action toggleCross, Action toggleLock)
    {
        this.toggleCross = toggleCross; this.toggleLock = toggleLock;
        hookProc = OnKey; CreateHandle(new CreateParams());
    }

    public void Configure(string crossing, string locking)
    {
        var cross = KeyChord.Parse(crossing); var force = KeyChord.Parse(locking);
        if (cross == force || force.Key == 0) throw new ArgumentException("两组快捷键必须不同；强制锁定需要普通按键。");
        Reset(); Warning = null;
        Crossing = crossing; Locking = locking;
        modifierOnly = cross.Key == 0;
        if (modifierOnly)
        {
            hook = Native.SetWindowsHookEx(13, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                modifierOnly = false; Crossing = "Ctrl+Alt+F9";
                Warning = "Ctrl+Win 键盘钩子不可用，已尝试 Ctrl+Alt+F9。";
                Register(1, KeyChord.Parse(Crossing));
            }
        }
        else Register(1, cross);
        if (!Native.RegisterHotKey(Handle, 2, force.Modifiers | 0x4000, force.Key))
        {
            Locking = "Ctrl+Alt+F10";
            Warning = (Warning ?? "") + "强制锁定快捷键注册失败，已尝试 Ctrl+Alt+F10。";
            if (KeyChord.Parse(Crossing) == KeyChord.Parse(Locking)) throw new ArgumentException("备用快捷键冲突，请修改配置。");
            Register(2, KeyChord.Parse(Locking));
        }
    }

    private void Register(int id, KeyChord chord)
    {
        if (!Native.RegisterHotKey(Handle, id, chord.Modifiers | 0x4000, chord.Key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "快捷键注册失败，请使用托盘设置修改。");
    }

    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data);
            int flags = Marshal.ReadInt32(data, 8);
            if ((flags & 0x10) == 0) // ignore injected events
            {
                bool isDown = message.ToInt64() is 0x100 or 0x104;
                bool isUp = message.ToInt64() is 0x101 or 0x105;
                bool isModifier = key is 0xA2 or 0xA3 or 0x11 or 0x5B or 0x5C;
                if (isDown)
                {
                    down.Add(key);
                    if (!isModifier) { used = true; armed = false; }
                    bool ctrl = down.Any(k => k is 0xA2 or 0xA3 or 0x11);
                    bool win = down.Any(k => k is 0x5B or 0x5C);
                    if (ctrl && win && !used && down.All(k => k is 0xA2 or 0xA3 or 0x11 or 0x5B or 0x5C)) armed = true;
                }
                if (isUp)
                {
                    if (isModifier && armed && !used)
                    {
                        armed = false; used = true;
                        // Post to the tray message pump: keep low-level hook fast.
                        NativePost.Post(Handle, 0x8001);
                    }
                    down.Remove(key);
                    if (down.Count == 0) armed = used = false;
                }
            }
        }
        // Observe without suppressing Windows key-up; swallowing it can leave
        // Windows/game key state inconsistent. See docs for Start menu caveat.
        return Native.CallNextHookEx(hook, code, message, data);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x8001 || (message.Msg == 0x312 && message.WParam.ToInt32() == 1)) toggleCross();
        if (message.Msg == 0x312 && message.WParam.ToInt32() == 2) toggleLock();
        base.WndProc(ref message);
    }
    private void Reset()
    {
        Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        down.Clear(); armed = used = false;
    }
    public void Dispose() { Reset(); DestroyHandle(); }
    private static class NativePost
    {
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);
        public static void Post(IntPtr hwnd, uint msg) => PostMessage(hwnd, msg, IntPtr.Zero, IntPtr.Zero);
    }
}
