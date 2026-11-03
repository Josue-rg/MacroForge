using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace MacroForge;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly string LastProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacroForge", "last.json");

    private readonly MacroEngine _engine = new();
    private readonly Recorder _recorder = new();
    private GlobalHotkey? _hotkeys;

    private string _repeatText = "0";
    private string _loopDelayText = "100";
    private string _status = "";
    private string _toggleKey = "";
    private string _stopKey = "";
    private string _playKey = "";
    private bool _recording;
    private bool _running;

    public ObservableCollection<MacroStep> Steps { get; } = new();

    public SimpleSettings ClickSettings { get; } = new(isClick: true);

    public SimpleSettings KeySettings { get; } = new(isClick: false);

    public string RepeatText
    {
        get => _repeatText;
        set { if (_repeatText == value) return; _repeatText = value; OnPropertyChanged(); }
    }

    public string LoopDelayText
    {
        get => _loopDelayText;
        set { if (_loopDelayText == value) return; _loopDelayText = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { if (_status == value) return; _status = value; OnPropertyChanged(); }
    }

    /// <summary>Atajo global que inicia el macro o graba en Avanzado.</summary>
    public string ToggleKey
    {
        get => _toggleKey;
        set
        {
            if (_toggleKey == value) return;
            _toggleKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartLabel));
            OnPropertyChanged(nameof(HasKeys));
            OnPropertyChanged(nameof(CanStart));
        }
    }

    /// <summary>Atajo global que detiene (o para la grabación).</summary>
    public string StopKey
    {
        get => _stopKey;
        set
        {
            if (_stopKey == value) return;
            _stopKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StopLabel));
            OnPropertyChanged(nameof(PlayStopLabel));
            OnPropertyChanged(nameof(HasKeys));
            OnPropertyChanged(nameof(CanStart));
        }
    }

    /// <summary>Atajo global que reproduce la grabación de Avanzado.</summary>
    public string PlayKey
    {
        get => _playKey;
        set
        {
            if (_playKey == value) return;
            _playKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayLabel));
        }
    }

    public bool HasKeys => _toggleKey.Length > 0 && _stopKey.Length > 0;

    public bool IsAdvanced => Tabs.SelectedIndex == 2;

    public bool CanStart => !_running && !_recording && HasKeys;

    public bool CanStop => _recording || _running;

    public bool CanConfigure => !_running && !_recording;

    public bool CanPlay => IsAdvanced && !_recording && !_running && Steps.Count > 0;

    public bool CanPlayStop => IsAdvanced && _running;

    public string StartLabel
    {
        get
        {
            string verb = IsAdvanced ? "Grabar" : "Iniciar";
            return _toggleKey.Length > 0 ? $"{verb} · {_toggleKey}" : verb;
        }
    }

    public string StopLabel
    {
        get
        {
            string verb = IsAdvanced ? "Parar" : "Detener";
            return _stopKey.Length > 0 ? $"{verb} · {_stopKey}" : verb;
        }
    }

    public string PlayLabel => _playKey.Length > 0 ? "Play · " + _playKey : "Play";

    public string PlayStopLabel => _stopKey.Length > 0 ? "Stop · " + _stopKey : "Stop";

    public string RecordInfo
    {
        get
        {
            if (_recording) return "Grabando…";

            if (Steps.Count == 0) return "Sin grabación";

            long ms = Steps.Sum(s => (long)s.DelayMs);
            return $"{Steps.Count} eventos · {ms / 1000.0:0.0} s";
        }
    }

    public bool Recording
    {
        get => _recording;
        set
        {
            if (_recording == value) return;
            _recording = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanConfigure));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(RecordInfo));
        }
    }

    public bool Running
    {
        get => _running;
        set
        {
            if (_running == value) return;
            _running = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotRunning));
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanConfigure));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(CanPlayStop));
        }
    }

    public bool NotRunning => !_running;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _engine.Status += OnEngineStatus;

        Steps.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RecordInfo));
            OnPropertyChanged(nameof(CanPlay));
        };

        Tabs.SelectionChanged += OnTabChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StartLabel));
        OnPropertyChanged(nameof(StopLabel));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanConfigure));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanPlayStop));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadLastProfile();

        if (!HasKeys)
            Status = "Pulsa Configurar para elegir las teclas de Iniciar y Detener.";
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _recorder.Stop();
        _recorder.Dispose();
        _engine.Dispose();
        _hotkeys?.Dispose();
        SaveLastProfile();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            _hotkeys = new GlobalHotkey(this);
            _hotkeys.Bind("panic", ModifierKeys.None, Key.F12, StopMacro);

            if (_toggleKey.Length > 0 && !ApplyToggleKey(_toggleKey, report: false))
                Status = $"No se pudo usar {_toggleKey}: puede estar ocupado por otra app.";

            if (_stopKey.Length > 0)
                ApplyStopKey(_stopKey, report: false);

            if (_playKey.Length > 0)
                ApplyPlayKey(_playKey, report: false);
        }
        catch (Exception ex)
        {
            Status = "Aviso: no se pudieron registrar los atajos (" + ex.Message + ")";
        }
    }

    private void OnEngineStatus(string message) => Dispatcher.BeginInvoke(new Action(() =>
    {
        Status = message;
        Running = _engine.IsRunning;
    }));

    private void Toggle()
    {
        if (_recording)
        {
            StopRecording();
            return;
        }

        if (_engine.IsRunning)
        {
            _engine.Stop();
            return;
        }

        if (IsAdvanced)
            StartRecording();
        else
            StartMacro();
    }

    /// <summary>Registra el atajo global que inicia y detiene el macro.</summary>
    private bool ApplyToggleKey(string text, bool report)
    {
        if (_hotkeys is null) return false;

        if (!HotkeyParser.TryParse(text, out ModifierKeys modifiers, out Key key))
        {
            if (report) Status = "Atajo no válido: " + text;
            return false;
        }

        if (key == Key.F12 && modifiers == ModifierKeys.None)
        {
            if (report) Status = "F12 está reservado para detener el macro.";
            return false;
        }

        if (!_hotkeys.Bind("start", modifiers, key, Toggle))
        {
            // Conserva la tecla elegida aunque otra app la tenga ocupada ahora.
            ToggleKey = text;
            if (report) Status = $"No se pudo usar {text}: otra app puede tenerlo ocupado.";
            return false;
        }

        ToggleKey = text;
        if (report) Status = "Atajo para iniciar y detener: " + text;
        return true;
    }

    /// <summary>Libera los atajos mientras se captura una tecla nueva.</summary>
    public void SuspendHotkeys()
    {
        _hotkeys?.Unbind("start");
        _hotkeys?.Unbind("stop");
        _hotkeys?.Unbind("play");
        _hotkeys?.Unbind("panic");
    }

    /// <summary>Vuelve a registrar los atajos actuales.</summary>
    public void ResumeHotkeys()
    {
        _hotkeys?.Bind("panic", ModifierKeys.None, Key.F12, StopMacro);

        if (_toggleKey.Length > 0) ApplyToggleKey(_toggleKey, report: false);
        if (_stopKey.Length > 0) ApplyStopKey(_stopKey, report: false);
        if (_playKey.Length > 0) ApplyPlayKey(_playKey, report: false);
    }

    private void Configure_Click(object sender, RoutedEventArgs e)
    {
        if (IsAdvanced)
        {
            ConfigureAdvanced();
            return;
        }

        ConfigureSimple();
    }

    private void ConfigureSimple()
    {
        string? startKey = CaptureKey(CapturePurpose.Start);

        if (startKey is null)
        {
            Status = "Sin ejecucion.";
            return;
        }

        if (!ApplyToggleKey(startKey, report: true)) return;

        string? stopKey = CaptureKey(CapturePurpose.Stop);

        if (stopKey is null)
        {
            Status = "Guardo correctamente.";
            return;
        }

        if (!ApplyStopKey(stopKey, report: true)) return;

        Status = $"{ToggleKey} inicia y {StopKey} detiene.";
    }

    private void ConfigureAdvanced()
    {
        string? recordKey = CaptureKey(CapturePurpose.Record, "GRABAR", "Tecla para grabar", "PASO 1 DE 3");

        if (recordKey is null)
        {
            Status = "Configuración cancelada.";
            return;
        }

        if (!ApplyToggleKey(recordKey, report: true)) return;

        string? playKey = CaptureKey(CapturePurpose.Play, "PLAY", "Tecla para reproducir", "PASO 2 DE 3");

        if (playKey is null)
        {
            Status = "Se guardó la tecla de grabar.";
            return;
        }

        if (!ApplyPlayKey(playKey, report: true)) return;

        string? stopKey = CaptureKey(CapturePurpose.Stop, "PARAR", "Tecla para parar", "PASO 3 DE 3");

        if (stopKey is null)
        {
            Status = "Se guardaron grabar y reproducir.";
            return;
        }

        if (!ApplyStopKey(stopKey, report: true)) return;

        Status = $"{ToggleKey} graba, {PlayKey} reproduce y {StopKey} para.";
    }

    /// <summary>Abre el diálogo de captura sin que los atajos globales interfieran.</summary>
    private string? CaptureKey(CapturePurpose purpose, string? action = null, string? title = null, string? step = null)
    {
        string? captured;

        SuspendHotkeys();

        try
        {
            CaptureKeyWindow dialog = new(purpose, action, title, step) { Owner = this };
            captured = dialog.ShowDialog() == true ? dialog.CapturedKey : null;
        }
        finally
        {
            ResumeHotkeys();
        }

        return captured;
    }

    /// <summary>Registra el atajo de parada (además de F12, que nunca se toca).</summary>
    private bool ApplyStopKey(string text, bool report)
    {
        if (_hotkeys is null) return false;

        if (!HotkeyParser.TryParse(text, out ModifierKeys modifiers, out Key key))
        {
            if (report) Status = "Atajo no válido: " + text;
            return false;
        }

        // F12 siempre está activo como pánico; no hace falta registrarlo otra vez.
        if (key == Key.F12 && modifiers == ModifierKeys.None)
        {
            StopKey = text;
            if (report) Status = "F12 detiene siempre.";
            return true;
        }

        if (!_hotkeys.Bind("stop", modifiers, key, StopMacro))
        {
            StopKey = text;
            if (report) Status = $"No se pudo usar {text}: otra app puede tenerlo ocupado.";
            return false;
        }

        StopKey = text;
        if (report) Status = "Atajo para parar: " + text;
        return true;
    }

    /// <summary>Registra el atajo que reproduce la grabación.</summary>
    private bool ApplyPlayKey(string text, bool report)
    {
        if (_hotkeys is null) return false;

        if (!HotkeyParser.TryParse(text, out ModifierKeys modifiers, out Key key))
        {
            if (report) Status = "Atajo no válido: " + text;
            return false;
        }

        if (key == Key.F12 && modifiers == ModifierKeys.None)
        {
            if (report) Status = "F12 está reservado para parar.";
            return false;
        }

        if (!_hotkeys.Bind("play", modifiers, key, PlayRecording))
        {
            PlayKey = text;
            if (report) Status = $"No se pudo usar {text}: otra app puede tenerlo ocupado.";
            return false;
        }

        PlayKey = text;
        if (report) Status = "Atajo para reproducir: " + text;
        return true;
    }

    private void StartMacro()
    {
        if (IsAdvanced)
        {
            StartRecording();
            return;
        }

        if (_engine.IsRunning) return;

        if (!HasKeys)
        {
            Status = "Configura primero las teclas de Iniciar y Detener.";
            return;
        }

        MacroProfile profile;

        switch (Tabs.SelectedIndex)
        {
            case 0:
                profile = BuildSimpleProfile(ClickSettings);
                break;

            case 1:
                profile = BuildSimpleProfile(KeySettings);
                break;

            default:
                profile = BuildAdvancedProfile();
                break;
        }

        if (profile.Steps.Count == 0)
        {
            Status = "Añade al menos un paso.";
            return;
        }

        foreach (MacroStep step in profile.Steps)
        {
            if (!step.UsesKey) continue;

            try
            {
                KeyParser.ToVirtualKey(step.Key);
            }
            catch (Exception ex)
            {
                Status = ex.Message;
                return;
            }
        }

        _engine.Start(profile);
        Running = true;
    }

    private static MacroProfile BuildSimpleProfile(SimpleSettings settings) => new()
    {
        RepeatCount = settings.Infinite ? 0 : settings.RepeatCount,
        LoopDelayMs = 0,
        DurationMs = settings.StopByDuration ? settings.DurationMs : 0,
        Steps = new List<MacroStep>
        {
            new()
            {
                Type = settings.IsClick ? ActionType.Click : ActionType.KeyTap,
                MouseButton = settings.MouseButton,
                Key = settings.Key,
                DelayMs = settings.IntervalMs
            }
        }
    };

    private MacroProfile BuildAdvancedProfile() => new()
    {
        RepeatCount = int.TryParse(RepeatText, out int repeat) && repeat > 0 ? repeat : 0,
        LoopDelayMs = int.TryParse(LoopDelayText, out int loop) && loop >= 0 ? loop : 0,
        ToggleKey = ToggleKey,
        StopKey = StopKey,
        PlayKey = PlayKey,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };

    /// <summary>Empieza a grabar teclas, clics y movimiento del ratón.</summary>
    private void StartRecording()
    {
        if (_recording || _engine.IsRunning) return;

        if (!HasKeys)
        {
            Status = "Configura primero las teclas de Grabar y Parar.";
            return;
        }

        List<int> ignored = new();
        AddVirtualKey(ignored, _toggleKey);
        AddVirtualKey(ignored, _stopKey);
        AddVirtualKey(ignored, _playKey);
        ignored.Add(0x7B); // F12

        try
        {
            _recorder.Start(ignored);
        }
        catch (Exception ex)
        {
            Status = "No se pudo grabar: " + ex.Message;
            return;
        }

        Recording = true;
        Status = _stopKey.Length > 0 ? $"Grabando… pulsa {_stopKey} para parar." : "Grabando…";
    }

    private static void AddVirtualKey(List<int> keys, string text)
    {
        if (HotkeyParser.TryParse(text, out _, out Key key))
            keys.Add(KeyInterop.VirtualKeyFromKey(key));
    }

    private void StopRecording()
    {
        if (!_recording) return;

        _recorder.Stop();
        Recording = false;

        try
        {
            Point a = PointToScreen(new Point(0, 0));
            Point b = PointToScreen(new Point(ActualWidth, ActualHeight));
            _recorder.TrimTrailingInside((int)a.X, (int)a.Y, (int)b.X, (int)b.Y);
        }
        catch
        {
            // si la ventana ya no está, se deja la grabación tal cual
        }

        int recorded = _recorder.Steps.Count;

        if (recorded > 0)
        {
            Steps.Clear();

            foreach (MacroStep step in _recorder.Steps)
                Steps.Add(step);
        }

        Status = recorded == 0 ? "No se grabó nada." : $"Grabado: {recorded} eventos.";
    }

    /// <summary>Reproduce la grabación las veces indicadas.</summary>
    private void PlayRecording()
    {
        if (_recording)
        {
            Status = "Para la grabación antes de reproducir.";
            return;
        }

        if (_engine.IsRunning) return;

        if (Steps.Count == 0)
        {
            Status = "Graba algo primero.";
            return;
        }

        MacroProfile profile = new()
        {
            RepeatCount = int.TryParse(RepeatText, out int repeat) ? Math.Max(0, repeat) : 1,
            LoopDelayMs = 0,
            Steps = Steps.Select(s => s.Clone()).ToList()
        };

        _engine.Start(profile);
        Running = true;
        Status = "Reproduciendo…";
    }

    private void Play_Click(object sender, RoutedEventArgs e) => PlayRecording();

    private void PlayStop_Click(object sender, RoutedEventArgs e) => StopMacro();

    private void StopMacro()
    {
        if (_recording)
        {
            StopRecording();
            return;
        }

        _engine.Stop();
    }


    private void Start_Click(object sender, RoutedEventArgs e) => StartMacro();

    private void Stop_Click(object sender, RoutedEventArgs e) => StopMacro();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Abrir macro",
            Filter = "Macro (*.json)|*.json|Todos los archivos (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            MacroProfile? profile = ProfileStore.Load(dialog.FileName);

            if (profile is null)
            {
                Status = "El archivo está vacío o no es válido.";
                return;
            }

            ApplyProfile(profile);
            Status = "Cargado: " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            Status = "No se pudo abrir: " + ex.Message;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Guardar macro",
            Filter = "Macro (*.json)|*.json|Todos los archivos (*.*)|*.*",
            FileName = "mi-macro.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ProfileStore.Save(dialog.FileName, BuildAdvancedProfile());
            Status = "Guardado: " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            Status = "No se pudo guardar: " + ex.Message;
        }
    }

    private void ApplyProfile(MacroProfile profile)
    {
        Steps.Clear();

        foreach (MacroStep step in profile.Steps)
            Steps.Add(step);

        RepeatText = profile.RepeatCount.ToString();
        LoopDelayText = profile.LoopDelayMs.ToString();

        if (!string.IsNullOrWhiteSpace(profile.ToggleKey) && profile.ToggleKey != ToggleKey)
            ApplyToggleKey(profile.ToggleKey, report: false);

        if (!string.IsNullOrWhiteSpace(profile.StopKey) && profile.StopKey != StopKey)
            ApplyStopKey(profile.StopKey, report: false);

        if (!string.IsNullOrWhiteSpace(profile.PlayKey) && profile.PlayKey != PlayKey)
            ApplyPlayKey(profile.PlayKey, report: false);
    }

    private void LoadLastProfile()
    {
        try
        {
            if (!File.Exists(LastProfilePath)) return;

            MacroProfile? profile = ProfileStore.Load(LastProfilePath);
            if (profile is null) return;

            ApplyProfile(profile);
        }
        catch
        {
            // archivo corrupto: se empieza en blanco
        }
    }

    private void SaveLastProfile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastProfilePath)!);
            ProfileStore.Save(LastProfilePath, BuildAdvancedProfile());
        }
        catch
        {
            // no bloquear el cierre
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
