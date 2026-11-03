using System.Diagnostics;

namespace MacroForge;

/// <summary>
/// Ejecuta un perfil en un hilo aparte, con esperas interrumpibles
/// (la tecla de pánico corta el macro en pocos milisegundos).
/// </summary>
public sealed class MacroEngine : IDisposable
{
    private readonly InputBackend _input = new();
    private Thread? _worker;
    private volatile bool _stop;
    private volatile bool _running;

    public bool IsRunning => _running;

    /// <summary>Mensajes para la barra de estado. Se emiten desde el hilo del motor.</summary>
    public event Action<string>? Status;

    public void Start(MacroProfile profile)
    {
        if (_running || (_worker?.IsAlive ?? false)) return;

        _stop = false;
        _running = true;

        MacroProfile snapshot = profile.Clone();
        _worker = new Thread(() => Run(snapshot))
        {
            IsBackground = true,
            Name = "MacroForge.Worker"
        };
        _worker.Start();

        Status?.Invoke("Ejecutando…");
    }

    public void Stop() => _stop = true;

    private void Run(MacroProfile profile)
    {
        string message;

        try
        {
            Stopwatch total = Stopwatch.StartNew();
            int loop = 0;

            while (!_stop && (profile.RepeatCount <= 0 || loop < profile.RepeatCount))
            {
                if (Expired(profile, total)) break;
                loop++;

                foreach (MacroStep step in profile.Steps)
                {
                    if (_stop || Expired(profile, total)) break;

                    Execute(step);

                    if (step.DelayMs > 0 && !Wait(step.DelayMs)) break;
                }

                if (_stop || Expired(profile, total)) break;
                if (profile.LoopDelayMs > 0 && !Wait(profile.LoopDelayMs)) break;
            }

            message = _stop ? "Detenido." : "Macro terminado.";
        }
        catch (Exception ex)
        {
            message = "Error: " + ex.Message;
        }
        finally
        {
            _input.ReleaseAll();
        }

        _running = false;
        Status?.Invoke(message);
    }

    private void Execute(MacroStep step)
    {
        switch (step.Type)
        {
            case ActionType.KeyTap:
                _input.KeyTap(KeyParser.ToVirtualKey(step.Key));
                break;

            case ActionType.KeyDown:
                _input.KeyDown(KeyParser.ToVirtualKey(step.Key));
                break;

            case ActionType.KeyUp:
                _input.KeyUp(KeyParser.ToVirtualKey(step.Key));
                break;

            case ActionType.Click:
                _input.Click(step.MouseButton);
                break;

            case ActionType.Move:
                _input.MoveRelative(step.X, step.Y);
                break;

            case ActionType.MoveTo:
                _input.MoveTo(step.X, step.Y);
                break;

            case ActionType.MouseDown:
                _input.MoveTo(step.X, step.Y);
                _input.MouseDown(step.MouseButton);
                break;

            case ActionType.MouseUp:
                _input.MoveTo(step.X, step.Y);
                _input.MouseUp(step.MouseButton);
                break;

            case ActionType.Wait:
                break;
        }
    }

    private static bool Expired(MacroProfile profile, Stopwatch total)
        => profile.DurationMs > 0 && total.ElapsedMilliseconds >= profile.DurationMs;

    /// <summary>Espera con precisión de ~1 ms y se corta si llega la orden de parar.</summary>
    private bool Wait(int milliseconds)
    {
        if (milliseconds <= 0) return !_stop;

        Stopwatch sw = Stopwatch.StartNew();

        while (true)
        {
            if (_stop) return false;

            long left = milliseconds - sw.ElapsedMilliseconds;
            if (left <= 0) return true;

            if (left > 3)
                Thread.Sleep((int)Math.Min(left - 2, 10));
            else
                Thread.SpinWait(200);
        }
    }

    public void Dispose()
    {
        Stop();

        try
        {
            _worker?.Join(1500);
        }
        catch
        {
            // ignorar
        }
    }
}
