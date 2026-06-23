using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace MultiSych.Desktop.ViewModels;

public class ErrorReportViewModel : ViewModelBase
{
    private string _issueTitle = string.Empty;
    private string _issueDescription = string.Empty;

    public ErrorReportViewModel()
    {
        SubmitGitHubIssueCommand = new RelayCommand(_ => SubmitToGitHub(), _ => !string.IsNullOrWhiteSpace(IssueTitle));
    }

    public string IssueTitle
    {
        get => _issueTitle;
        set { if (SetProperty(ref _issueTitle, value)) (SubmitGitHubIssueCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string IssueDescription
    {
        get => _issueDescription;
        set => SetProperty(ref _issueDescription, value);
    }

    public ICommand SubmitGitHubIssueCommand { get; }

    private void SubmitToGitHub()
    {
        // GitHub URL'sine başlık ve açıklamayı (issue gövdesini) query parametresi olarak ekliyoruz
        var body = $"**Açıklama / Description:**\n{IssueDescription}\n\n**Uygulama Bilgileri:**\nMultiSych Desktop v1.0\nOS: {RuntimeInformation.OSDescription}";
        var url = $"https://github.com/yourusername/MultiSych/issues/new?title={Uri.EscapeDataString(IssueTitle)}&body={Uri.EscapeDataString(body)}";
        
        try
        {
            Process.Start(url);
        }
        catch
        {
            // İşletim sistemine göre farklı tarayıcı açma senaryoları
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
        }
    }
}
