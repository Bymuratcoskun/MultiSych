namespace MultiSych.Desktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private string _summaryText = "Track connected accounts, storage mounts, and sync health from a unified dashboard.";

    public string Title => "Dashboard";
    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }
}
