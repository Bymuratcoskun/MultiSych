using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using MultiSych.Services.Data;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Xunit;

namespace MultiSych.Tests
{
    public class UnifiedSearchServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<LocalCacheDbContext> _options;
        private readonly Mock<IDbContextFactory<LocalCacheDbContext>> _factoryMock;
        private readonly Mock<IAIService> _aiMock;

        public UnifiedSearchServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<LocalCacheDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var ctx = new LocalCacheDbContext(_options))
            {
                ctx.Database.EnsureCreated();
                Seed(ctx);
            }

            _factoryMock = new Mock<IDbContextFactory<LocalCacheDbContext>>();
            _factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new LocalCacheDbContext(_options));

            _aiMock = new Mock<IAIService>();
        }

        private static void Seed(LocalCacheDbContext ctx)
        {
            ctx.CachedEmails.Add(new EmailMessageEntity
            {
                AccountId = "acc_1",
                MessageId = "m1",
                Subject = "Bütçe raporu onayı",
                From = "patron@firma.com",
                Snippet = "Yıllık bütçe raporunu onayınıza sunuyorum.",
                Body = "Merhaba, ekteki bütçe raporunu inceleyip onaylar mısınız?",
                Provider = "google",
                ReceivedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                ReceivedDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)
            });
            ctx.CachedEmails.Add(new EmailMessageEntity
            {
                AccountId = "acc_1",
                MessageId = "m2",
                Subject = "Öğle yemeği",
                From = "arkadas@mail.com",
                Snippet = "Bugün öğle yemeğine çıkalım mı?",
                Body = "Selam, bugün buluşalım mı?",
                Provider = "google",
                ReceivedAt = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc),
                ReceivedDate = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)
            });

            ctx.CloudFiles.Add(new CloudFileEntity
            {
                AccountId = "acc_1",
                FileId = "f1",
                FileName = "Bütçe_2026.xlsx",
                Path = "/Belgeler/Bütçe_2026.xlsx",
                MimeType = "application/vnd.ms-excel",
                IsDirectory = false,
                Provider = "google"
            });
            ctx.CloudFiles.Add(new CloudFileEntity
            {
                AccountId = "acc_1",
                FileId = "d1",
                FileName = "Belgeler",
                Path = "/Belgeler",
                MimeType = "application/vnd.google-apps.folder",
                IsDirectory = true,
                Provider = "google"
            });

            ctx.CachedEvents.Add(new CalendarEventEntity
            {
                AccountId = "acc_1",
                EventId = "e1",
                Title = "Bütçe toplantısı",
                Description = "Q3 bütçe görüşmesi",
                Location = "Toplantı Odası A",
                StartTime = new DateTime(2026, 6, 5, 14, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 6, 5, 15, 0, 0, DateTimeKind.Utc),
                Provider = "google"
            });

            ctx.SaveChanges();
        }

        private UnifiedSearchService CreateSut() => new(_factoryMock.Object, _aiMock.Object);

        [Fact]
        public async Task SearchAsync_FindsAcrossAllSources()
        {
            var sut = CreateSut();
            var results = await sut.SearchAsync("bütçe");

            // Bütçe: 1 e-posta + 1 dosya + 1 etkinlik eşleşmeli (öğle yemeği e-postası hariç).
            Assert.Contains(results, r => r.Type == SearchSourceType.Email && r.Id == "m1");
            Assert.Contains(results, r => r.Type == SearchSourceType.File && r.Id == "f1");
            Assert.Contains(results, r => r.Type == SearchSourceType.CalendarEvent && r.Id == "e1");
            Assert.DoesNotContain(results, r => r.Id == "m2");
        }

        [Fact]
        public async Task SearchAsync_ExcludesDirectories()
        {
            var sut = CreateSut();
            var results = await sut.SearchAsync("Belgeler", SearchScope.Files);

            Assert.DoesNotContain(results, r => r.Id == "d1"); // klasör
        }

        [Fact]
        public async Task SearchAsync_RespectsScope()
        {
            var sut = CreateSut();
            var results = await sut.SearchAsync("bütçe", SearchScope.Emails);

            Assert.All(results, r => Assert.Equal(SearchSourceType.Email, r.Type));
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task SearchAsync_TitleMatchScoresHigherThanBodyMatch()
        {
            var sut = CreateSut();
            // "onayı" konuda (m1) geçiyor; başka e-postada gövdede yok → m1 üstte.
            var results = await sut.SearchAsync("bütçe raporu");
            var email = results.First(r => r.Type == SearchSourceType.Email);
            Assert.Equal("m1", email.Id);
            Assert.True(email.Score > 0);
        }

        [Fact]
        public async Task SearchAsync_EmptyQueryReturnsEmpty()
        {
            var sut = CreateSut();
            Assert.Empty(await sut.SearchAsync("   "));
            Assert.Empty(await sut.SearchAsync("bütçe", SearchScope.None));
        }

        [Fact]
        public async Task AskAsync_WithMatches_CallsAiWithContextAndReturnsSources()
        {
            string? capturedPrompt = null;
            _aiMock.Setup(a => a.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((p, _) => capturedPrompt = p)
                .ReturnsAsync("Bütçe raporu patronunuz tarafından [1] gönderildi.");

            var sut = CreateSut();
            var answer = await sut.AskAsync("bütçe raporu kimden geldi?");

            Assert.Contains("Bütçe raporu", answer.Answer);
            Assert.NotEmpty(answer.Sources);
            Assert.NotNull(capturedPrompt);
            Assert.Contains("KAYNAKLAR:", capturedPrompt);
            Assert.Contains("SORU:", capturedPrompt);
            _aiMock.Verify(a => a.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AskAsync_NoMatches_DoesNotCallAi()
        {
            var sut = CreateSut();
            var answer = await sut.AskAsync("kuantum fiziği simülasyonu xyzabc");

            Assert.Empty(answer.Sources);
            _aiMock.Verify(a => a.GetResponseAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
