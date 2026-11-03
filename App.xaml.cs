using System.Windows;

namespace MacroForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Timing.Begin();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Timing.End();
        base.OnExit(e);
    }
}
