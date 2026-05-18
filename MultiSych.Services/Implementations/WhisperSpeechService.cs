using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
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

#pragma warning disable CA1416
        private System.Speech.Synthesis.SpeechSynthesizer? _synthesizer;
#pragma warning restore CA1416

        public async Task InitializeAsync(string modelPath)
        {
            if (!File.Exists(modelPath))
            {
                _logger.Information("Whisper model not found at {ModelPath}. Downloading automatically...", modelPath);
                try
                {
                    // Whisper.net 1.9.0 ile downloader artık statik değil, instance üzerinden çağrılır
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
                var factory = WhisperFactory.FromPath(modelPath);
                _processor = factory.CreateBuilder()
                    .WithLanguage("auto") // Sesin dilini otomatik algılar
                    .Build();
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
            if (_processor == null)
                throw new InvalidOperationException("Whisper processor is not initialized.");

            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            _logger.Information("Transcribing audio file: {AudioFilePath}", audioFilePath);
            
            var resultText = string.Empty;
            using var fileStream = File.OpenRead(audioFilePath);
            
            await foreach (var result in _processor.ProcessAsync(fileStream))
            {
                resultText += result.Text + " ";
            }
            
            return resultText.Trim();
        }

        public Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

            if (OperatingSystem.IsWindows())
            {
                try
                {
#pragma warning disable CA1416
                    if (_synthesizer == null)
                    {
                        _synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                        _synthesizer.SetOutputToDefaultAudioDevice();
                    }
                    _synthesizer.SpeakAsyncCancelAll(); // Varsa önceki konuşmayı susturur
                    _synthesizer.SpeakAsync(text);
#pragma warning restore CA1416
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to speak text");
                }
            }
            else
            {
                _logger.Warning("Text-to-speech is currently only supported on Windows.");
            }

            return Task.CompletedTask;
        }

        public void StopSpeaking()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
#pragma warning disable CA1416
                    _synthesizer?.SpeakAsyncCancelAll();
#pragma warning restore CA1416
                }
                catch { }
            }
        }
    }
}