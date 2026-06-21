using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using MultiSych.Services.Configuration;
using MultiSych.Services.Implementations;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    public class AIServiceTests
    {
        private Mock<IHttpClientFactory> CreateMockHttpClientFactory(
            Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
        {
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => handlerFunc(request));

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(_ => _.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(mockHandler.Object, disposeHandler: false));

            return mockFactory;
        }

        [Fact]
        public async Task SendMessageAsync_Hybrid_GeminiConfiguredAndWorking_ShouldUseGemini()
        {
            // Arrange
            var config = new MultiSychConfig
            {
                AI = new AISettings
                {
                    GeminiApiKey = "gemini_key_123"
                }
            };

            var factory = CreateMockHttpClientFactory(request =>
            {
                Assert.Contains("generativelanguage.googleapis.com", request.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"candidates\": [ { \"content\": { \"parts\": [ { \"text\": \"Gemini Response\" } ] } } ] }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var service = new AIService(factory.Object, config);

            // Act
            var response = await service.SendMessageAsync(
                new List<ChatHistoryMessage> { new() { Role = "user", Content = "Hello" } },
                "hybrid"
            );

            // Assert
            Assert.Equal("Gemini Response", response);
        }

        [Fact]
        public async Task SendMessageAsync_Hybrid_GeminiFails_OpenAIConfiguredAndWorking_ShouldFallbackToOpenAI()
        {
            // Arrange
            var config = new MultiSychConfig
            {
                AI = new AISettings
                {
                    GeminiApiKey = "gemini_key_123",
                    CopilotApiKey = "openai_key_456"
                }
            };

            var factory = CreateMockHttpClientFactory(request =>
            {
                var uri = request.RequestUri!.ToString();
                if (uri.Contains("generativelanguage.googleapis.com"))
                {
                    // Fail Gemini
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                Assert.Contains("api.openai.com", uri);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"choices\": [ { \"message\": { \"content\": \"OpenAI Response\" } } ] }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var service = new AIService(factory.Object, config);

            // Act
            var response = await service.SendMessageAsync(
                new List<ChatHistoryMessage> { new() { Role = "user", Content = "Hello" } },
                "hybrid"
            );

            // Assert
            Assert.Equal("OpenAI Response", response);
        }

        [Fact]
        public async Task SendMessageAsync_Hybrid_GeminiAndOpenAIFail_YandexConfiguredAndWorking_ShouldFallbackToYandex()
        {
            // Arrange
            var config = new MultiSychConfig
            {
                AI = new AISettings
                {
                    GeminiApiKey = "gemini_key_123",
                    CopilotApiKey = "openai_key_456",
                    YandexAiApiKey = "yandex_key_789"
                }
            };

            var factory = CreateMockHttpClientFactory(request =>
            {
                var uri = request.RequestUri!.ToString();
                if (uri.Contains("generativelanguage.googleapis.com") || uri.Contains("api.openai.com"))
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                Assert.Contains("llm.api.cloud.yandex.net", uri);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"result\": { \"alternatives\": [ { \"message\": { \"text\": \"Yandex Response\" } } ] } }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var service = new AIService(factory.Object, config);

            // Act
            var response = await service.SendMessageAsync(
                new List<ChatHistoryMessage> { new() { Role = "user", Content = "Hello" } },
                "hybrid"
            );

            // Assert
            Assert.Equal("Yandex Response", response);
        }

        [Fact]
        public async Task SendMessageAsync_Hybrid_NoProvidersConfigured_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var config = new MultiSychConfig
            {
                AI = new AISettings() // No keys
            };

            var factory = new Mock<IHttpClientFactory>();
            var service = new AIService(factory.Object, config);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(
                    new List<ChatHistoryMessage> { new() { Role = "user", Content = "Hello" } },
                    "hybrid"
                )
            );
        }
    }
}
