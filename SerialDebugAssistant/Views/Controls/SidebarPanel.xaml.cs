using System.Windows.Controls;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views.Controls;

public partial class SidebarPanel : UserControl
{
    public SidebarPanel() => InitializeComponent();

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && ThemeComboBox.SelectedItem is string theme && vm.SelectedTheme != theme)
            vm.SelectedTheme = theme;
    }
}
