using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MacroForge;

public enum CapturePurpose
{
    Step,
    Start,
    Record,
    Play,
    Stop
}

public partial class CaptureKeyWindow : Window
{
    public CaptureKeyWindow() : this(CapturePurpose.Step)
    {
    }

    public CaptureKeyWindow(CapturePurpose purpose, string? action = null, string? title = null, string? step = null)
    {
        Purpose = purpose;
        AllowModifiers = purpose != CapturePurpose.Step;

        InitializeComponent();
        ApplyLook(action, title, step);
    }

    public CapturePurpose Purpose { get; }

    public bool AllowModifiers { get; }

    public string? CapturedKey { get; private set; }

    private void ApplyLook(string? action, string? title, string? step)
    {
        switch (Purpose)
        {
            case CapturePurpose.Start:
                Paint("#2563EB", step ?? "", action ?? "INICIAR", "Tecla o combinación que inicia");
                Title = title ?? "Tecla ó combinacion para iniciar";
                break;

            case CapturePurpose.Record:
                Paint("#2563EB", step ?? "", action ?? "GRABAR", "Tecla o combinación que graba");
                Title = title ?? "Tecla ó combinacion para grabar";
                break;

            case CapturePurpose.Play:
                Paint("#16A34A", step ?? "", action ?? "PLAY", "Tecla o combinación que reproduce");
                Title = title ?? "Tecla ó combinacion para reproducir";
                break;

            case CapturePurpose.Stop:
                Paint("#DC2626", step ?? "", action ?? "DETENER", "Tecla o combinación que detiene");
                Title = title ?? "Tecla ó combinacion para detener";
                break;

            default:
                Paint("#9CA3AF", step ?? "", action ?? "TECLA", "");
                Title = title ?? "Capturar tecla";
                break;
        }
    }

    private void Paint(string color, string step, string action, string hint)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();

        AccentBar.Background = brush;
        ActionText.Foreground = brush;
        StepText.Text = step;
        ActionText.Text = action;
        HintText.Text = hint;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Pulsar solo Ctrl/Alt/Shift no termina la captura: hay que pulsar la tecla final.
        if (IsModifier(key)) return;

        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            DialogResult = false;
            return;
        }

        CapturedKey = AllowModifiers && Keyboard.Modifiers != ModifierKeys.None
            ? HotkeyParser.Format(Keyboard.Modifiers, key)
            : key.ToString();

        DialogResult = true;
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;
}
