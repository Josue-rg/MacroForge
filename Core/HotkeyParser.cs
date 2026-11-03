using System.Windows.Input;

namespace MacroForge;

/// <summary>
/// Lee atajos escritos como "F9", "Ctrl+F9" o "Ctrl+Shift+K".
/// </summary>
public static class HotkeyParser
{
    public static bool TryParse(string? text, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control;
                    break;

                case "shift":
                    modifiers |= ModifierKeys.Shift;
                    break;

                case "alt":
                    modifiers |= ModifierKeys.Alt;
                    break;

                case "win":
                case "windows":
                    modifiers |= ModifierKeys.Windows;
                    break;

                default:
                    return false;
            }
        }

        try
        {
            int virtualKey = KeyParser.ToVirtualKey(parts[^1]);
            key = KeyInterop.KeyFromVirtualKey(virtualKey);
        }
        catch
        {
            return false;
        }

        return key != Key.None;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        List<string> parts = new(4);

        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
