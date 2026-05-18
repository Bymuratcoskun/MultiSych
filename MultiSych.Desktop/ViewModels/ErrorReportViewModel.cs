using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MultiSych.Services.Configuration;

namespace MultiSych.Desktop.ViewModels;

public sealed class ErrorReportViewModel : ViewModelBase
{
    private string _logContent = "Loading logs...";
    private readonly string _reportsFolder;
    private readonly string _logsFolder;

    public ErrorReportViewModel(MultiSychConfig config)
    {
        _reportsFolder = config.Security?.ReportFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Reports");
        _logsFolder = Path.Combine(Directory.GetCurrentDirectory(), "logs");

        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        OpenReportsFolderCommand = new RelayCommand(_ => OpenFolder(_reportsFolder));

        _ = RefreshAsync();
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value);
    }

    public ObservableCollection<string> ReportFiles { get; } = new();

    private async Task RefreshAsync()
    {
        try
        {
            if (Directory.Exists(_logsFolder))
            {
                var latestLog = Directory.GetFiles(_logsFolder, "multisych-*.txt")
                                         .OrderByDescending(f => f)
                                         .FirstOrDefault();
                if (latestLog != null)
                {
                    // Log dosyasına Serilog yazmaya devam ettiği için FileShare.ReadWrite ile açıyoruz
                    using var stream = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    LogContent = await reader.ReadToEndAsync();
                    
                    if (string.IsNullOrWhiteSpace(LogContent))
                        LogContent = "Log file is currently empty.";
                }
                else
                {
                    LogContent = "No log files found in the logs directory.";
                }
            }
            else
            {
                LogContent = "Logs directory does not exist yet.";
            }
        }
        catch (Exception ex)
        {
            LogContent = $"Error reading log file: {ex.Message}";
        }

        ReportFiles.Clear();
        try
        {
            if (Directory.Exists(_reportsFolder))
            {
                var files = Directory.GetFiles(_reportsFolder).OrderByDescending(f => f).Select(Path.GetFileName);
                foreach (var f in files)
                {
                    if (f != null) ReportFiles.Add(f);
                }
            }
        }
        catch { }
    }

    private void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true };
                if (OperatingSystem.IsLinux())
                {
                    processInfo.FileName = "xdg-open";
                    processInfo.ArgumentList.Add(path);
                    processInfo.UseShellExecute = false;
                }
                System.Diagnostics.Process.Start(processInfo);
            }
            catch { }
        }
    }

}