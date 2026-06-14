using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MultiSych.Desktop.Views;

public partial class MessageDialog : Window
{
    public new string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public MessageDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e) => Close();
}