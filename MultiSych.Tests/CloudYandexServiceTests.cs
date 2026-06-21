using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using Microsoft.Extensions.Logging;
using MultiSych.Services.Implementations;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    public class CloudYandexServiceTests
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
        public async Task GetStorageQuotaAsync_ShouldCallDiskEndpointAndReturnQuota()
        {
            // Arrange
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("cloud-api.yandex.net/v1/disk", request.RequestUri!.ToString());
                Assert.Equal("OAuth test_access_token", request.Headers.Authorization!.ToString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"used_space\": 5000, \"total_space\": 10000 }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var loggerMock = new Mock<ILogger<CloudYandexService>>();
            var token = new OAuthToken { AccessToken = "test_access_token" };
            var service = new CloudYandexService(factoryMock.Object, loggerMock.Object, token);

            // Act
            var quota = await service.GetStorageQuotaAsync();

            // Assert
            Assert.NotNull(quota);
            Assert.Equal(5000, quota.UsedBytes);
            Assert.Equal(10000, quota.TotalBytes);
            Assert.Equal(50, quota.PercentUsed);
        }

        [Fact]
        public async Task ListFilesAsync_ShouldCallResourcesEndpointAndReturnFiles()
        {
            // Arrange
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("cloud-api.yandex.net/v1/disk/resources", request.RequestUri!.ToString());
                Assert.Contains("path=" + Uri.EscapeDataString("/testdir"), request.RequestUri.Query);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"_embedded\": { \"items\": [ " +
                        "{ \"name\": \"file1.txt\", \"type\": \"file\", \"size\": 100, \"modified\": \"2026-06-21T18:00:00Z\" }, " +
                        "{ \"name\": \"subdir\", \"type\": \"dir\", \"size\": 0, \"modified\": \"2026-06-21T18:30:00Z\" } " +
                        "] } }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var loggerMock = new Mock<ILogger<CloudYandexService>>();
            var token = new OAuthToken { AccessToken = "test_access_token" };
            var service = new CloudYandexService(factoryMock.Object, loggerMock.Object, token);

            // Act
            var files = await service.ListFilesAsync("/testdir");

            // Assert
            Assert.NotNull(files);
            Assert.Equal(2, files.Count);

            var file1 = files[0];
            Assert.Equal("file1.txt", file1.Name);
            Assert.Equal("/testdir/file1.txt", file1.Path);
            Assert.Equal(100, file1.Size);
            Assert.False(file1.IsDirectory);

            var subdir = files[1];
            Assert.Equal("subdir", subdir.Name);
            Assert.Equal("/testdir/subdir", subdir.Path);
            Assert.Equal(0, subdir.Size);
            Assert.True(subdir.IsDirectory);
        }

        [Fact]
        public async Task GetMailboxesAsync_ShouldCallMailboxesEndpointAndReturnMailboxes()
        {
            // Arrange
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("mail.yandex.com/api/v1/user/mailboxes", request.RequestUri!.ToString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"mailboxes\": [ " +
                        "{ \"email\": \"user@yandex.com\", \"is_default\": true, \"folder_count\": 5 } " +
                        "] }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var loggerMock = new Mock<ILogger<CloudYandexService>>();
            var token = new OAuthToken { AccessToken = "test_access_token" };
            var service = new CloudYandexService(factoryMock.Object, loggerMock.Object, token);

            // Act
            var mailboxes = await service.GetMailboxesAsync();

            // Assert
            Assert.NotNull(mailboxes);
            Assert.Single(mailboxes);
            Assert.Equal("user@yandex.com", mailboxes[0].Email);
            Assert.True(mailboxes[0].IsDefault);
            Assert.Equal(5, mailboxes[0].FolderCount);
        }

        [Fact]
        public async Task GetRecentEmailsAsync_ShouldCallMessagesEndpointAndReturnEmails()
        {
            // Arrange
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("mail.yandex.com/api/v1/user/folders/1/messages", request.RequestUri!.ToString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"messages\": [ " +
                        "{ \"id\": \"msg_1\", \"from\": \"sender@test.com\", \"subject\": \"Hello Test\", \"date\": \"2026-06-21T18:00:00Z\", \"attachments\": [ { \"id\": \"att_1\" } ] } " +
                        "] }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var loggerMock = new Mock<ILogger<CloudYandexService>>();
            var token = new OAuthToken { AccessToken = "test_access_token" };
            var service = new CloudYandexService(factoryMock.Object, loggerMock.Object, token);

            // Act
            var emails = await service.GetRecentEmailsAsync(10);

            // Assert
            Assert.NotNull(emails);
            Assert.Single(emails);
            Assert.Equal("msg_1", emails[0].Id);
            Assert.Equal("sender@test.com", emails[0].From);
            Assert.Equal("Hello Test", emails[0].Subject);
            Assert.True(emails[0].HasAttachments);
        }

        [Fact]
        public async Task GetCalendarsAsync_ShouldCallCalendarsEndpointAndReturnCalendars()
        {
            // Arrange
            var factoryMock = CreateMockHttpClientFactory(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("cloud-api.yandex.net/v1/calendars", request.RequestUri!.ToString());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{ \"calendars\": [ " +
                        "{ \"id\": \"cal_1\", \"name\": \"My Calendar\", \"description\": \"Personal cal\", \"is_read_only\": false } " +
                        "] }",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    )
                };
            });

            var loggerMock = new Mock<ILogger<CloudYandexService>>();
            var token = new OAuthToken { AccessToken = "test_access_token" };
            var service = new CloudYandexService(factoryMock.Object, loggerMock.Object, token);

            // Act
            var calendars = await service.GetCalendarsAsync();

            // Assert
            Assert.NotNull(calendars);
            Assert.Single(calendars);
            Assert.Equal("cal_1", calendars[0].Id);
            Assert.Equal("My Calendar", calendars[0].Name);
            Assert.Equal("Personal cal", calendars[0].Description);
            Assert.False(calendars[0].IsReadOnly);
        }
    }
}
