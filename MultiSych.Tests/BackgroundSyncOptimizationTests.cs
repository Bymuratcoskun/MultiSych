#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using MultiSych.Services.Data;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using MultiSych.Services.Configuration;

namespace MultiSych.Tests
{
    public class BackgroundSyncOptimizationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<LocalCacheDbContext> _dbContextOptions;
        private readonly Mock<IDbContextFactory<LocalCacheDbContext>> _dbContextFactoryMock;
        private readonly Mock<IStorageService> _storageServiceMock;

        public BackgroundSyncOptimizationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _dbContextOptions = new DbContextOptionsBuilder<LocalCacheDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                context.Database.EnsureCreated();
            }

            _dbContextFactoryMock = new Mock<IDbContextFactory<LocalCacheDbContext>>();
            _dbContextFactoryMock.Setup(f => f.CreateDbContext())
                .Returns(() => new LocalCacheDbContext(_dbContextOptions));

            _storageServiceMock = new Mock<IStorageService>();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        private void SeedDatabase(List<CloudFileEntity> files)
        {
            using var context = new LocalCacheDbContext(_dbContextOptions);
            context.CloudFiles.AddRange(files);
            context.SaveChanges();
        }

        [Fact]
        public async Task ThrottledStream_ThrottlesReadOperations()
        {
            // Arrange: 100 KB of data
            byte[] data = new byte[100 * 1024];
            new Random().NextBytes(data);
            using var memoryStream = new MemoryStream(data);

            // Limit to 50 KB/sec, which means reading 100 KB should take at least 1.5 - 2 seconds
            using var throttledStream = new ThrottledStream(memoryStream, 50 * 1024);

            byte[] buffer = new byte[8192];
            var startTime = DateTime.UtcNow;

            // Act
            int bytesRead;
            int totalBytesRead = 0;
            while ((bytesRead = await throttledStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;
            }

            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.Equal(data.Length, totalBytesRead);
            // Expected time should be at least 1.5 seconds due to throttle.
            Assert.True(duration.TotalMilliseconds >= 1000, $"Duration was only {duration.TotalMilliseconds} ms, throttling failed.");
        }

        [Fact]
        public void PowerStatusHelper_DoesNotCrash()
        {
            // Act & Assert: Should run without throwing exceptions on any platform
            var isOnBattery = PowerStatusHelper.IsOnBattery();
            var batteryPct = PowerStatusHelper.GetBatteryPercent();

            Assert.True(batteryPct >= 0 && batteryPct <= 100);
        }

        [Fact]
        public void Cleanup_ConflictResolution_ServerWins()
        {
            // Arrange
            var runtimeSettings = new RuntimeSyncSettings
            {
                ConflictResolutionStrategy = "ServerWins"
            };

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object, runtimeSettings);

            // Seed DB with file entity
            var fileTime = DateTime.UtcNow.AddMinutes(-5);
            SeedDatabase(new List<CloudFileEntity>
            {
                new CloudFileEntity
                {
                    AccountId = "acc_123",
                    FileId = "cloud_file_1",
                    FileName = "doc.txt",
                    Path = "/doc.txt",
                    ParentId = "root",
                    MimeType = "text/plain",
                    FileSize = 10,
                    IsDirectory = false,
                    Provider = "Google"
                }
            });

            // Set up DB account
            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                context.Accounts.Add(new MultiSych.Services.Data.AccountCredentialEntity
                {
                    AccountId = "acc_123",
                    Email = "test@example.com",
                    Provider = "Google",
                    AccessToken = "token",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });
                context.SaveChanges();

                // Update UpdatedAt for the file to match fileTime
                var f = context.CloudFiles.First(x => x.FileId == "cloud_file_1");
                context.Entry(f).Property("UpdatedAt").CurrentValue = fileTime;
                context.SaveChanges();
            }

            // Setup Storage Service to return a newer file in the cloud (Conflict!)
            var cloudFile = new CloudFile
            {
                FileId = "cloud_file_1",
                FileName = "doc.txt",
                ModifiedDate = DateTime.UtcNow, // newer than fileTime
                FileSize = 20,
                Provider = "Google"
            };
            _storageServiceMock.Setup(s => s.GetFileAsync(It.IsAny<AccountCredentials>(), "cloud_file_1"))
                .ReturnsAsync(cloudFile);

            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            File.WriteAllText(tempFilePath, "modified content");

            var fileInfoMock = new Mock<IDokanFileInfo>();
            var fileContext = new CloudVirtualFileSystem.FileContext
            {
                FileId = "cloud_file_1",
                LocalTempPath = tempFilePath,
                IsModified = true
            };
            fileInfoMock.SetupGet(i => i.Context).Returns(fileContext);

            // Act
            fs.Cleanup("\\doc.txt", fileInfoMock.Object);

            // Assert: ServerWins means upload is NOT performed
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<AccountCredentials>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            
            // Clean up temp file
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }

        [Fact]
        public void Cleanup_ConflictResolution_KeepBoth()
        {
            // Arrange
            var runtimeSettings = new RuntimeSyncSettings
            {
                ConflictResolutionStrategy = "KeepBoth"
            };

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object, runtimeSettings);

            var fileTime = DateTime.UtcNow.AddMinutes(-5);
            SeedDatabase(new List<CloudFileEntity>
            {
                new CloudFileEntity
                {
                    AccountId = "acc_123",
                    FileId = "cloud_file_1",
                    FileName = "doc.txt",
                    Path = "/doc.txt",
                    ParentId = "root",
                    MimeType = "text/plain",
                    FileSize = 10,
                    IsDirectory = false,
                    Provider = "Google"
                }
            });

            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                context.Accounts.Add(new MultiSych.Services.Data.AccountCredentialEntity
                {
                    AccountId = "acc_123",
                    Email = "test@example.com",
                    Provider = "Google",
                    AccessToken = "token",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });
                context.SaveChanges();

                var f = context.CloudFiles.First(x => x.FileId == "cloud_file_1");
                context.Entry(f).Property("UpdatedAt").CurrentValue = fileTime;
                context.SaveChanges();
            }

            var cloudFile = new CloudFile
            {
                FileId = "cloud_file_1",
                FileName = "doc.txt",
                ModifiedDate = DateTime.UtcNow, // newer than fileTime
                FileSize = 20,
                Provider = "Google"
            };
            _storageServiceMock.Setup(s => s.GetFileAsync(It.IsAny<AccountCredentials>(), "cloud_file_1"))
                .ReturnsAsync(cloudFile);

            // Mock upload to return a new file ID for the conflict copy
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<AccountCredentials>(), It.IsAny<string>(), "root"))
                .ReturnsAsync("conflict_file_id");

            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            File.WriteAllText(tempFilePath, "modified content");

            var fileInfoMock = new Mock<IDokanFileInfo>();
            var fileContext = new CloudVirtualFileSystem.FileContext
            {
                FileId = "cloud_file_1",
                LocalTempPath = tempFilePath,
                IsModified = true
            };
            fileInfoMock.SetupGet(i => i.Context).Returns(fileContext);

            // Act
            fs.Cleanup("\\doc.txt", fileInfoMock.Object);

            // Assert: KeepBoth means we uploaded the conflict copy
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<AccountCredentials>(), It.IsAny<string>(), "root"), Times.Once);

            // Verify a new conflict file entity was added to the DB
            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                var conflictFile = context.CloudFiles.FirstOrDefault(x => x.FileId == "conflict_file_id");
                Assert.NotNull(conflictFile);
                Assert.Contains("Local Conflict", conflictFile.FileName);
                Assert.Contains("Local Conflict", conflictFile.Path);
            }

            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }
}
#endif
