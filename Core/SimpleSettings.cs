using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace MacroForge;

/// <summary>Ajustes de las pestañas simples (Autoclic y Auto-tecla).</summary>
public sealed class SimpleSettings : INotifyPropertyChanged
{
    private MouseButtonKind _mouseButton = MouseButtonKind.Left;
    private string _key = "F1";
    private string _intervalText = "100";
    private int _intervalUnitIndex;
    private bool _infinite;
    private string _repeatText = "10";
    private bool _stopByDuration;
    private string _durationText = "30";
    private int _durationUnitIndex;

    public SimpleSettings(bool isClick)
    {
        IsClick = isClick;
        _infinite = isClick;
    }

    public bool IsClick { get; }

    public bool ShowMouseButton => IsClick;

    public bool ShowKey => !IsClick;

    public MouseButtonKind MouseButton
    {
        get => _mouseButton;
        set { if (_mouseButton == value) return; _mouseButton = value; OnPropertyChanged(); }
    }

    public string Key
    {
        get => _key;
        set { if (_key == value) return; _key = value; OnPropertyChanged(); }
    }

    public string IntervalText
    {
        get => _intervalText;
        set
        {
            if (_intervalText == value) return;
            _intervalText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IntervalMs));
        }
    }

    /// <summary>0 = milisegundos, 1 = segundos.</summary>
    public int IntervalUnitIndex
    {
        get => _intervalUnitIndex;
        set
        {
            if (_intervalUnitIndex == value) return;
            _intervalUnitIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IntervalMs));
        }
    }

    public int IntervalMs
    {
        get
        {
            double value = ParseNumber(IntervalText, 100);
            double ms = IntervalUnitIndex == 1 ? value * 1000 : value;
            return Math.Max(1, (int)Math.Round(ms));
        }
    }

    public bool Infinite
    {
        get => _infinite;
        set
        {
            if (_infinite == value) return;
            _infinite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Finite));
        }
    }

    public bool Finite => !_infinite;

    public string RepeatText
    {
        get => _repeatText;
        set { if (_repeatText == value) return; _repeatText = value; OnPropertyChanged(); }
    }

    public int RepeatCount => Math.Max(1, ParseInt(RepeatText, 10));

    public bool StopByDuration
    {
        get => _stopByDuration;
        set { if (_stopByDuration == value) return; _stopByDuration = value; OnPropertyChanged(); }
    }

    public string DurationText
    {
        get => _durationText;
        set { if (_durationText == value) return; _durationText = value; OnPropertyChanged(); }
    }

    /// <summary>0 = segundos, 1 = minutos.</summary>
    public int DurationUnitIndex
    {
        get => _durationUnitIndex;
        set { if (_durationUnitIndex == value) return; _durationUnitIndex = value; OnPropertyChanged(); }
    }

    public int DurationMs
    {
        get
        {
            double value = ParseNumber(DurationText, 30);
            double ms = DurationUnitIndex == 1 ? value * 60_000 : value * 1000;
            return Math.Max(1, (int)Math.Round(ms));
        }
    }

    private static double ParseNumber(string? text, double fallback)
    {
        string clean = (text ?? string.Empty).Trim().Replace(',', '.');

        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0
            ? value
            : fallback;
    }

    private static int ParseInt(string? text, int fallback)
        => int.TryParse((text ?? string.Empty).Trim(), out int value) && value > 0 ? value : fallback;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
