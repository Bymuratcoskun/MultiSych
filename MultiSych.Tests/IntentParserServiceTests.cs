using System;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;

namespace MultiSych.Tests
{
    public class IntentParserServiceTests
    {
        [Fact]
        public async Task ParseIntentAsync_EmptyText_ShouldReturnUnknown()
        {
            // Arrange
            var mockAiService = new Mock<IAIService>();
            var service = new IntentParserService(mockAiService.Object);

            // Act
            var result = await service.ParseIntentAsync(string.Empty);

            // Assert
            Assert.Equal("Unknown", result);
        }

        [Theory]
        [InlineData("Lütfen hesapları senkronize et", "Sync")]
        [InlineData("Dosyaları eşitle", "Sync")]
        [InlineData("Gelen mailleri güncelle", "Sync")]
        [InlineData("Şu maili özetler misin", "Summarize")]
        [InlineData("Bugün gelen maillerin özetini çıkar", "Summarize")]
        [InlineData("Takvimimi açar mısın", "Calendar")]
        [InlineData("Yaklaşan etkinlikleri göster", "Calendar")]
        [InlineData("Genel bakış paneline git", "Dashboard")]
        [InlineData("Ana ekranı aç", "Dashboard")]
        [InlineData("Bağlı hesapları göster", "Accounts")]
        [InlineData("Hesap yönetimi sekmesine geç", "Accounts")]
        [InlineData("Yapay zeka asistanını aç", "AI")]
        [InlineData("Robot asistanı göster", "AI")]
        [InlineData("Dosya gezginine git", "Explorer")]
        [InlineData("Sanal sürücü dosyalarını göster", "Explorer")]
        [InlineData("Sistem günlüklerini aç", "Logs")]
        [InlineData("Hata loglarını göster", "Logs")]
        [InlineData("Uygulama ayarlarını aç", "Settings")]
        [InlineData("Dil ve tema ayarlarını göster", "Settings")]
        [InlineData("Sohbet robotuna geç", "Chat")]
        [InlineData("Mesajlaşmayı başlat", "Chat")]
        [InlineData("Rastgele bir komut söyle", "Unknown")]
        public async Task ParseIntentAsync_FallbackKeywords_ShouldMatchExactly(string input, string expectedIntent)
        {
            // Arrange
            var mockAiService = new Mock<IAIService>();
            // Force AI call to throw an exception to trigger the offline NLU / fallback keyword matcher
            mockAiService
                .Setup(ai => ai.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("AI service offline"));

            var service = new IntentParserService(mockAiService.Object);

            // Act
            var result = await service.ParseIntentAsync(input);

            // Assert
            Assert.Equal(expectedIntent, result);
        }

        [Fact]
        public async Task ParseIntentAsync_WithAIServiceWorking_ShouldReturnAIResult()
        {
            // Arrange
            var mockAiService = new Mock<IAIService>();
            mockAiService
                .Setup(ai => ai.GetResponseAsync(It.IsAny<string>(), "hybrid"))
                .ReturnsAsync("Chat");

            var service = new IntentParserService(mockAiService.Object);

            // Act
            // "bilgisayarı kapat" has no similarity overlap with any sample phrases, so it will call the mock AI service.
            var result = await service.ParseIntentAsync("bilgisayarı kapat");

            // Assert
            Assert.Equal("Chat", result);
            mockAiService.Verify(ai => ai.GetResponseAsync(It.IsAny<string>(), "hybrid"), Times.Once);
        }
    }
}
