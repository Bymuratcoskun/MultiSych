using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using MultiSych.Services.Interfaces;
using Serilog;
using Whisper.net;
using Whisper.net.Ggml;

namespace MultiSych.Services.Implementations
{
    public class WhisperSpeechService : ISpeechService
    {
        private WhisperProcessor? _processor;
        private readonly ILogger _logger = Log.ForContext<WhisperSpeechService>();

        private readonly IIntentParserService _intentParserService;
        private readonly ISyncSignalService _syncSignalService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IEventBus _eventBus;

        private string? _modelPath;
        private string _loadedLanguage = "auto";
        private WhisperFactory? _factory;

        private System.Threading.Timer? _realTimeTimer;
        private bool _isRealTimeTranscribing = false;
        private string? _realTimeTempFilePath;

        public WhisperSpeechService(
            IIntentParserService intentParserService,
            ISyncSignalService syncSignalService,
            IUserSettingsService userSettingsService,
            IEventBus eventBus)
        {
            _intentParserService = intentParserService;
            _syncSignalService = syncSignalService;
            _userSettingsService = userSettingsService;
            _eventBus = eventBus;
        }

#if WINDOWS
        private System.Speech.Synthesis.SpeechSynthesizer? _synthesizer;
#endif

        public async Task InitializeAsync(string modelPath)
        {
            _modelPath = modelPath;

            if (!File.Exists(modelPath))
            {
                _logger.Information("Whisper model not found at {ModelPath}. Downloading automatically...", modelPath);
                try
                {
                    using var httpClient = new HttpClient();
                    using var modelStream = await new WhisperGgmlDownloader(httpClient).GetGgmlModelAsync(GgmlType.Base);
                    using var fileWriter = File.OpenWrite(modelPath);
                    await modelStream.CopyToAsync(fileWriter);
                    _logger.Information("Whisper model downloaded successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to download Whisper model");
                    throw new Exception($"Failed to download Whisper model: {ex.Message}", ex);
                }
            }

            try
            {
                var targetLanguage = GetTargetLanguageCode();
                EnsureProcessor(targetLanguage);
                _logger.Information("Whisper processor initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize Whisper processor");
                throw;
            }
        }

        public async Task<string> TranscribeAudioAsync(string audioFilePath)
        {
            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            var targetLanguage = GetTargetLanguageCode();
            EnsureProcessor(targetLanguage);

            if (_processor == null)
                throw new InvalidOperationException("Whisper processor is not initialized.");

            _logger.Information("Transcribing audio file: {AudioFilePath} with language: {Language}", audioFilePath, targetLanguage);
            
            var resultText = string.Empty;
            using var fileStream = File.OpenRead(audioFilePath);
            var segmentLines = new System.Collections.Generic.List<string>();
            
            await foreach (var result in _processor.ProcessAsync(fileStream))
            {
                resultText += result.Text + " ";
                
                var startFormatted = $"{result.Start.Minutes:D2}:{result.Start.Seconds:D2}";
                var endFormatted = $"{result.End.Minutes:D2}:{result.End.Seconds:D2}";
                segmentLines.Add($"[{startFormatted} - {endFormatted}]: {result.Text.Trim()}");
            }
            
            resultText = resultText.Trim();

            if (segmentLines.Count > 0)
            {
                var diarizationOutput = string.Join("\n", segmentLines);
                _logger.Information("Zaman damgalı konuşma analizi:\n{Diarization}", diarizationOutput);
                _eventBus.Publish(new DetailedTranscriptionEvent(diarizationOutput));
            }

            var intent = await _intentParserService.ParseIntentAsync(resultText);
            if (intent == "Sync")
            {
                _logger.Information("Sesli komut algılandı: 'Sync'. Arka plan senkronizasyonu tetikleniyor.");
                _syncSignalService.TriggerSync();
                _eventBus.Publish(new NotificationIntentEvent("Arka plan senkronizasyonu başlatıldı."));
                _ = SpeakAsync("Senkronizasyon başlatılıyor");
            }
            else if (intent == "Summarize")
            {
                _logger.Information("Sesli komut algılandı: 'Summarize'. Analiz ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Analyzer"));
                _eventBus.Publish(new NotificationIntentEvent("Belge analiz ekranı açıldı."));
                _ = SpeakAsync("Belge analiz ekranı açılıyor");
            }
            else if (intent == "Calendar")
            {
                _logger.Information("Sesli komut algılandı: 'Calendar'. Takvim ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Calendar"));
                _eventBus.Publish(new NotificationIntentEvent("Takvim sekmesine geçildi."));
                _ = SpeakAsync("Takvim sekmesine geçiliyor");
            }
            else if (intent == "Dashboard")
            {
                _logger.Information("Sesli komut algılandı: 'Dashboard'. Genel bakış ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Dashboard"));
                _eventBus.Publish(new NotificationIntentEvent("Genel bakış ekranı açıldı."));
                _ = SpeakAsync("Genel bakış ekranı açılıyor");
            }
            else if (intent == "Accounts")
            {
                _logger.Information("Sesli komut algılandı: 'Accounts'. Bağlı hesaplar ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Accounts"));
                _eventBus.Publish(new NotificationIntentEvent("Bağlı hesaplar ekranı açıldı."));
                _ = SpeakAsync("Bağlı hesaplar ekranı açılıyor");
            }
            else if (intent == "AI")
            {
                _logger.Information("Sesli komut algılandı: 'AI'. Yapay zeka asistan genel bakışına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("AI"));
                _eventBus.Publish(new NotificationIntentEvent("Yapay zeka asistanı açıldı."));
                _ = SpeakAsync("Yapay zeka asistanı açılıyor");
            }
            else if (intent == "Explorer")
            {
                _logger.Information("Sesli komut algılandı: 'Explorer'. Dosya gezginine geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Explorer"));
                _eventBus.Publish(new NotificationIntentEvent("Dosya gezgini açıldı."));
                _ = SpeakAsync("Dosya gezgini açılıyor");
            }
            else if (intent == "Logs")
            {
                _logger.Information("Sesli komut algılandı: 'Logs'. Sistem logları ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Logs"));
                _eventBus.Publish(new NotificationIntentEvent("Sistem logları ekranı açıldı."));
                _ = SpeakAsync("Sistem logları ekranı açılıyor");
            }
            else if (intent == "Settings")
            {
                _logger.Information("Sesli komut algılandı: 'Settings'. Ayarlar ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Settings"));
                _eventBus.Publish(new NotificationIntentEvent("Ayarlar ekranı açıldı."));
                _ = SpeakAsync("Ayarlar ekranı açılıyor");
            }
            else if (intent == "Chat")
            {
                _logger.Information("Sesli komut algılandı: 'Chat'. Sohbet ekranına geçiliyor.");
                _eventBus.Publish(new NavigationIntentEvent("Chat"));
                _eventBus.Publish(new NotificationIntentEvent("Sohbet ekranı açıldı."));
                _ = SpeakAsync("Sohbet ekranı açılıyor");
            }
            
            return resultText;
        }

        public Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (_synthesizer == null)
                    {
                        _synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                        _synthesizer.SetOutputToDefaultAudioDevice();
                    }
                    _synthesizer.SpeakAsyncCancelAll();
                    _synthesizer.SpeakAsync(text);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to speak text");
                }
            }
            else
#endif
            if (OperatingSystem.IsMacOS())
            {
                try
                {
                    Process.Start(new ProcessStartInfo("say", $"\"{text.Replace("\"", "\\\"")}\"") { CreateNoWindow = true });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to speak text on macOS");
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    Process.Start(new ProcessStartInfo("spd-say", $"\"{text.Replace("\"", "\\\"")}\"") { CreateNoWindow = true });
                }
                catch
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("espeak", $"\"{text.Replace("\"", "\\\"")}\"") { CreateNoWindow = true });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to speak text on Linux (spd-say/espeak not found)");
                    }
                }
            }

            return Task.CompletedTask;
        }

        public void StopSpeaking()
        {
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                try { _synthesizer?.SpeakAsyncCancelAll(); }
                catch { }
            }
#endif
        }

        private string GetTargetLanguageCode()
        {
            var userLang = _userSettingsService?.Settings?.Language;
            return userLang switch
            {
                "Türkçe" => "tr",
                "English" => "en",
                _ => "auto"
            };
        }

        private void EnsureProcessor(string targetLanguage)
        {
            if (string.IsNullOrEmpty(_modelPath)) return;
            if (_factory == null)
            {
                _factory = WhisperFactory.FromPath(_modelPath);
            }

            if (_processor == null || _loadedLanguage != targetLanguage)
            {
                _processor?.Dispose();
                _processor = _factory.CreateBuilder()
                    .WithLanguage(targetLanguage)
                    .Build();
                _loadedLanguage = targetLanguage;
                _logger.Information("Whisper processor recreated/initialized for language: {Language}", targetLanguage);
            }
        }

        public void StartRealTimeTranscription(string tempFilePath)
        {
            StopRealTimeTranscription();
            _realTimeTempFilePath = tempFilePath;
            
            _realTimeTimer = new System.Threading.Timer(async _ =>
            {
                if (_isRealTimeTranscribing) return;
                _isRealTimeTranscribing = true;

                try
                {
                    var partialCopy = _realTimeTempFilePath + ".partial";
                    if (File.Exists(_realTimeTempFilePath))
                    {
                        File.Copy(_realTimeTempFilePath, partialCopy, true);

                        var fileInfo = new System.IO.FileInfo(partialCopy);
                        if (fileInfo.Length > 44)
                        {
                            var targetLanguage = GetTargetLanguageCode();
                            EnsureProcessor(targetLanguage);

                            if (_processor != null)
                            {
                                var text = "";
                                using var fileStream = File.OpenRead(partialCopy);
                                await foreach (var segment in _processor.ProcessAsync(fileStream))
                                {
                                    text += segment.Text + " ";
                                }

                                text = text.Trim();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    _eventBus.Publish(new PartialTranscriptionEvent(text));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to perform real-time partial transcription");
                }
                finally
                {
                    _isRealTimeTranscribing = false;
                }
            }, null, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(2.5));
            
            _logger.Information("Real-time transcription timer started.");
        }

        public void StopRealTimeTranscription()
        {
            if (_realTimeTimer != null)
            {
                _realTimeTimer.Dispose();
                _realTimeTimer = null;
                _logger.Information("Real-time transcription timer stopped.");
            }

            try
            {
                var partialCopy = _realTimeTempFilePath + ".partial";
                if (File.Exists(partialCopy))
                {
                    File.Delete(partialCopy);
                }
            }
            catch { }
        }
    }
}
