using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface IAudioRecordingService
    {
        bool IsRecording { get; }
        void StartRecording(string outputPath);
        Task StopRecordingAsync();
    }
}