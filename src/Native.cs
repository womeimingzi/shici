using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Shici
{
    internal static class Native
    {
        internal const int WM_HOTKEY = 0x0312;
        internal const int WM_SHOWAPP = 0x8001;
        internal const int HotkeyId = 7311;
        internal const uint HotkeyModifiers = 0x4001; // Alt + MOD_NOREPEAT
        internal const uint HotkeyKey = 0x45; // E
        internal const string HotkeyLabel = "Alt + E";
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        // INPUT's union must include MOUSEINPUT's size, even when sending only keys.
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr handle, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr handle, int id);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr handle, out Rect rect);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] internal static extern bool CloseClipboard();
        [DllImport("user32.dll")] internal static extern bool EmptyClipboard();
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] internal static extern uint EnumClipboardFormats(uint format);
        [DllImport("user32.dll")] internal static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll")] internal static extern IntPtr SetClipboardData(uint format, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClipboardFormatName(uint format, StringBuilder name, int count);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern UIntPtr GlobalSize(IntPtr memory);
        [DllImport("kernel32.dll")] internal static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] internal static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] internal static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll")] internal static extern IntPtr GlobalFree(IntPtr memory);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr CopyImage(IntPtr handle, uint type, int width, int height, uint flags);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr handle);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CopyEnhMetaFile(IntPtr source, string file);
        [DllImport("gdi32.dll")] internal static extern bool DeleteEnhMetaFile(IntPtr handle);

        internal static uint ProcessId(IntPtr handle)
        {
            uint id; GetWindowThreadProcessId(handle, out id); return id;
        }
        internal static bool ModifiersDown()
        {
            foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((GetAsyncKeyState(key) & 0x8000) != 0) return true;
            return false;
        }
        internal static bool CaptureKeysDown()
        {
            return ModifiersDown() || (GetAsyncKeyState((int)HotkeyKey) & 0x8000) != 0
                || (GetAsyncKeyState(0x43) & 0x8000) != 0; // Do not overlap a physical Ctrl+C.
        }
        internal static void MaskAltMenu(IntPtr target)
        {
            if (target == IntPtr.Zero || GetForegroundWindow() != target
                || (GetAsyncKeyState(0x12) & 0x8000) == 0) return;
            // The registered E key can be hidden from the foreground app, leaving
            // a bare Alt release that focuses its menu. VK_FF means "no mapping";
            // it masks that release without inserting text or dismissing a selection.
            var down = new Input { Type = 1 };
            down.Data.Keyboard.Key = 0xFF;
            var up = down; up.Data.Keyboard.Flags = 2;
            var mask = new[] { down, up };
            if (SendInput(2, mask, Marshal.SizeOf(typeof(Input))) != 2)
                SendInput(1, new[] { up }, Marshal.SizeOf(typeof(Input)));
        }
        internal static bool SendCopy()
        {
            var inputs = new Input[4];
            ushort[] keys = { 0x11, 0x43, 0x43, 0x11 };
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].Type = 1;
                inputs[i].Data.Keyboard.Key = keys[i];
                inputs[i].Data.Keyboard.Flags = i >= 2 ? 2u : 0u;
            }
            uint sent = SendInput(4, inputs, Marshal.SizeOf(typeof(Input)));
            if (sent == 4) return true;
            // A partial injection must not leave Ctrl or C held down.
            var release = new[] { inputs[2], inputs[3] };
            SendInput(2, release, Marshal.SizeOf(typeof(Input)));
            return false;
        }
    }
}
