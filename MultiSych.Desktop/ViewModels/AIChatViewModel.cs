using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Security;

namespace MultiSych.Desktop.ViewModels;

public class AIChatViewModel : ViewModelBase
{
    private readonly IAIService _aiService;
    private readonly MultiSychConfig _config;
    private readonly ISecureStorageService _secureStorage;
    private readonly ISpeechService _speechService;
    private readonly IWindowService _windowService;
    private readonly IAudioRecordingService _audioRecordingService;
    private string _currentMessage = string.Empty;
    private string _selectedModel = "default";
    private string _settingsStatus = "Ready";
    private string _apiKey = string.Empty;
    private bool _isSending;
    private bool _isSpeechModelLoaded;
    private bool _isRecording;
    private string _tempAudioFilePath = string.Empty;
    private bool _isAutoTtsEnabled;

    public AIChatViewModel(string provider, IServiceProvider services)
    {
        Provider = provider;
        _aiService = services.GetService(typeof(IAIService)) as IAIService ?? throw new InvalidOperationException("AI service is missing");
        _config = services.GetService(typeof(MultiSychConfig)) as MultiSychConfig ?? throw new InvalidOperationException("App configuration is missing");
        _secureStorage = services.GetService(typeof(ISecureStorageService)) as ISecureStorageService ?? throw new InvalidOperationException("Secure storage is missing");
        _speechService = services.GetService(typeof(ISpeechService)) as ISpeechService ?? throw new InvalidOperationException("Speech service is missing");
        _windowService = services.GetService(typeof(IWindowService)) as IWindowService ?? throw new InvalidOperationException("Window service is missing");
        _audioRecordingService = services.GetService(typeof(IAudioRecordingService)) as IAudioRecordingService ?? throw new InvalidOperationException("Audio recording service is missing");

        ProviderDisplayName = provider switch
        {
            "copilot" => "Copilot Chat",
            "gemini" => "Gemini Chat",
            "yandex" => "Yandex AI Chat",
            _ => "AI Chat"
        };

        AvailableModels = new ObservableCollection<string> { "default", "fast", "advanced" };
        SelectedModel = AvailableModels[0];
        ChatMessages = new ObservableCollection<ChatMessage>();
        ApiKey = GetProviderApiKey(provider);

        SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(CurrentMessage) && !IsSending);
        ToggleSettingsCommand = new RelayCommand(_ => SettingsStatus = "Settings panel active. Update API keys and model selection here.");
        UpdateApiKeyCommand = new RelayCommand(async _ => await UpdateApiKeyAsync());
        ToggleRecordingCommand = new RelayCommand(async _ => await ToggleRecordingAsync(), _ => !IsSending);
        ToggleAutoTtsCommand = new RelayCommand(_ => IsAutoTtsEnabled = !IsAutoTtsEnabled);
    }

    public ICommand SendMessageCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand UpdateApiKeyCommand { get; }
    public ICommand ToggleRecordingCommand { get; }
    public ICommand ToggleAutoTtsCommand { get; }

    public string Provider { get; }
    public string ProviderDisplayName { get; }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public ObservableCollection<string> AvailableModels { get; }

    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        set => SetProperty(ref _settingsStatus, value);
    }

    public bool IsSending
    {
        get => _isSending;
        private set
        {
            if (SetProperty(ref _isSending, value))
            {
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ToggleRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string RecordButtonText => IsRecording ? "⏹️" : "🎤";
    public string TtsButtonText => IsAutoTtsEnabled ? "🔊" : "🔇";

    public bool IsAutoTtsEnabled
    {
        get => _isAutoTtsEnabled;
        private set
        {
            if (SetProperty(ref _isAutoTtsEnabled, value))
            {
                OnPropertyChanged(nameof(TtsButtonText));
                
                if (!value)
                {
                    _speechService.StopSpeaking();
                }
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(RecordButtonText));
                (ToggleRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<ChatMessage> ChatMessages { get; }

    public string CurrentMessage
    {
        get => _currentMessage;
        set
        {
            if (SetProperty(ref _currentMessage, value))
            {
                (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage) || IsSending)
            return;

        ChatMessages.Add(new ChatMessage("You", CurrentMessage, true));
        var messageToSend = CurrentMessage;
        CurrentMessage = string.Empty;
        IsSending = true;
        SettingsStatus = "Sending message...";

        try
        {
            var response = await _aiService.SendMessageAsync(messageToSend, new List<string>(), Provider);
            ChatMessages.Add(new ChatMessage(ProviderDisplayName, response, false));
            SettingsStatus = "Message delivered.";
            
            if (IsAutoTtsEnabled)
            {
                await _speechService.SpeakAsync(response);
            }
        }
        catch (Exception ex)
        {
            ChatMessages.Add(new ChatMessage("System", $"Error: {ex.Message}", false));
            SettingsStatus = "Failed to send message.";
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task ToggleRecordingAsync()
    {
        try
        {
            if (!IsRecording)
            {
                if (!_isSpeechModelLoaded)
                {
                    SettingsStatus = "Loading/Downloading local Whisper AI model... This might take a minute on first run.";
                    var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "ggml-base.bin");
                    await _speechService.InitializeAsync(modelPath);
                    _isSpeechModelLoaded = true;
                }

                _tempAudioFilePath = Path.Combine(Path.GetTempPath(), $"multisych_mic_{Guid.NewGuid()}.wav");
                _audioRecordingService.StartRecording(_tempAudioFilePath);
                IsRecording = true;
                SettingsStatus = "Recording... Click the stop button to transcribe.";
            }
            else
            {
                SettingsStatus = "Transcribing audio locally...";
                await _audioRecordingService.StopRecordingAsync();
                IsRecording = false;

                try
                {
                    var text = await _speechService.TranscribeAudioAsync(_tempAudioFilePath);
                    CurrentMessage += string.IsNullOrWhiteSpace(CurrentMessage) ? text : $" {text}";
                    SettingsStatus = "Audio transcription complete.";
                }
                finally
                {
                    // Gizlilik (Privacy): Hata olsa dahi özel ses kaydının diskten silinmesini garanti ediyoruz
                    try { if (File.Exists(_tempAudioFilePath)) File.Delete(_tempAudioFilePath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            SettingsStatus = $"Audio operation failed: {ex.Message}";
            if (IsRecording) await _audioRecordingService.StopRecordingAsync();
            IsRecording = false;
        }
    }

    private async Task UpdateApiKeyAsync()
    {
        if (_config.AI is not null)
        {
            switch (Provider)
            {
                case "copilot":
                    _config.AI.CopilotApiKey = ApiKey;
                    await _secureStorage.SaveSecretAsync("COPILOT_API_KEY", ApiKey);
                    break;
                case "gemini":
                    _config.AI.GeminiApiKey = ApiKey;
                    await _secureStorage.SaveSecretAsync("GEMINI_API_KEY", ApiKey);
                    break;
                case "yandex":
                    _config.AI.YandexAiApiKey = ApiKey;
                    await _secureStorage.SaveSecretAsync("YANDEX_AI_API_KEY", ApiKey);
                    break;
            }

            SettingsStatus = "API key saved to .env and current session.";
            return;
        }

        SettingsStatus = "API key updated for current session.";
    }

    private string GetProviderApiKey(string provider)
    {
        return provider switch
        {
            "copilot" => _config.AI?.CopilotApiKey ?? "Not configured",
            "gemini" => _config.AI?.GeminiApiKey ?? "Not configured",
            "yandex" => _config.AI?.YandexAiApiKey ?? "Not configured",
            _ => "Not configured"
        };
    }
}
