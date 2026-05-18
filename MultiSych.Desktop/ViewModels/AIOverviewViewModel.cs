using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public sealed class AIOverviewViewModel : ViewModelBase
{
    private readonly IWindowService _windowService;
    private readonly MultiSychConfig _config;
    private string _statusMessage = "Open one of the AI providers to start a chat session.";

    public AIOverviewViewModel(IWindowService windowService, MultiSychConfig config)
    {
        _windowService = windowService;
        _config = config;

        ProviderButtons = new ObservableCollection<string> { "Copilot", "Gemini", "Yandex" };
        OpenChatCommand = new RelayCommand(provider => _windowService.ShowAIChat(provider?.ToString()?.ToLowerInvariant() ?? "copilot"));
        LoadApiKeyStatus();
    }

    public ObservableCollection<string> ProviderButtons { get; }
    public ICommand OpenChatCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private void LoadApiKeyStatus()
    {
        var configured = new List<string>();
        if (!string.IsNullOrWhiteSpace(_config.AI?.CopilotApiKey)) configured.Add("Copilot");
        if (!string.IsNullOrWhiteSpace(_config.AI?.GeminiApiKey)) configured.Add("Gemini");
        if (!string.IsNullOrWhiteSpace(_config.AI?.YandexAiApiKey)) configured.Add("Yandex");

        StatusMessage = configured.Count > 0
            ? $"Configured providers: {string.Join(", ", configured)}."
            : "No AI provider credentials configured yet.";
    }
}
