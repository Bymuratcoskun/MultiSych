using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Implementations;
using MultiSych.Services.Models;

namespace MultiSych.Tests
{
    public class VirtualDriveServiceTests
    {
        [Fact]
        public async Task MountDriveAsync_AccountNotFound_ReturnsFalse()
        {
            // Arrange
            var accountStoreMock = new Mock<IAccountStore>();
            accountStoreMock.Setup(s => s.GetAccountAsync("non_existent")).ReturnsAsync((AccountCredentials?)null);

            var mountProviderMock = new Mock<IPlatformMountProvider>();
            var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object);

            // Act
            var result = await service.MountDriveAsync("non_existent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task MountDriveAsync_AlreadyMounted_ReturnsTrueWithoutRemounting()
        {
            // Arrange
            var account = new AccountCredentials { Email = "test@test.com", Provider = "Google" };
            var accountStoreMock = new Mock<IAccountStore>();
            accountStoreMock.Setup(s => s.GetAccountAsync("acc_1")).ReturnsAsync(account);

            var mountProviderMock = new Mock<IPlatformMountProvider>();
            mountProviderMock.Setup(p => p.GetAvailableDriveLetter()).Returns("Z:");
            mountProviderMock.Setup(p => p.MountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object);

            // Mount the first time
            var firstResult = await service.MountDriveAsync("acc_1");
            Assert.True(firstResult);

            // Act: Mount second time
            var secondResult = await service.MountDriveAsync("acc_1");

            // Assert
            Assert.True(secondResult);
            // Verify MountAsync was only called once
            mountProviderMock.Verify(p => p.MountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task MountDriveAsync_MountFails_ReturnsFalse()
        {
            // Arrange
            var account = new AccountCredentials { Email = "test@test.com", Provider = "Google" };
            var accountStoreMock = new Mock<IAccountStore>();
            accountStoreMock.Setup(s => s.GetAccountAsync("acc_1")).ReturnsAsync(account);

            var mountProviderMock = new Mock<IPlatformMountProvider>();
            mountProviderMock.Setup(p => p.GetAvailableDriveLetter()).Returns("Z:");
            mountProviderMock.Setup(p => p.MountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object);

            // Act
            var result = await service.MountDriveAsync("acc_1");

            // Assert
            Assert.False(result);
            Assert.False(await service.IsMountedAsync("acc_1"));
        }

        [Fact]
        public async Task UnmountDriveAsync_NotMounted_ReturnsFalse()
        {
            // Arrange
            var accountStoreMock = new Mock<IAccountStore>();
            var mountProviderMock = new Mock<IPlatformMountProvider>();
            var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object);

            // Act
            var result = await service.UnmountDriveAsync("acc_1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UnmountDriveAsync_Success_ReturnsTrueAndRemovesMount()
        {
            // Arrange
            var account = new AccountCredentials { Email = "test@test.com", Provider = "Google" };
            var accountStoreMock = new Mock<IAccountStore>();
            accountStoreMock.Setup(s => s.GetAccountAsync("acc_1")).ReturnsAsync(account);

            var mountProviderMock = new Mock<IPlatformMountProvider>();
            mountProviderMock.Setup(p => p.GetAvailableDriveLetter()).Returns("Z:");
            mountProviderMock.Setup(p => p.MountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            mountProviderMock.Setup(p => p.UnmountAsync("Z:")).ReturnsAsync(true);

            var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object);
            await service.MountDriveAsync("acc_1");

            // Act
            var result = await service.UnmountDriveAsync("acc_1");

            // Assert
            Assert.True(result);
            Assert.False(await service.IsMountedAsync("acc_1"));
        }

        [Fact]
        public async Task Dispose_ShouldUnmountAllActiveDrives()
        {
            // Arrange
            var account = new AccountCredentials { Email = "test@test.com", Provider = "Google" };
            var accountStoreMock = new Mock<IAccountStore>();
            accountStoreMock.Setup(s => s.GetAccountAsync("acc_1")).ReturnsAsync(account);

            var mountProviderMock = new Mock<IPlatformMountProvider>();
            mountProviderMock.Setup(p => p.GetAvailableDriveLetter()).Returns("Z:");
            mountProviderMock.Setup(p => p.MountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            mountProviderMock.Setup(p => p.UnmountAsync("Z:")).ReturnsAsync(true);

            using (var service = new VirtualDriveService(mountProviderMock.Object, accountStoreMock.Object))
            {
                await service.MountDriveAsync("acc_1");
            } // Dispose called here

            // Assert
            mountProviderMock.Verify(p => p.UnmountAsync("Z:"), Times.Once);
        }
    }
}
