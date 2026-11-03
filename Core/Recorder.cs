using System.Runtime.InteropServices;
using System.Windows.Input;

namespace MacroForge;

/// <summary>
/// Graba teclado y ratón (incluido el movimiento del cursor) con sus tiempos.
/// Usa hooks de bajo nivel de Windows, así que funciona con la app en segundo plano.
/// </summary>
public sealed class Recorder : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;

    private const int MoveIntervalMs = 8;
    private const int MoveMinDistance = 4;
    private const int MaxDelayMs = 60_000;

    private readonly List<MacroStep> _steps = new();
    private readonly HashSet<int> _ignoredKeys = new();
    private readonly LowLevelProc _keyProc;
    private readonly LowLevelProc _mouseProc;

    private IntPtr _keyHook;
    private IntPtr _mouseHook;
    private long _lastTick;
    private long _lastMoveTick;
    private int _lastX;
    private int _lastY;
    private bool _recording;

    public Recorder()
    {
        _keyProc = KeyHook;
        _mouseProc = MouseHook;
    }

    public bool IsRecording => _recording;

    public IReadOnlyList<MacroStep> Steps => _steps;

    public long DurationMs { get; private set; }

    /// <summary>Teclas de los atajos que no deben grabarse (la de grabar/parar/play).</summary>
    public void Start(IEnumerable<int> ignoredKeys)
    {
        if (_recording) return;

        _steps.Clear();
        _ignoredKeys.Clear();
        foreach (int key in ignoredKeys)
            _ignoredKeys.Add(key);

        _recording = true;
        _lastTick = Environment.TickCount64;
        _lastMoveTick = 0;
        _lastX = int.MinValue;
        _lastY = int.MinValue;

        IntPtr module = GetModuleHandle(null);
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);

        if (_keyHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("No se pudieron instalar los hooks de grabación.");
        }
    }

    public void Stop()
    {
        if (!_recording) return;

        _recording = false;

        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);

        _keyHook = IntPtr.Zero;
        _mouseHook = IntPtr.Zero;

        DurationMs = _steps.Count == 0 ? 0 : _steps.Sum(s => (long)s.DelayMs);
        TrimTrailingModifiers();
    }

    /// <summary>
    /// Quita los clics que se hicieron sobre la propia ventana al pulsar Parar,
    /// para que no se repitan luego.
    /// </summary>
    public void TrimTrailingInside(int left, int top, int right, int bottom)
    {
        while (_steps.Count > 0)
        {
            MacroStep last = _steps[^1];

            bool mouse = last.Type is ActionType.MouseDown or ActionType.MouseUp or ActionType.MoveTo;
            bool inside = last.X >= left && last.X <= right && last.Y >= top && last.Y <= bottom;

            if (!mouse || !inside) break;

            _steps.RemoveAt(_steps.Count - 1);
        }

        DurationMs = _steps.Count == 0 ? 0 : _steps.Sum(s => (long)s.DelayMs);
    }

    private IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recording)
        {
            int message = wParam.ToInt32();
            KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool down = message is WM_KEYDOWN or WM_SYSKEYDOWN;

            if ((message is WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP)
                && !_ignoredKeys.Contains((int)data.vkCode)
                && (data.flags & 0x10) == 0) // ignora eventos inyectados
            {
                AddKeyStep(down, (int)data.vkCode);
            }
        }

        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recording)
        {
            int message = wParam.ToInt32();
            MSLLHOOKSTRUCT data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if ((data.flags & 0x01) == 0) // ignora eventos inyectados
            {
                switch (message)
                {
                    case WM_MOUSEMOVE:
                        AddMove(data.pt.x, data.pt.y);
                        break;

                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        AddButtonStep(message, data.pt.x, data.pt.y, down: true);
                        break;

                    case WM_LBUTTONUP:
                    case WM_RBUTTONUP:
                    case WM_MBUTTONUP:
                        AddButtonStep(message, data.pt.x, data.pt.y, down: false);
                        break;
                }
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void AddKeyStep(bool down, int virtualKey)
    {
        Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
        if (key == Key.None) return;

        Add(new MacroStep
        {
            Type = down ? ActionType.KeyDown : ActionType.KeyUp,
            Key = key.ToString()
        });
    }

    private void AddMove(int x, int y)
    {
        long now = Environment.TickCount64;

        if (now - _lastMoveTick < MoveIntervalMs) return;
        if (Math.Abs(x - _lastX) < MoveMinDistance && Math.Abs(y - _lastY) < MoveMinDistance) return;

        _lastMoveTick = now;
        _lastX = x;
        _lastY = y;

        Add(new MacroStep { Type = ActionType.MoveTo, X = x, Y = y });
    }

    private void AddButtonStep(int message, int x, int y, bool down)
    {
        MouseButtonKind button = message switch
        {
            WM_RBUTTONDOWN or WM_RBUTTONUP => MouseButtonKind.Right,
            WM_MBUTTONDOWN or WM_MBUTTONUP => MouseButtonKind.Middle,
            _ => MouseButtonKind.Left
        };

        _lastX = x;
        _lastY = y;

        Add(new MacroStep
        {
            Type = down ? ActionType.MouseDown : ActionType.MouseUp,
            MouseButton = button,
            X = x,
            Y = y
        });
    }

    private void Add(MacroStep step)
    {
        long now = Environment.TickCount64;
        long elapsed = now - _lastTick;

        step.DelayMs = (int)Math.Clamp(elapsed, 0, MaxDelayMs);
        _lastTick = now;
        _steps.Add(step);
    }

    /// <summary>Quita un Ctrl/Shift/Alt suelto que haya quedado al pulsar el atajo de parar.</summary>
    private void TrimTrailingModifiers()
    {
        while (_steps.Count > 0)
        {
            MacroStep last = _steps[^1];

            if (last.Type == ActionType.KeyDown && IsModifier(last.Key))
            {
                _steps.RemoveAt(_steps.Count - 1);
                continue;
            }

            break;
        }
    }

    private static bool IsModifier(string key) => key is "LeftCtrl" or "RightCtrl"
        or "LeftShift" or "RightShift" or "LeftAlt" or "RightAlt"
        or "LWin" or "RWin";

    public void Dispose() => Stop();

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
