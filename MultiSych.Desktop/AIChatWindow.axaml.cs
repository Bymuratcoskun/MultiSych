using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop;

public partial class AIChatWindow : Window
{
    public AIChatWindow()
    {
        InitializeComponent();
    }

    public AIChatWindow(string provider, IServiceProvider services)
    {
        InitializeComponent();
        DataContext = new AIChatViewModel(provider, services);
        Title = $"{provider?.ToUpperInvariant()} Chat";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
