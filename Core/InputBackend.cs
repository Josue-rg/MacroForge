using System.Runtime.InteropServices;

namespace MacroForge;

/// <summary>
/// Inyecta teclado y ratón con SendInput usando SCAN CODES, que es lo que
/// leen los juegos con Raw Input / DirectInput.
/// </summary>
public sealed class InputBackend
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private readonly HashSet<int> _heldKeys = new();
    private readonly HashSet<MouseButtonKind> _heldButtons = new();

    public void KeyTap(int virtualKey)
    {
        KeyDown(virtualKey);
        KeyUp(virtualKey);
    }

    public void KeyDown(int virtualKey)
    {
        SendKey(virtualKey, down: true);
        _heldKeys.Add(virtualKey);
    }

    public void KeyUp(int virtualKey)
    {
        SendKey(virtualKey, down: false);
        _heldKeys.Remove(virtualKey);
    }

    /// <summary>Suelta todo lo que quedó presionado (pánico / fin de macro).</summary>
    public void ReleaseAll()
    {
        foreach (int vk in _heldKeys.ToArray())
            SendKey(vk, down: false);

        foreach (MouseButtonKind button in _heldButtons.ToArray())
            SendMouse(ButtonFlags(button, down: false), 0, 0);

        _heldKeys.Clear();
        _heldButtons.Clear();
    }

    public void Click(MouseButtonKind button)
    {
        MouseDown(button);
        MouseUp(button);
    }

    public void MouseDown(MouseButtonKind button)
    {
        SendMouse(ButtonFlags(button, down: true), 0, 0);
        _heldButtons.Add(button);
    }

    public void MouseUp(MouseButtonKind button)
    {
        SendMouse(ButtonFlags(button, down: false), 0, 0);
        _heldButtons.Remove(button);
    }

    public void MoveRelative(int dx, int dy) => SendMouse(MOUSEEVENTF_MOVE, dx, dy);

    /// <summary>Mueve el cursor a una posición absoluta de la pantalla.</summary>
    public void MoveTo(int x, int y)
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = Math.Max(GetSystemMetrics(SM_CXVIRTUALSCREEN) - 1, 1);
        int height = Math.Max(GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1, 1);

        int normalizedX = (int)Math.Round((x - left) * 65535.0 / width);
        int normalizedY = (int)Math.Round((y - top) * 65535.0 / height);

        normalizedX = Math.Clamp(normalizedX, 0, 65535);
        normalizedY = Math.Clamp(normalizedY, 0, 65535);

        SendMouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                  normalizedX, normalizedY);
    }

    private static uint ButtonFlags(MouseButtonKind button, bool down) => button switch
    {
        MouseButtonKind.Right => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
        MouseButtonKind.Middle => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
        _ => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP
    };

    private static void SendKey(int virtualKey, bool down)
    {
        ushort scan = (ushort)MapVirtualKey((uint)virtualKey, 0);
        uint flags = KEYEVENTF_SCANCODE;

        if (!down) flags |= KEYEVENTF_KEYUP;
        if (IsExtended(virtualKey)) flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouse(uint flags, int dx, int dy)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static bool IsExtended(int vk) => vk switch
    {
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 => true,
        0x2C or 0x2D or 0x2E or 0x6F => true,
        0x90 or 0xA3 or 0xA5 => true,
        0x5B or 0x5C => true,
        _ => false
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
