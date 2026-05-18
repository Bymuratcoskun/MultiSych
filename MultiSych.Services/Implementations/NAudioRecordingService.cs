using System;
using System.Threading.Tasks;
using NAudio.Wave;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations
{
    public class NAudioRecordingService : IAudioRecordingService
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private TaskCompletionSource<bool>? _stopTcs;

        public bool IsRecording { get; private set; }

        public void StartRecording(string outputPath)
        {
            if (IsRecording) return;

            // Whisper modeli en iyi 16000 Hz, 1 Kanal (Mono) ses ile çalışır.
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1)
            };

            _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, a) =>
            {
                _writer.Write(a.Buffer, 0, a.BytesRecorded);
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
            IsRecording = true;
        }

        public Task StopRecordingAsync()
        {
            if (!IsRecording) return Task.CompletedTask;
            
            IsRecording = false;
            _stopTcs = new TaskCompletionSource<bool>();
            _waveIn?.StopRecording();
            return _stopTcs.Task; // Dosyanın diske tamamen yazılmasını ve kilitlerin kalkmasını bekler
        }
    }
}