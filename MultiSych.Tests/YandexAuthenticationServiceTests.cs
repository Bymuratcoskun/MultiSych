using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Implementations;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    public class YandexAuthenticationServiceTests
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

        private Mock<IConfigurationServiceExtended> CreateMockConfig(string clientId, string clientSecret, string redirectUri)
        {
            var mock = new Mock<IConfigurationServiceExtended>();
            mock.Setup(c => c.GetString("YANDEX_CLIENT_ID", "")).Returns(clientId);
            mock.Setup(c => c.GetString("YANDEX_CLIENT_SECRET", "")).Returns(clientSecret);
            mock.Setup(c => c.GetString("YANDEX_REDIRECT_URI", It.IsAny<string>())).Returns(redirectUri);
            return mock;
        }

        [Fact]
        public void GetAuthorizationUrl_ShouldContainConfiguredValues()
        {
            // Arrange
            var configMock = CreateMockConfig("test_client_id", "test_secret", "http://localhost:5000/callback/yandex");
            var factoryMock = new Mock<IHttpClientFactory>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<YandexAuthenticationService>>();
            var service = new YandexAuthenticationService(factoryMock.Object, configMock.Object, loggerMock.Object);

            // Act
            var url = service.GetAuthorizationUrl("test_state");

            // Assert
            Assert.Contains("client_id=test_client_id", url);
            Assert.Contains("redirect_uri=" + Uri.EscapeDataString("http://localhost:5000/callback/yandex"), url);
            Assert.Contains("state=test_state", url);
            Assert.Contains("response_type=code", url);
            Assert.Contains("scope=", url);
        }

        [Fact]
        public async Task GetTokenAsync_ShouldPostToTokenUrlAndReturnParsedToken()
        {
            // Arrange
            var configMock = CreateMockConfig("test_client_id", "test_secret", "http://localhost:5000/callback/yandex");
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://oauth.yandex.com/token", request.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"access_token\": \"abc123token\", \"refresh_token\": \"refresh456\", \"expires_in\": 3600, \"token_type\": \"Bearer\" }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<YandexAuthenticationService>>();
            var service = new YandexAuthenticationService(factoryMock.Object, configMock.Object, loggerMock.Object);

            // Act
            var token = await service.GetTokenAsync("auth_code_123");

            // Assert
            Assert.NotNull(token);
            Assert.Equal("abc123token", token.AccessToken);
            Assert.Equal("refresh456", token.RefreshToken);
            Assert.Equal(3600, token.ExpiresIn);
            Assert.Equal("Bearer", token.TokenType);
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldPostToTokenUrlWithRefreshTokenAndReturnNewToken()
        {
            // Arrange
            var configMock = CreateMockConfig("test_client_id", "test_secret", "http://localhost:5000/callback/yandex");
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://oauth.yandex.com/token", request.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"access_token\": \"new_access_token\", \"refresh_token\": \"new_refresh_token\", \"expires_in\": 7200, \"token_type\": \"Bearer\" }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<YandexAuthenticationService>>();
            var service = new YandexAuthenticationService(factoryMock.Object, configMock.Object, loggerMock.Object);

            var oldToken = new OAuthToken
            {
                AccessToken = "old_access_token",
                RefreshToken = "old_refresh_token"
            };

            // Act
            var token = await service.RefreshTokenAsync(oldToken);

            // Assert
            Assert.NotNull(token);
            Assert.Equal("new_access_token", token.AccessToken);
            Assert.Equal("new_refresh_token", token.RefreshToken);
            Assert.Equal(7200, token.ExpiresIn);
        }

        [Fact]
        public async Task GetUserInfoAsync_ShouldGetFromUserInfoUrlAndReturnUserInfo()
        {
            // Arrange
            var configMock = CreateMockConfig("test_client_id", "test_secret", "http://localhost:5000/callback/yandex");
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("https://login.yandex.ru/info", request.RequestUri!.ToString());
                Assert.Equal("OAuth abc123token", request.Headers.Authorization!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"id\": \"user_id_1\", \"default_email\": \"test@yandex.com\", \"display_name\": \"Yandex User\", \"first_name\": \"Yandex\", \"last_name\": \"User\", \"default_avatar_id\": \"avatar_id_123\" }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<YandexAuthenticationService>>();
            var service = new YandexAuthenticationService(factoryMock.Object, configMock.Object, loggerMock.Object);

            var token = new OAuthToken { AccessToken = "abc123token" };

            // Act
            var userInfo = await service.GetUserInfoAsync(token);

            // Assert
            Assert.NotNull(userInfo);
            Assert.Equal("user_id_1", userInfo.Id);
            Assert.Equal("test@yandex.com", userInfo.Email);
            Assert.Equal("Yandex User", userInfo.Name);
            Assert.Equal("Yandex", userInfo.FirstName);
            Assert.Equal("User", userInfo.LastName);
            Assert.Equal("https://avatars.yandex.net/get-yapic/avatar_id_123/islands-small", userInfo.Picture);
        }
    }
}
