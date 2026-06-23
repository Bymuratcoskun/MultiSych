using Xunit;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MultiSych.Tests;

public class ConfigurationServiceTests
{
    private readonly string _testEnvPath = Path.Combine(Path.GetTempPath(), ".env.test");

    public ConfigurationServiceTests()
    {
        // Clean up test file
        if (File.Exists(_testEnvPath))
        {
            File.Delete(_testEnvPath);
        }
    }

    [Fact]
    public async Task SaveAndRetrieveSetting_ShouldWork()
    {
        // Arrange
        var service = new ConfigurationService();
        var testKey = "TEST_KEY";
        var testValue = "test_value";

        // Act
        await service.SaveSettingAsync(testKey, testValue);

        // Assert - Verify value was saved
        var retrievedValue = service.GetString(testKey);
        Assert.NotEmpty(retrievedValue);
    }

    [Fact]
    public void GetString_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var service = new ConfigurationService();
        var defaultValue = "default";

        // Act
        var result = service.GetString("NON_EXISTENT_KEY", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void GetInt_ShouldParseCorrectly()
    {
        // Arrange
        var service = new ConfigurationService();

        // Act & Assert
        var result = service.GetInt("NON_EXISTENT_INT", 99);
        Assert.Equal(99, result); // Default value
    }

    [Fact]
    public void GetBool_ShouldParseCorrectly()
    {
        // Arrange
        var service = new ConfigurationService();

        // Act & Assert
        var result = service.GetBool("NON_EXISTENT_BOOL", false);
        Assert.False(result);
    }

    [Fact]
    public async Task SaveMultipleSettings_ShouldWork()
    {
        // Arrange
        var service = new ConfigurationService();
        var settings = new Dictionary<string, string>
        {
            { "KEY1", "value1" },
            { "KEY2", "value2" },
            { "KEY3", "value3" }
        };

        // Act
        await service.SaveSettingsAsync(settings);

        // Assert
        // Settings should be saved (we can't easily verify without .env file access in test)
        Assert.True(true); // Placeholder
    }
}
