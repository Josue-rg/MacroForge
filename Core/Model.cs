using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MacroForge;

public enum ActionType
{
    KeyTap,
    KeyDown,
    KeyUp,
    Click,
    Move,
    Wait,
    MouseDown,
    MouseUp,
    MoveTo
}

public enum MouseButtonKind
{
    Left,
    Right,
    Middle
}

/// <summary>Opción mostrada en el desplegable de "Acción".</summary>
public sealed class ActionChoice
{
    public ActionType Value { get; init; }
    public string Label { get; init; } = string.Empty;

    public static IReadOnlyList<ActionChoice> All { get; } = new List<ActionChoice>
    {
        new() { Value = ActionType.KeyTap,  Label = "Pulsar tecla" },
        new() { Value = ActionType.KeyDown, Label = "Mantener" },
        new() { Value = ActionType.KeyUp,   Label = "Soltar" },
        new() { Value = ActionType.Click,   Label = "Clic" },
        new() { Value = ActionType.Move,    Label = "Mover ratón" },
        new() { Value = ActionType.Wait,    Label = "Esperar" },
    };
}

/// <summary>Opción mostrada en el desplegable de "Botón".</summary>
public sealed class ButtonChoice
{
    public MouseButtonKind Value { get; init; }
    public string Label { get; init; } = string.Empty;

    public static IReadOnlyList<ButtonChoice> All { get; } = new List<ButtonChoice>
    {
        new() { Value = MouseButtonKind.Left,   Label = "Izquierdo" },
        new() { Value = MouseButtonKind.Right,  Label = "Derecho" },
        new() { Value = MouseButtonKind.Middle, Label = "Central" },
    };
}

public sealed class MacroStep : INotifyPropertyChanged
{
    private ActionType _type = ActionType.KeyTap;
    private string _key = "F1";
    private MouseButtonKind _mouseButton = MouseButtonKind.Left;
    private int _x;
    private int _y;
    private int _delayMs = 50;

    public ActionType Type
    {
        get => _type;
        set
        {
            if (_type == value) return;
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsesKey));
        }
    }

    public string Key
    {
        get => _key;
        set
        {
            if (_key == value) return;
            _key = value;
            OnPropertyChanged();
        }
    }

    public MouseButtonKind MouseButton
    {
        get => _mouseButton;
        set
        {
            if (_mouseButton == value) return;
            _mouseButton = value;
            OnPropertyChanged();
        }
    }

    public int X
    {
        get => _x;
        set
        {
            if (_x == value) return;
            _x = value;
            OnPropertyChanged();
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            if (_y == value) return;
            _y = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Espera después de ejecutar este paso, en milisegundos.</summary>
    public int DelayMs
    {
        get => _delayMs;
        set
        {
            if (_delayMs == value) return;
            _delayMs = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool UsesKey => Type is ActionType.KeyTap or ActionType.KeyDown or ActionType.KeyUp;

    public MacroStep Clone() => new()
    {
        Type = Type,
        Key = Key,
        MouseButton = MouseButton,
        X = X,
        Y = Y,
        DelayMs = DelayMs
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MacroProfile
{
    public string Name { get; set; } = "Macro";
    public int RepeatCount { get; set; }
    public int LoopDelayMs { get; set; } = 100;

    /// <summary>Límite total en milisegundos. 0 = sin límite.</summary>
    public int DurationMs { get; set; }

    /// <summary>Atajo global que inicia el macro (vacío = sin configurar).</summary>
    public string ToggleKey { get; set; } = "";

    /// <summary>Atajo global para detener (F12 siempre funciona).</summary>
    public string StopKey { get; set; } = "";

    /// <summary>Atajo global para reproducir la grabación de Avanzado.</summary>
    public string PlayKey { get; set; } = "";

    public List<MacroStep> Steps { get; set; } = new();

    public MacroProfile Clone() => new()
    {
        Name = Name,
        RepeatCount = RepeatCount,
        LoopDelayMs = LoopDelayMs,
        DurationMs = DurationMs,
        ToggleKey = ToggleKey,
        StopKey = StopKey,
        PlayKey = PlayKey,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };
}
