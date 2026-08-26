using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new SerialService());
        DataContext = _vm;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico"));

        _vm.PropertyChanged += OnVmPropertyChanged;
        Loaded += (_, _) => ApplyView();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedView))
            ApplyView();
    }

    private void ApplyView()
    {
        var isPid = _vm.SelectedView == AppView.Pid;
        SerialSidebar.Visibility = isPid ? Visibility.Collapsed : Visibility.Visible;
        SerialMainPanel.Visibility = isPid ? Visibility.Collapsed : Visibility.Visible;
        PidSidebarCtrl.Visibility = isPid ? Visibility.Visible : Visibility.Collapsed;
        PidMainPanelCtrl.Visibility = isPid ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
