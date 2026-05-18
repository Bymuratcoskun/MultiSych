using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces
{
    public interface ISpeechService
    {
        Task InitializeAsync(string modelPath);
        Task<string> TranscribeAudioAsync(string audioFilePath);
        Task SpeakAsync(string text);
        void StopSpeaking();
    }
}