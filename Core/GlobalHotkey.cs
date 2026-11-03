using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MacroForge;

/// <summary>
/// Atajos globales (funcionan aunque la app no tenga el foco).
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private readonly Dictionary<string, Binding> _slots = new();
    private int _nextId = 0xA000;

    private readonly record struct Binding(int Id, ModifierKeys Modifiers, Key Key);

    public GlobalHotkey(Window window)
    {
        WindowInteropHelper helper = new(window);
        _source = HwndSource.FromHwnd(helper.Handle)
                  ?? throw new InvalidOperationException("La ventana todavía no tiene handle.");
        _source.AddHook(Hook);
    }

    /// <summary>
    /// Registra o reemplaza un atajo. Devuelve false si otra aplicación ya lo tiene;
    /// en ese caso el atajo anterior de ese hueco sigue funcionando.
    /// </summary>
    public bool Bind(string slot, ModifierKeys modifiers, Key key, Action action)
    {
        bool hadPrevious = _slots.TryGetValue(slot, out Binding previous);

        if (hadPrevious && previous.Modifiers == modifiers && previous.Key == key)
        {
            _actions[previous.Id] = action;
            return true;
        }

        int id = _nextId++;

        if (!RegisterHotKey(_source.Handle, id, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)))
            return false;

        if (hadPrevious)
        {
            UnregisterHotKey(_source.Handle, previous.Id);
            _actions.Remove(previous.Id);
            _slots.Remove(slot);
        }

        _slots[slot] = new Binding(id, modifiers, key);
        _actions[id] = action;
        return true;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out Action? action))
        {
            action();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (int id in _actions.Keys)
            UnregisterHotKey(_source.Handle, id);

        _actions.Clear();
        _slots.Clear();
        _source.RemoveHook(Hook);
    }

    /// <summary>Libera un atajo sin tocar los demás.</summary>
    public void Unbind(string slot)
    {
        if (!_slots.TryGetValue(slot, out Binding binding)) return;

        UnregisterHotKey(_source.Handle, binding.Id);
        _actions.Remove(binding.Id);
        _slots.Remove(slot);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
