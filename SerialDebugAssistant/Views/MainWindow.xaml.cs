using System;
using System.Windows;
using System.Windows.Media.Imaging;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new SerialService());
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico"));
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
