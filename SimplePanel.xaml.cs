using System.Windows;
using System.Windows.Controls;

namespace MacroForge;

public partial class SimplePanel : UserControl
{
    public SimplePanel()
    {
        InitializeComponent();
    }

    private SimpleSettings? Settings => DataContext as SimpleSettings;

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (Settings is null) return;

        MainWindow? window = Window.GetWindow(this) as MainWindow;
        string? captured;

        window?.SuspendHotkeys();

        try
        {
            CaptureKeyWindow dialog = new() { Owner = Window.GetWindow(this) };

            captured = dialog.ShowDialog() == true ? dialog.CapturedKey : null;
        }
        finally
        {
            window?.ResumeHotkeys();
        }

        if (captured is not null)
            Settings.Key = captured;
    }
}
