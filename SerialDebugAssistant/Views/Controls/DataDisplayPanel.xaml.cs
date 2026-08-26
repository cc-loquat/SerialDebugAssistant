using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SerialDebugAssistant.Views.Controls;

public partial class DataDisplayPanel : UserControl
{
    public DataDisplayPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewModels.MainViewModel oldVm)
        {
            oldVm.DataLineReceived -= AppendColoredLine;
            oldVm.ClearReceivedRequested -= ClearRecvBox;
        }
        if (e.NewValue is ViewModels.MainViewModel vm)
        {
            vm.DataLineReceived += AppendColoredLine;
            vm.ClearReceivedRequested += ClearRecvBox;
        }
    }

    private void AppendColoredLine(string text, string direction, string timestamp)
    {
        Dispatcher.Invoke(() =>
        {
            var brush = direction == "TX"
                ? (Brush)Application.Current.Resources["TxTextBrush"]
                : (Brush)Application.Current.Resources["RxTextBrush"];

            var para = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = 1
            };

            para.Inlines.Add(new Run($"[{timestamp}] [{direction}] {text}") { Foreground = brush });

            RecvBox.Document.Blocks.Add(para);
            RecvBox.ScrollToEnd();
        });
    }

    private void ClearRecvBox()
    {
        Dispatcher.Invoke(() => RecvBox.Document.Blocks.Clear());
    }

    private void SendBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            int caret = SendBox.CaretIndex;
            SendBox.Text = SendBox.Text.Insert(caret, "\n");
            SendBox.CaretIndex = caret + 1;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (DataContext is ViewModels.MainViewModel vm)
                vm.SendCommand.Execute(null);
        }
    }
}