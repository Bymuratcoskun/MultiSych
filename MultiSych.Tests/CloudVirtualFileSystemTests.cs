#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DokanNet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using MultiSych.Services.Data;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    public class CloudVirtualFileSystemTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<LocalCacheDbContext> _dbContextOptions;
        private readonly Mock<IDbContextFactory<LocalCacheDbContext>> _dbContextFactoryMock;
        private readonly Mock<IStorageService> _storageServiceMock;

        public CloudVirtualFileSystemTests()
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
        public void CreateFile_RootDirectory_ReturnsSuccess()
        {
            // Arrange
            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act
            var status = fs.CreateFile("\\", DokanNet.FileAccess.ReadData, FileShare.ReadWrite, FileMode.Open, FileOptions.None, FileAttributes.Directory, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
            fileInfoMock.VerifySet(i => i.IsDirectory = true, Times.Once);
        }

        [Fact]
        public void CreateFile_ExistingFile_ReturnsSuccessAndSetsContext()
        {
            // Arrange
            var file = new CloudFileEntity
            {
                AccountId = "acc_123",
                FileId = "file_abc",
                FileName = "test.txt",
                Path = "/test.txt",
                FileSize = 1234,
                IsDirectory = false
            };
            SeedDatabase(new List<CloudFileEntity> { file });

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act
            var status = fs.CreateFile("\\test.txt", DokanNet.FileAccess.ReadData, FileShare.ReadWrite, FileMode.Open, FileOptions.None, FileAttributes.Normal, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
            fileInfoMock.VerifySet(i => i.Context = It.Is<object>(c => c != null), Times.Once);
        }

        [Fact]
        public void CreateFile_NonExistentFile_OpenMode_ReturnsFileNotFound()
        {
            // Arrange
            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act
            var status = fs.CreateFile("\\nonexistent.txt", DokanNet.FileAccess.ReadData, FileShare.ReadWrite, FileMode.Open, FileOptions.None, FileAttributes.Normal, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.FileNotFound, status);
        }

        [Fact]
        public void GetFileInformation_ExistingFile_ReturnsSuccessAndFileInfo()
        {
            // Arrange
            var file = new CloudFileEntity
            {
                AccountId = "acc_123",
                FileId = "file_abc",
                FileName = "test.txt",
                Path = "/test.txt",
                FileSize = 1234,
                IsDirectory = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            SeedDatabase(new List<CloudFileEntity> { file });

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act
            var status = fs.GetFileInformation("\\test.txt", out var fileInfo, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
            Assert.Equal("test.txt", fileInfo.FileName);
            Assert.Equal(1234, fileInfo.Length);
            Assert.False(fileInfo.Attributes.HasFlag(FileAttributes.Directory));
        }

        [Fact]
        public void FindFiles_ReturnsFilesInDirectory()
        {
            // Arrange
            var filesList = new List<CloudFileEntity>
            {
                new CloudFileEntity { AccountId = "acc_123", FileId = "file_1", FileName = "test1.txt", Path = "/test1.txt", IsDirectory = false, ParentId = null },
                new CloudFileEntity { AccountId = "acc_123", FileId = "file_2", FileName = "test2.txt", Path = "/test2.txt", IsDirectory = false, ParentId = null },
                new CloudFileEntity { AccountId = "acc_123", FileId = "dir_1", FileName = "subfolder", Path = "/subfolder", IsDirectory = true, ParentId = null },
                new CloudFileEntity { AccountId = "acc_123", FileId = "file_3", FileName = "nested.txt", Path = "/subfolder/nested.txt", IsDirectory = false, ParentId = "dir_1" }
            };
            SeedDatabase(filesList);

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act: Find in Root
            var status = fs.FindFiles("\\", out var foundFiles, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
            Assert.Contains(foundFiles, f => f.FileName == "test1.txt");
            Assert.Contains(foundFiles, f => f.FileName == "test2.txt");
            Assert.Contains(foundFiles, f => f.FileName == "subfolder");
            Assert.DoesNotContain(foundFiles, f => f.FileName == "nested.txt");
        }

        [Fact]
        public void GetVolumeInformation_ReturnsCorrectMetadata()
        {
            // Arrange
            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();

            // Act
            var status = fs.GetVolumeInformation(out var volumeLabel, out var features, out var fileSystemName, out var maximumComponentLength, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
            Assert.Equal("MultiSych Drive", volumeLabel);
            Assert.Equal("NTFS", fileSystemName);
            Assert.Equal(256u, maximumComponentLength);
        }

        [Fact]
        public void Cleanup_ModifiedFile_UploadsToCloudAndUpdatesCache()
        {
            // Arrange
            // 1. Seed account
            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                context.Accounts.Add(new MultiSych.Services.Data.AccountCredentialEntity
                {
                    AccountId = "acc_123",
                    Email = "test@yandex.com",
                    Provider = "Yandex",
                    AccessToken = "token_123",
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                });
                context.SaveChanges();
            }

            // 2. Seed temporary cloud file
            var file = new CloudFileEntity
            {
                AccountId = "acc_123",
                FileId = "temp_file_123",
                FileName = "newfile.txt",
                Path = "/newfile.txt",
                FileSize = 0,
                IsDirectory = false
            };
            SeedDatabase(new List<CloudFileEntity> { file });

            // Create a temp file to simulate modified file content
            var localTempFile = Path.GetTempFileName();
            File.WriteAllText(localTempFile, "Hello World from Virtual Drive!");

            // Mock storage upload to return a real cloud file ID
            _storageServiceMock.Setup(s => s.UploadFileAsync(
                It.IsAny<AccountCredentials>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).ReturnsAsync("real_cloud_file_id");

            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();
            
            // Set up context
            var fileContext = new CloudVirtualFileSystem.FileContext 
            { 
                FileId = "temp_file_123", 
                LocalTempPath = localTempFile, 
                IsModified = true 
            };
            fileInfoMock.Setup(i => i.Context).Returns(fileContext);

            // Act
            fs.Cleanup("\\newfile.txt", fileInfoMock.Object);

            // Assert
            // 1. Verify storage upload was called
            _storageServiceMock.Verify(s => s.UploadFileAsync(
                It.Is<AccountCredentials>(c => c.AccountId == "acc_123"),
                localTempFile,
                "root"
            ), Times.Once);

            // 2. Verify local temp file was deleted
            Assert.False(File.Exists(localTempFile));

            // 3. Verify local cache database was updated with the real file ID and file size
            using (var context = new LocalCacheDbContext(_dbContextOptions))
            {
                var cachedFile = context.CloudFiles.FirstOrDefault(f => f.AccountId == "acc_123" && f.Path == "/newfile.txt");
                Assert.NotNull(cachedFile);
                Assert.Equal("real_cloud_file_id", cachedFile.FileId);
                Assert.True(cachedFile.FileSize > 0);
            }
        }

#pragma warning disable CA1416
        [Fact]
        public void GetFileSecurity_RootDirectory_ReturnsSuccess()
        {
            if (!OperatingSystem.IsWindows())
            {
                // Arrange
                var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
                var fileInfoMock = new Mock<IDokanFileInfo>();
                fileInfoMock.Setup(i => i.IsDirectory).Returns(true);

                // Act
                var status = fs.GetFileSecurity("\\", out var security, System.Security.AccessControl.AccessControlSections.All, fileInfoMock.Object);

                // Assert
                Assert.Equal(DokanResult.NotImplemented, status);
                Assert.Null(security);
                return;
            }

            // Arrange (Windows-only)
            {
                var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
                var fileInfoMock = new Mock<IDokanFileInfo>();
                fileInfoMock.Setup(i => i.IsDirectory).Returns(true);

                // Act
                var status = fs.GetFileSecurity("\\", out var security, System.Security.AccessControl.AccessControlSections.All, fileInfoMock.Object);

                // Assert
                Assert.Equal(DokanResult.Success, status);
                Assert.NotNull(security);
                Assert.IsType<System.Security.AccessControl.DirectorySecurity>(security);
            }
        }

        [Fact]
        public void SetFileSecurity_ReturnsSuccess()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Arrange
            var fs = new CloudVirtualFileSystem("acc_123", _storageServiceMock.Object, _dbContextFactoryMock.Object);
            var fileInfoMock = new Mock<IDokanFileInfo>();
            var security = new System.Security.AccessControl.FileSecurity();

            // Act
            var status = fs.SetFileSecurity("\\test.txt", security, System.Security.AccessControl.AccessControlSections.All, fileInfoMock.Object);

            // Assert
            Assert.Equal(DokanResult.Success, status);
        }
#pragma warning restore CA1416
    }
}
#endif
