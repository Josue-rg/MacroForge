using System.Runtime.InteropServices;

namespace MacroForge;

/// <summary>
/// Sube la resolución del temporizador del sistema a 1 ms mientras la app está abierta.
/// Sin esto, Thread.Sleep redondea a ~15.6 ms y los macros pierden precisión.
/// </summary>
public static class Timing
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    public static void Begin() => TimeBeginPeriod(1);

    public static void End() => TimeEndPeriod(1);
}
