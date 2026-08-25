using System.Windows;
using SerialDebugAssistant.Views;

namespace SerialDebugAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
    }
}
