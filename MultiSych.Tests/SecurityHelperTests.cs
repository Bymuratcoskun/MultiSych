using System;
using System.Security.Cryptography;
using Xunit;
using MultiSych.Services.Configuration;
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

        [Fact]
        public void EncryptBytes_DecryptBytes_Roundtrip_ShouldRestoreOriginalContent()
        {
            // Arrange
            var originalText = "This is a premium high-security transparent caching test.";
            var plaintext = System.Text.Encoding.UTF8.GetBytes(originalText);
            var password = "MySuperSecurePassword123!!";

            // Act
            var ciphertext = SecurityHelper.EncryptBytes(plaintext, password);
            var decrypted = SecurityHelper.DecryptBytes(ciphertext, password);
            var decryptedText = System.Text.Encoding.UTF8.GetString(decrypted);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.NotEqual(plaintext, ciphertext);
            Assert.Equal(originalText, decryptedText);
        }

        [Fact]
        public void DecryptBytes_WithWrongPassword_ShouldThrowException()
        {
            // Arrange
            var plaintext = System.Text.Encoding.UTF8.GetBytes("Secret Data");
            var correctPassword = "CorrectPassword123";
            var wrongPassword = "WrongPassword123";

            // Act
            var ciphertext = SecurityHelper.EncryptBytes(plaintext, correctPassword);

            // Assert
            Assert.ThrowsAny<Exception>(() => SecurityHelper.DecryptBytes(ciphertext, wrongPassword));
        }

        // 2026-09-20, FAZ 1 (docs/KARARLAR.md K4): SecurityHelper.ValidatePassword ve
        // ValidateTwoFactorCode Console I/O'suz saf fonksiyonlara ayrıldı ki GTK
        // SecurityGateWindow bunları çağırabilsin. Bu testler özellikle "yapılandırma
        // eksikse sessizce kabul et" regresyonuna karşı — LoginViewModel'in eski hâli
        // saklı parola boşsa HER parolayı kabul ediyordu (bkz. docs/IS-PAKETI-guvenlik-kapisi.md).
        // MULTISYCH_STARTUP_PASSWORD process-global bir ortam değişkeni olduğu için
        // her test kendi değerini set edip finally'de eski hâline döndürüyor.
        [Fact]
        public void ValidatePassword_CorrectPassword_ReturnsTrue()
        {
            var original = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", "DogruParola123");
                var security = new SecuritySettings { RequireStartupPassword = true };

                Assert.True(SecurityHelper.ValidatePassword(security, "DogruParola123"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", original);
            }
        }

        [Fact]
        public void ValidatePassword_WrongPassword_ReturnsFalse()
        {
            var original = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", "DogruParola123");
                var security = new SecuritySettings { RequireStartupPassword = true };

                Assert.False(SecurityHelper.ValidatePassword(security, "YanlisParola"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", original);
            }
        }

        [Fact]
        public void ValidatePassword_NotConfigured_ReturnsFalse_FailClosed()
        {
            // Kritik regresyon testi: saklı parola boş/yapılandırılmamışsa
            // HİÇBİR parola kabul edilmemeli (eski LoginViewModel'in hatası buydu).
            var original = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", null);
                var security = new SecuritySettings { RequireStartupPassword = true };

                Assert.False(SecurityHelper.ValidatePassword(security, string.Empty));
                Assert.False(SecurityHelper.ValidatePassword(security, "herhangi-bir-sey"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD", original);
            }
        }

        [Fact]
        public void ValidateTwoFactorCode_ValidCode_ReturnsTrue()
        {
            var secret = "MZXW6YTBOI";
            var decodedSecret = SecurityHelper.Base32Decode(secret);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

            string expectedCode;
            using (var hmac = new HMACSHA1(decodedSecret))
            {
                expectedCode = SecurityHelper.GenerateTotp(hmac, timestamp, 6);
            }

            var security = new SecuritySettings { EnableTwoFactorAuth = true, TwoFactorSecret = secret };

            Assert.True(SecurityHelper.ValidateTwoFactorCode(security, expectedCode));
        }

        [Fact]
        public void ValidateTwoFactorCode_InvalidCode_ReturnsFalse()
        {
            var security = new SecuritySettings { EnableTwoFactorAuth = true, TwoFactorSecret = "MZXW6YTBOI" };

            Assert.False(SecurityHelper.ValidateTwoFactorCode(security, "000000"));
        }

        [Fact]
        public void ValidateTwoFactorCode_SecretNotConfigured_ReturnsFalse_FailClosed()
        {
            var security = new SecuritySettings { EnableTwoFactorAuth = true, TwoFactorSecret = null };

            Assert.False(SecurityHelper.ValidateTwoFactorCode(security, "123456"));
        }
    }
}
