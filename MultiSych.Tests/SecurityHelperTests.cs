using System;
using System.Security.Cryptography;
using Xunit;
using MultiSych.Services.Security;

namespace MultiSych.Tests
{
    public class SecurityHelperTests
    {
        [Fact]
        public void BuildSqlCipherConnectionString_WithEncryption_ShouldContainPassword()
        {
            // Arrange
            var dbPath = "test.db";
            var password = "SuperSecretPassword123";
            var encrypt = true;

            // Act
            var connectionString = SecurityHelper.BuildSqlCipherConnectionString(dbPath, password, encrypt);

            // Assert
            Assert.Contains($"Password={password}", connectionString);
            Assert.Contains($"Data Source={dbPath}", connectionString);
        }

        [Fact]
        public void BuildSqlCipherConnectionString_WithoutEncryption_ShouldNotContainPassword()
        {
            // Arrange
            var dbPath = "test.db";
            var password = "SuperSecretPassword123";
            var encrypt = false;

            // Act
            var connectionString = SecurityHelper.BuildSqlCipherConnectionString(dbPath, password, encrypt);

            // Assert
            Assert.DoesNotContain("Password=", connectionString);
            Assert.Contains($"Data Source={dbPath}", connectionString);
        }

        [Fact]
        public void BuildSqlCipherConnectionString_EncryptTrueButPasswordMissing_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dbPath = "test.db";
            string? password = null;
            var encrypt = true;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                SecurityHelper.BuildSqlCipherConnectionString(dbPath, password, encrypt)
            );
        }

        [Fact]
        public void Base32Decode_ValidInput_ShouldReturnCorrectBytes()
        {
            // Arrange
            var secret = "MZXW6YTBOI"; // "foobar" in Base32

            // Act
            var decoded = SecurityHelper.Base32Decode(secret);
            var resultString = System.Text.Encoding.ASCII.GetString(decoded);

            // Assert
            Assert.Equal("foobar", resultString);
        }

        [Fact]
        public void Base32Decode_InvalidInput_ShouldThrowFormatException()
        {
            // Arrange
            var invalidSecret = "invalid_character_1";

            // Act & Assert
            Assert.Throws<FormatException>(() => SecurityHelper.Base32Decode(invalidSecret));
        }

        [Fact]
        public void ValidateTotpCode_ValidCode_ShouldReturnTrue()
        {
            // Arrange
            var secret = "MZXW6YTBOI"; // Valid base32 secret
            var decodedSecret = SecurityHelper.Base32Decode(secret);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

            string expectedCode;
            using (var hmac = new HMACSHA1(decodedSecret))
            {
                expectedCode = SecurityHelper.GenerateTotp(hmac, timestamp, 6);
            }

            // Act
            var isValid = SecurityHelper.ValidateTotpCode(secret, expectedCode);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateTotpCode_InvalidCode_ShouldReturnFalse()
        {
            // Arrange
            var secret = "MZXW6YTBOI";
            var invalidCode = "000000";

            // Act
            var isValid = SecurityHelper.ValidateTotpCode(secret, invalidCode);

            // Assert
            Assert.False(isValid);
        }
    }
}
