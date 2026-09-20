using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NAudio.Wave;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class NAudioRecordingService : IAudioRecordingService
    {
        private readonly ILogger _logger = Log.ForContext<NAudioRecordingService>();
        
        // Windows NAudio specific fields
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        
        // Linux/macOS process specific fields
        private Process? _recordProcess;
        
        private TaskCompletionSource<bool>? _stopTcs;

        public bool IsRecording { get; private set; }

        public void StartRecording(string outputPath)
        {
            if (IsRecording) return;

            // Ensure parent directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                StartWindowsRecording(outputPath);
            }
            else
            {
                var requiredCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "arecord" : "ffmpeg";
                if (!IsCommandAvailable(requiredCommand))
                {
                    var installCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) 
                        ? "sudo apt install alsa-utils ffmpeg" 
                        : "brew install ffmpeg";
                    throw new MultiSych.Services.Exceptions.DependencyMissingException(
                        requiredCommand, 
                        installCmd, 
                        $"Audio recording dependency '{requiredCommand}' is missing in system PATH. Please install it using: {installCmd}");
                }
                StartUnixRecording(outputPath);
            }
            
            IsRecording = true;
        }

        private bool IsCommandAvailable(string command)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return false;

            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, command);
                if (File.Exists(fullPath))
                {
                    return true;
                }
            }
            return false;
        }

        private void StartWindowsRecording(string outputPath)
        {
            _logger.Information("Starting Windows native recording using NAudio to: {Path}", outputPath);
            
            // Whisper modeli en iyi 16000 Hz, 1 Kanal (Mono) ses ile çalışır.
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1)
            };

            _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, a) =>
            {
                byte[] buffer = a.Buffer;
                int bytesRecorded = a.BytesRecorded;

                // 16-bit PCM Mono ses verisini işleme
                for (int i = 0; i < bytesRecorded; i += 2)
                {
                    short sample = (short)((buffer[i + 1] << 8) | buffer[i]);

                    // Gürültü Kapısı (Noise Gate) - Dip fan/ortam gürültülerini temizle
                    if (Math.Abs(sample) < 150)
                    {
                        sample = 0;
                    }
                    else
                    {
                        // Otomatik Kazanç Kontrolü (AGC) - Sinyali 1.5 katına çıkar
                        int amplified = (int)(sample * 1.5);
                        if (amplified > short.MaxValue) sample = short.MaxValue;
                        else if (amplified < short.MinValue) sample = short.MinValue;
                        else sample = (short)amplified;
                    }

                    buffer[i] = (byte)(sample & 0xFF);
                    buffer[i + 1] = (byte)((sample >> 8) & 0xFF);
                }

                _writer.Write(buffer, 0, bytesRecorded);
            };

            _waveIn.RecordingStopped += (s, a) =>
            {
                _writer?.Dispose();
                _writer = null;
                _waveIn?.Dispose();
                _waveIn = null;
                _stopTcs?.TrySetResult(true);
            };

            _waveIn.StartRecording();
        }

        private void StartUnixRecording(string outputPath)
        {
            _logger.Information("Starting Unix process-based recording to: {Path}", outputPath);
            
            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // arecord standard ALSA utility for recording
                psi = new ProcessStartInfo
                {
                    FileName = "arecord",
                    Arguments = $"-f S16_LE -c 1 -r 16000 -t wav \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
            }
            else // macOS / fallback
            {
                // ffmpeg configuration for audio capture
                psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -f avfoundation -i \":0\" -ar 16000 -ac 1 \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
            }

            try
            {
                _recordProcess = Process.Start(psi);
                if (_recordProcess == null || _recordProcess.HasExited)
                {
                    throw new Exception("Failed to start Unix recording process. Check if arecord or ffmpeg is installed.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start audio recording process on Unix");
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!IsRecording) return;
            
            IsRecording = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _stopTcs = new TaskCompletionSource<bool>();
                _waveIn?.StopRecording();
                await _stopTcs.Task; // Wait for NAudio file release
            }
            else
            {
                if (_recordProcess != null && !_recordProcess.HasExited)
                {
                    try
                    {
                        _logger.Information("Stopping Unix recording process gracefully...");
                        // Send SIGINT (Ctrl+C signal) to process to write clean WAV headers
                        using var killProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "kill",
                            Arguments = $"-2 {_recordProcess.Id}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        
                        if (killProcess != null)
                        {
                            await killProcess.WaitForExitAsync();
                        }

                        // Wait for process to shut down gracefully
                        using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3)))
                        {
                            await _recordProcess.WaitForExitAsync(cts.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to gracefully stop Unix recording process. Force killing...");
                        try { _recordProcess.Kill(); } catch { }
                    }
                    finally
                    {
                        _recordProcess.Dispose();
                        _recordProcess = null;
                    }
                }
            }
        }
    }
}
