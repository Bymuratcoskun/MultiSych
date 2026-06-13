using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DotNetEnv;
using Microsoft.Data.Sqlite;
using MultiSych.Services.Configuration;

namespace MultiSych.Services.Security
{
    public static class SecurityHelper
    {
        public static string BuildSqlCipherConnectionString(string databasePath, string? storagePassword, bool encryptDatabase)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            if (encryptDatabase)
            {
                if (string.IsNullOrWhiteSpace(storagePassword))
                {
                    throw new InvalidOperationException("Database encryption is enabled but storage password is missing.");
                }

                builder.Password = storagePassword;
            }

            return builder.ToString();
        }

        public static void LoadEnvironmentFiles(IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                if (!File.Exists(filePath))
                    continue;

                Env.NoClobber().Load(filePath);
            }
        }

        public static void SaveEnvironmentVariable(string key, string value, string envFileName = ".env")
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), envFileName);
            var lines = new List<string>();
            if (File.Exists(filePath))
            {
                lines.AddRange(File.ReadAllLines(filePath));
            }

            var index = lines.FindIndex(l => l.StartsWith($"{key}=", StringComparison.Ordinal));
            var entry = $"{key}={value}";
            if (index >= 0)
            {
                lines[index] = entry;
            }
            else
            {
                lines.Add(entry);
            }

            File.WriteAllLines(filePath, lines);
        }

        public static async Task<bool> EnsureLocalDataConsentAsync(SecuritySettings security)
        {
            if (security.UseLocalOnly)
                return true;

            if (!IsConsoleInteractive())
            {
                // Launched from a desktop environment without a terminal.
                // Assume consent and proceed to show the GUI.
                return true;
            }

            Console.Write("Store personal data locally and keep all account data on this machine? (yes/no): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            return answer == "yes" || answer == "y";
        }

        private static bool IsConsoleInteractive()
        {
            try
            {
                return !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;
            }
            catch
            {
                return false;
            }
        }

        public static bool VerifyStartupSecurity(SecuritySettings security)
        {
            if (!security.RequireStartupPassword && !security.EnableTwoFactorAuth)
                return true;

            if (security.RequireStartupPassword)
            {
                var storedPassword = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(storedPassword))
                {
                    Console.WriteLine("Startup password is configured but not available.");
                    return false;
                }

                Console.Write("Enter startup password: ");
                var entered = ReadPassword();
                Console.WriteLine();
                if (entered != storedPassword)
                {
                    Console.WriteLine("Invalid startup password.");
                    return false;
                }
            }

            if (security.EnableTwoFactorAuth)
            {
                if (string.IsNullOrWhiteSpace(security.TwoFactorSecret))
                {
                    Console.WriteLine("Two-factor authentication is enabled but no secret is configured.");
                    return false;
                }

                Console.Write("Enter 2FA code: ");
                var code = Console.ReadLine()?.Trim() ?? string.Empty;
                if (!ValidateTotpCode(security.TwoFactorSecret, code))
                {
                    Console.WriteLine("Invalid 2FA code.");
                    return false;
                }
            }

            return true;
        }

        public static string ReadPassword()
        {
            var buffer = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    break;

                if (key.Key == ConsoleKey.Backspace && buffer.Length > 0)
                {
                    buffer.Length--;
                    Console.Write("\b \b");
                    continue;
                }

                buffer.Append(key.KeyChar);
                Console.Write("*");
            }

            return buffer.ToString();
        }

        private static bool ValidateTotpCode(string secret, string code, int digits = 6, int timeStepSeconds = 30)
        {
            try
            {
                var normalizedSecret = secret.Replace(" ", string.Empty).ToUpperInvariant();
                var secretBytes = Base32Decode(normalizedSecret);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / timeStepSeconds;

                using var hmac = new HMACSHA1(secretBytes);

                for (var counter = timestamp - 1; counter <= timestamp + 1; counter++)
                {
                    var expected = GenerateTotp(hmac, counter, digits);
                    if (expected == code)
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string GenerateTotp(HMACSHA1 hmac, long counter, int digits)
        {
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            var hash = hmac.ComputeHash(counterBytes);
            var offset = hash[^1] & 0x0F;
            var binaryCode = ((hash[offset] & 0x7F) << 24)
                | ((hash[offset + 1] & 0xFF) << 16)
                | ((hash[offset + 2] & 0xFF) << 8)
                | (hash[offset + 3] & 0xFF);

            var otp = binaryCode % (int)Math.Pow(10, digits);
            return otp.ToString($"D{digits}");
        }

        private static byte[] Base32Decode(string base32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var output = new List<byte>();
            var bits = 0;
            var value = 0;

            foreach (var c in base32)
            {
                if (c == '=')
                    break;

                var index = alphabet.IndexOf(c);
                if (index < 0)
                    throw new FormatException("Invalid Base32 character.");

                value = (value << 5) | index;
                bits += 5;

                if (bits >= 8)
                {
                    bits -= 8;
                    output.Add((byte)((value >> bits) & 0xFF));
                }
            }

            return output.ToArray();
        }

        public static async Task RunSecuritySetupAsync()
        {
            Console.WriteLine("--- Security Setup ---");
            var useLocalOnly = PromptYesNo("Enable local-only storage? (yes/no) [yes]: ", true);
            var encryptStorage = PromptYesNo("Enable storage encryption? (yes/no) [yes]: ", true);

            string? storagePassword = null;
            if (encryptStorage)
            {
                storagePassword = ReadPasswordWithPrompt("Enter a strong storage password: ");
                var confirmStoragePassword = ReadPasswordWithPrompt("Confirm storage password: ");
                if (string.IsNullOrWhiteSpace(storagePassword) || storagePassword != confirmStoragePassword)
                {
                    Console.WriteLine("Storage passwords do not match or are empty. Aborting security setup.");
                    return;
                }
            }

            var requireStartupPassword = PromptYesNo("Require startup password? (yes/no) [yes]: ", true);
            string? startupPassword = null;
            if (requireStartupPassword)
            {
                startupPassword = ReadPasswordWithPrompt("Enter a strong startup password: ");
                var confirmStartupPassword = ReadPasswordWithPrompt("Confirm startup password: ");
                if (string.IsNullOrWhiteSpace(startupPassword) || startupPassword != confirmStartupPassword)
                {
                    Console.WriteLine("Startup passwords do not match or are empty. Aborting security setup.");
                    return;
                }
            }

            var enableTwoFactor = PromptYesNo("Enable TOTP two-factor authentication? (yes/no) [no]: ", false);
            string? twoFactorSecret = null;
            if (enableTwoFactor)
            {
                twoFactorSecret = GenerateBase32Secret(32);
                Console.WriteLine($"Generated TOTP secret: {twoFactorSecret}");
                Console.WriteLine("Add this secret into your authenticator app now.");
            }

            Console.Write("Report folder path [reports]: ");
            var reportFolderInput = Console.ReadLine()?.Trim();
            var reportFolder = string.IsNullOrWhiteSpace(reportFolderInput) ? "reports" : reportFolderInput;

            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "multisych-security.env");
            var lines = new List<string>
            {
                $"MULTISYCH_LOCAL_ONLY={(useLocalOnly ? "true" : "false")}",
                $"MULTISYCH_ENCRYPT_STORAGE={(encryptStorage ? "true" : "false")}",
                $"MULTISYCH_ENABLE_2FA={(enableTwoFactor ? "true" : "false")}",
                $"MULTISYCH_2FA_SECRET={twoFactorSecret ?? string.Empty}",
                $"MULTISYCH_REPORT_FOLDER={reportFolder}"
            };

            if (encryptStorage)
            {
                lines.Insert(2, $"MULTISYCH_STORAGE_PASSWORD={storagePassword}");
            }

            if (requireStartupPassword)
            {
                lines.Insert(2, $"MULTISYCH_STARTUP_PASSWORD={startupPassword}");
            }

            await File.WriteAllLinesAsync(envFilePath, lines);
            SetSecureFilePermissions(envFilePath);

            Console.WriteLine($"Security setup complete. Environment helper file created at: {envFilePath}");
            Console.WriteLine("Load the variables into your shell with: source multisych-security.env");
        }

        private static bool PromptYesNo(string prompt, bool defaultYes)
        {
            Console.Write(prompt);
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(answer))
                return defaultYes;

            return answer.StartsWith("y") || answer == "yes";
        }

        private static string GenerateBase32Secret(int length = 32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var secret = new StringBuilder(length);
            foreach (var b in bytes)
            {
                secret.Append(alphabet[b % alphabet.Length]);
            }

            return secret.ToString();
        }

        private static string ReadPasswordWithPrompt(string prompt)
        {
            Console.Write(prompt);
            var password = ReadPassword();
            Console.WriteLine();
            return password;
        }

        private static void SetSecureFilePermissions(string filePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch
                {
                    // Ignore permission fix failures on unsupported platforms.
                }
            }
        }
    }
}
