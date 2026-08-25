using System.Windows.Controls;
using System.Windows.Input;

namespace SerialDebugAssistant.Views.Controls;

public partial class DataDisplayPanel : UserControl
{
    public DataDisplayPanel() => InitializeComponent();

    private void RecvBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecvBox.ScrollToEnd();
    }

    private void SendBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            // Alt+Enter 插入换行
            int caret = SendBox.CaretIndex;
            SendBox.Text = SendBox.Text.Insert(caret, "\n");
            SendBox.CaretIndex = caret + 1;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            // 回车发送
            e.Handled = true;
            if (DataContext is SerialDebugAssistant.ViewModels.MainViewModel vm)
                vm.SendCommand.Execute(null);
        }
    }
}
