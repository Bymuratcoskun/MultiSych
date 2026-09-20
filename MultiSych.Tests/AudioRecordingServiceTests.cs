using System;
using System.IO;
using System.Runtime.InteropServices;
using MultiSych.Services.Exceptions;
using MultiSych.Services.Implementations;
using Xunit;

namespace MultiSych.Tests
{
    public class AudioRecordingServiceTests
    {
        [Fact]
        public void StartRecording_WhenDependencyMissingOnUnix_ShouldThrowDependencyMissingException()
        {
            // Bu testi sadece Unix (Linux/macOS) işletim sistemlerinde çalıştırıyoruz
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            // Arrange
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                // PATH'i boşaltarak arecord ve ffmpeg komutlarının bulunamamasını sağlıyoruz
                Environment.SetEnvironmentVariable("PATH", string.Empty);

                var service = new NAudioRecordingService();
                var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

                // Act & Assert
                Assert.Throws<DependencyMissingException>(() => service.StartRecording(tempFile));
            }
            finally
            {
                // PATH'i eski haline getiriyoruz
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
    }
}
