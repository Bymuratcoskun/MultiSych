using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace MultiSych.Desktop.Views;

public partial class MessageDialog : Window
{
    public string DialogTitle { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public MessageDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void ShowMessage(string title, string message)
    {
        DialogTitle = title;
        Message = message;
        this.Title = title;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            _ = ShowDialog(desktop.MainWindow);
            return;
        }

        Show();
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e) => Close();
}