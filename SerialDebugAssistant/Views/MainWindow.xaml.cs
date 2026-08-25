using System.Windows;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new SerialService());
    }
}
