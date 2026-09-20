using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using MultiSych.Services.Data;
using MultiSych.Services.Implementations;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    // 2026-09-20, docs/KARARLAR.md K15: SearchFilesAsync artık yerel önbellekte
    // gerçek bir arama yapıyor (önceden koşulsuz NotImplementedException fırlatıyordu).
    // Bu test yalnız "derleniyor" demenin yetmediğini, aramanın GERÇEKTEN filtrelediğini
    // kanıtlıyor.
    public class CloudStorageServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<LocalCacheDbContext> _options;
        private readonly CloudStorageService _service;

        public CloudStorageServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<LocalCacheDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var ctx = new LocalCacheDbContext(_options))
            {
                ctx.Database.EnsureCreated();
                ctx.CloudFiles.AddRange(
                    new CloudFileEntity { AccountId = "acc_1", FileId = "f1", FileName = "Butce_Raporu_2026.xlsx", Path = "/Butce_Raporu_2026.xlsx", Provider = "Google" },
                    new CloudFileEntity { AccountId = "acc_1", FileId = "f2", FileName = "Tatil_Fotograflari.zip", Path = "/Tatil_Fotograflari.zip", Provider = "Google" },
                    new CloudFileEntity { AccountId = "acc_2", FileId = "f3", FileName = "Butce_Taslak.docx", Path = "/Butce_Taslak.docx", Provider = "Microsoft" }
                );
                ctx.SaveChanges();
            }

            var scopeFactory = BuildScopeFactory(_options);
            _service = new CloudStorageService(new Mock<IHttpClientFactory>().Object, scopeFactory);
        }

        private static IServiceScopeFactory BuildScopeFactory(DbContextOptions<LocalCacheDbContext> options)
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => new LocalCacheDbContext(options));
            return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task SearchFilesAsync_MatchingQuery_ReturnsOnlyMatchesForThatAccount()
        {
            var credentials = new AccountCredentials { AccountId = "acc_1", Email = "a@b.com", Provider = "Google" };

            var results = await _service.SearchFilesAsync(credentials, "Butce");

            Assert.Single(results);
            Assert.Equal("Butce_Raporu_2026.xlsx", results[0].FileName);
        }

        [Fact]
        public async Task SearchFilesAsync_NoMatch_ReturnsEmptyList()
        {
            var credentials = new AccountCredentials { AccountId = "acc_1", Email = "a@b.com", Provider = "Google" };

            var results = await _service.SearchFilesAsync(credentials, "hicbirsekildeeslesmeyecekbirmetin");

            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchFilesAsync_EmptyQuery_ReturnsEmptyList()
        {
            var credentials = new AccountCredentials { AccountId = "acc_1", Email = "a@b.com", Provider = "Google" };

            var results = await _service.SearchFilesAsync(credentials, "");

            Assert.Empty(results);
        }
    }
}
