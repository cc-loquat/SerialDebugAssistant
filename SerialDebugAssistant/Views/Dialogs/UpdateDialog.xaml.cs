using System.Windows;
using System.Windows.Input;

namespace SerialDebugAssistant.Views.Dialogs;

public partial class UpdateDialog : Window
{
    public bool ShouldUpdate { get; private set; }

    public UpdateDialog(string version, string releaseNotes)
    {
        InitializeComponent();
        VersionBlock.Text = $"版本 {version} 已发布";
        ReleaseNotesBlock.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "（无更新说明）"
            : releaseNotes;
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldUpdate = true;
        Close();
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldUpdate = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}