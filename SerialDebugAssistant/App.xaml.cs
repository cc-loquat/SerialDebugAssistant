using System.Windows;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.Views;

namespace SerialDebugAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Apply(ThemeService.LoadTheme(), save: false);
        var window = new MainWindow();
        window.Show();
    }
}
