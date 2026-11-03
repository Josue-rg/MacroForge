using System.Windows.Input;

namespace MacroForge;

/// <summary>
/// Convierte el nombre de una tecla ("A", "F1", "Space", "Ctrl"...) en un código virtual de Windows.
/// </summary>
public static class KeyParser
{
    private static readonly Dictionary<string, Key> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = Key.LeftCtrl,
        ["control"] = Key.LeftCtrl,
        ["shift"] = Key.LeftShift,
        ["alt"] = Key.LeftAlt,
        ["esc"] = Key.Escape,
        ["enter"] = Key.Return,
        ["intro"] = Key.Return,
        ["win"] = Key.LWin,
        ["windows"] = Key.LWin,
        ["space"] = Key.Space,
        ["espacio"] = Key.Space,
        ["tab"] = Key.Tab,
        ["del"] = Key.Delete,
        ["supr"] = Key.Delete,
        ["backspace"] = Key.Back,
        ["flecha arriba"] = Key.Up,
        ["flecha abajo"] = Key.Down,
        ["flecha izquierda"] = Key.Left,
        ["flecha derecha"] = Key.Right,
    };

    public static int ToVirtualKey(string? text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new ArgumentException("Falta la tecla.");

        if (Aliases.TryGetValue(text, out var alias))
            return KeyInterop.VirtualKeyFromKey(alias);

        if (text.Length == 1)
        {
            char c = char.ToUpperInvariant(text[0]);
            if (c is >= 'A' and <= 'Z') return c;
            if (c is >= '0' and <= '9') return c;
        }

        if (Enum.TryParse<Key>(text, true, out var key) && key != Key.None)
            return KeyInterop.VirtualKeyFromKey(key);

        throw new ArgumentException($"Tecla no reconocida: \"{text}\"");
    }
}
