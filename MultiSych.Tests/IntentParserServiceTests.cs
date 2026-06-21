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
        [InlineData("Rastgele bir komut söyle", "Unknown")]
        public async Task ParseIntentAsync_FallbackKeywords_ShouldMatchExactly(string input, string expectedIntent)
        {
            // Arrange
            var mockAiService = new Mock<IAIService>();
            // Force AI call to throw an exception to trigger the fallback keyword matcher
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
                .ReturnsAsync("Summarize");

            var service = new IntentParserService(mockAiService.Object);

            // Act
            var result = await service.ParseIntentAsync("Analiz ekranına geç");

            // Assert
            Assert.Equal("Summarize", result);
            mockAiService.Verify(ai => ai.GetResponseAsync(It.IsAny<string>(), "hybrid"), Times.Once);
        }
    }
}
