using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Configuration;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.ViewModels;

public abstract class MessageSegment : ViewModelBase { }

public class TextSegment : MessageSegment
{
    private string _text = string.Empty;
    public string Text { get => _text; set => SetProperty(ref _text, value); }
}

public class CodeSegment : MessageSegment
{
    private string _code = string.Empty;
    private string _language = "text";
    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Language { get => _language; set => SetProperty(ref _language, value); }
}

public class ChatUIMessage : ViewModelBase
{
    public ObservableCollection<MessageSegment> Segments { get; } = [];
    public bool IsUser { get; set; }
    public string SenderName => IsUser ? "Siz" : "Yapay Zeka";
    public string Time => DateTime.Now.ToString("HH:mm");
}

public class ChatViewModel : ViewModelBase
{
    private readonly IAIService _aiService;
    private readonly MultiSychConfig _config;
    private readonly IAudioRecordingService _audioRecordingService;
    private readonly ISpeechService _speechService;
    private string _inputText = string.Empty;
    private bool _isBusy;
    private bool _isRecording;
    private bool _isSpeechModelLoaded;
    private string _tempAudioFilePath = string.Empty;

    public ObservableCollection<ChatUIMessage> Messages { get; } = [];

    public ChatViewModel(IAIService aiService, MultiSychConfig config, IAudioRecordingService audioRecordingService, ISpeechService speechService)
    {
        _aiService = aiService;
        _config = config;
        _audioRecordingService = audioRecordingService;
        _speechService = speechService;
        SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsBusy);
        ToggleRecordingCommand = new RelayCommand(async _ => await ToggleRecordingAsync(), _ => !IsBusy);

        // Başlangıç mesajı
        var initialMessage = new ChatUIMessage { IsUser = false };
        initialMessage.Segments.Add(new TextSegment { Text = "Merhaba! Size nasıl yardımcı olabilirim? Dosyalarınızı özetleyebilir, programınızı sorgulayabilir veya genel sorularınızı yanıtlayabilirim." });
        Messages.Add(initialMessage);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set 
        {
            if (SetProperty(ref _isBusy, value))
            {
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ToggleRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    public string RecordButtonText => IsRecording ? "⏹️" : "🎤";

    public ICommand SendCommand { get; }
    public ICommand ToggleRecordingCommand { get; }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText;
        InputText = string.Empty;
        
        var userMessage = new ChatUIMessage { IsUser = true };
        userMessage.Segments.Add(new TextSegment { Text = userText });
        Messages.Add(userMessage);
        
        IsBusy = true;
        
        try
        {
            var provider = _config.AI?.DefaultProvider ?? "hybrid";

            // Son 10 mesajı context olarak al
            var conversationHistory = Messages
                .TakeLast(10)
                .Select(m => string.Join(Environment.NewLine, m.Segments.Select(s => s is TextSegment ts ? ts.Text : (s as CodeSegment)?.Code ?? "")))
                .ToList();

            var response = await _aiService.SendMessageAsync(userText, conversationHistory, provider);
            
            var aiMessage = new ChatUIMessage { IsUser = false };
            Dispatcher.UIThread.Post(() => Messages.Add(aiMessage));
            
            // AI Cevabını daktilo efekti ile ekrana yansıt ve bitene kadar UI'ın meşgul (IsBusy) kalmasını sağla
            await TypewriterEffectAsync(aiMessage, response);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => 
            {
                var errorMessage = new ChatUIMessage { IsUser = false };
                errorMessage.Segments.Add(new TextSegment { Text = $"Hata: {ex.Message}" });
                Messages.Add(errorMessage);
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task TypewriterEffectAsync(ChatUIMessage message, string rawText)
    {
        var segments = ParseSegments(rawText);
        
        foreach (var seg in segments)
        {
            if (seg is TextSegment textSeg)
            {
                var liveSeg = new TextSegment { Text = "" };
                Dispatcher.UIThread.Post(() => message.Segments.Add(liveSeg));
                
                // Metinleri kelime kelime (boşlukları koruyarak) ayır ve yaz
                var words = Regex.Split(textSeg.Text, @"(?<=\s+)");
                foreach (var word in words)
                {
                    Dispatcher.UIThread.Post(() => liveSeg.Text += word);
                    await Task.Delay(30); // Kelime başı daktilo gecikmesi
                }
            }
            else if (seg is CodeSegment codeSeg)
            {
                var liveSeg = new CodeSegment { Language = codeSeg.Language, Code = "" };
                Dispatcher.UIThread.Post(() => message.Segments.Add(liveSeg));
                
                // Kod bloklarını satır satır veya daha hızlı yaz
                var lines = Regex.Split(codeSeg.Code, @"(?<=\n)");
                foreach (var line in lines)
                {
                    Dispatcher.UIThread.Post(() => liveSeg.Code += line);
                    await Task.Delay(20); 
                }
            }
        }
    }

    private List<MessageSegment> ParseSegments(string rawText)
    {
        var result = new List<MessageSegment>();
        var lastIndex = 0;
        var regex = new Regex(@"```(?<lang>\w*)\r?\n(?<code>[\s\S]*?)\r?\n```", RegexOptions.Multiline);
    
        foreach (Match match in regex.Matches(rawText))
        {
            if (match.Index > lastIndex)
            {
                result.Add(new TextSegment { Text = rawText.Substring(lastIndex, match.Index - lastIndex) });
            }

            var lang = match.Groups["lang"].Value;
            var code = match.Groups["code"].Value;
            result.Add(new CodeSegment { Language = string.IsNullOrWhiteSpace(lang) ? "cs" : lang, Code = code });

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < rawText.Length)
        {
            result.Add(new TextSegment { Text = rawText.Substring(lastIndex) });
        }
        if (lastIndex == 0 && !string.IsNullOrEmpty(rawText))
        {
            result.Add(new TextSegment { Text = rawText });
        }
        return result;
    }

    private async Task ToggleRecordingAsync()
    {
        try
        {
            if (!IsRecording)
            {
                IsBusy = true;
                if (!_isSpeechModelLoaded)
                {
                    var loadingMessage = new ChatUIMessage { IsUser = false };
                    loadingMessage.Segments.Add(new TextSegment { Text = "Whisper AI modeli yükleniyor... Bu işlem ilk seferde biraz zaman alabilir." });
                    Dispatcher.UIThread.Post(() => Messages.Add(loadingMessage));
                    var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "ggml-base.bin");
                    await _speechService.InitializeAsync(modelPath);
                    _isSpeechModelLoaded = true;
                }

                _tempAudioFilePath = Path.Combine(Path.GetTempPath(), $"multisych_mic_{Guid.NewGuid()}.wav");
                _audioRecordingService.StartRecording(_tempAudioFilePath);
                IsRecording = true;
                IsBusy = false;
            }
            else
            {
                IsBusy = true;
                await _audioRecordingService.StopRecordingAsync();
                IsRecording = false;

                try
                {
                    var processingMessage = new ChatUIMessage { IsUser = false };
                    processingMessage.Segments.Add(new TextSegment { Text = "Ses işleniyor..." });
                    Dispatcher.UIThread.Post(() => Messages.Add(processingMessage));
                    var text = await _speechService.TranscribeAudioAsync(_tempAudioFilePath);
                    InputText += string.IsNullOrWhiteSpace(InputText) ? text : $" {text}";
                }
                finally
                {
                    try { if (File.Exists(_tempAudioFilePath)) File.Delete(_tempAudioFilePath); } catch { }
                    IsBusy = false;
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = new ChatUIMessage { IsUser = false };
            errorMessage.Segments.Add(new TextSegment { Text = $"Ses kaydı hatası: {ex.Message}" });
            Dispatcher.UIThread.Post(() => Messages.Add(errorMessage));
            if (IsRecording) await _audioRecordingService.StopRecordingAsync();
            IsRecording = false;
            IsBusy = false;
        }
    }
}