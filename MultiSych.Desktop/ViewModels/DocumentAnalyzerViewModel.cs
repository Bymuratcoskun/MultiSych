using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using MultiSych.Desktop.Services;
using MultiSych.Services.Models;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public sealed class DocumentAnalyzerViewModel : ViewModelBase
{
    private readonly IAIService _aiService;
    private readonly IWindowService _windowService;
    private string _documentContent = string.Empty;
    private string _summaryResult = string.Empty;
    private string _statusMessage = "Paste your document content and click Summarize.";
    private string _selectedProvider = "hybrid";
    private bool _isAnalyzing;
    private string _emailFrom = string.Empty;
    private string _emailSubject = string.Empty;
    private string _emailBody = string.Empty;
    private string _emailAnalysisResult = string.Empty;

    public DocumentAnalyzerViewModel(IAIService aiService, IWindowService windowService)
    {
        _aiService = aiService;
        _windowService = windowService;
        AvailableProviders = new ObservableCollection<string> { "hybrid", "copilot", "gemini", "yandex" };
        SummarizeCommand = new RelayCommand(async _ => await SummarizeAsync(), _ => !string.IsNullOrWhiteSpace(DocumentContent) && !IsAnalyzing);
        AnalyzeEmailCommand = new RelayCommand(async _ => await AnalyzeEmailAsync(), _ => !string.IsNullOrWhiteSpace(EmailBody) && !IsAnalyzing);
        LoadFileCommand = new RelayCommand(async _ => await LoadFileAsync());
    }

    public ObservableCollection<string> AvailableProviders { get; }
    public ICommand SummarizeCommand { get; }
    public ICommand AnalyzeEmailCommand { get; }
    public ICommand LoadFileCommand { get; }

    public string DocumentContent
    {
        get => _documentContent;
        set
        {
            if (SetProperty(ref _documentContent, value))
            {
                (SummarizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SummaryResult
    {
        get => _summaryResult;
        set => SetProperty(ref _summaryResult, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set 
        { 
            if (SetProperty(ref _isAnalyzing, value))
            {
                (SummarizeCommand as RelayCommand)?.RaiseCanExecuteChanged(); 
                (AnalyzeEmailCommand as RelayCommand)?.RaiseCanExecuteChanged(); 
            }
        }
    }

    public string EmailFrom
    {
        get => _emailFrom;
        set => SetProperty(ref _emailFrom, value);
    }

    public string EmailSubject
    {
        get => _emailSubject;
        set => SetProperty(ref _emailSubject, value);
    }

    public string EmailBody
    {
        get => _emailBody;
        set
        {
            if (SetProperty(ref _emailBody, value))
            {
                (AnalyzeEmailCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string EmailAnalysisResult
    {
        get => _emailAnalysisResult;
        set => SetProperty(ref _emailAnalysisResult, value);
    }

    private async Task LoadFileAsync()
    {
        var filePath = await _windowService.OpenFileDialogAsync("Select a document", new[] { "*.txt", "*.md", "*.json", "*.csv", "*.log" });
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                StatusMessage = $"Loading file: {Path.GetFileName(filePath)}...";
                var content = await File.ReadAllTextAsync(filePath);
                DocumentContent = content;
                StatusMessage = "File loaded successfully. Ready to summarize.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading file: {ex.Message}";
            }
        }
    }

    private async Task SummarizeAsync()
    {
        if (string.IsNullOrWhiteSpace(DocumentContent)) return;
        
        IsAnalyzing = true;
        StatusMessage = $"Summarizing document using {SelectedProvider}...";
        SummaryResult = string.Empty;
        
        try 
        {
            SummaryResult = await _aiService.SummarizeDocumentAsync(DocumentContent, SelectedProvider);
            StatusMessage = "Summary generated successfully.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsAnalyzing = false; }
    }

    private async Task AnalyzeEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(EmailBody)) return;
        
        IsAnalyzing = true;
        StatusMessage = $"Analyzing email using {SelectedProvider}...";
        EmailAnalysisResult = string.Empty;
        
        try 
        {
            var emailMessage = new EmailMessage
            {
                From = string.IsNullOrWhiteSpace(EmailFrom) ? "Unknown Sender" : EmailFrom,
                Subject = string.IsNullOrWhiteSpace(EmailSubject) ? "No Subject" : EmailSubject,
                Body = EmailBody
            };
            EmailAnalysisResult = await _aiService.AnalyzeEmailAsync(emailMessage, SelectedProvider);
            StatusMessage = "Email analysis completed successfully.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsAnalyzing = false; }
    }
}