using System.Windows.Controls;
using System.Windows;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views.Controls;

public partial class SidebarPanel : UserControl
{
    public SidebarPanel() => InitializeComponent();

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not string theme) return;
        var vm = DataContext as MainViewModel ?? Window.GetWindow(this)?.DataContext as MainViewModel;
        if (vm is not null && vm.SelectedTheme != theme) vm.SelectedTheme = theme;
    }
}
