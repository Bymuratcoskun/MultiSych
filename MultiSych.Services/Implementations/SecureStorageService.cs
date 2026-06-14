using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class SecureStorageService : ISecureStorageService
    {
        private readonly ILogger _logger;
        private readonly string _windowsSecretsFolder;

        public SecureStorageService()
        {
            _logger = Log.ForContext<SecureStorageService>();
            
            _windowsSecretsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Secrets");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Directory.Exists(_windowsSecretsFolder))
            {
                Directory.CreateDirectory(_windowsSecretsFolder);
            }
        }

        public async Task SaveSecretAsync(string key, string value)
        {
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var encryptedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
                    var filePath = Path.Combine(_windowsSecretsFolder, $"{key}.dat");
                    await File.WriteAllBytesAsync(filePath, encryptedBytes);
                }
#pragma warning restore CA1416
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await RunProcessAsync("security", $"add-generic-password -s \"MultiSych\" -a \"{key}\" -w \"{value}\" -U");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    await RunProcessWithStdinAsync("secret-tool", $"store --label=\"MultiSych Secret\" application MultiSych key \"{key}\"", value);
                }
                
                _logger.Information("Saved secure secret for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save secure secret for key: {Key}", key);
                throw;
            }
        }

        public async Task<string?> GetSecretAsync(string key)
        {
            try
            {
#pragma warning disable CA1416
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var filePath = Path.Combine(_windowsSecretsFolder, $"{key}.dat");
                    if (!File.Exists(filePath)) return null;
                    
                    var encryptedBytes = await File.ReadAllBytesAsync(filePath);
                    var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
#pragma warning restore CA1416
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return await RunProcessAsync("security", $"find-generic-password -s \"MultiSych\" -a \"{key}\" -w");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await RunProcessAsync("secret-tool", $"lookup application MultiSych key \"{key}\"");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to retrieve secure secret for key (it may not exist): {Key}", key);
            }
            return null;
        }

        public async Task DeleteSecretAsync(string key)
        {
            try
            {
#pragma warning disable CA1416
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var filePath = Path.Combine(_windowsSecretsFolder, $"{key}.dat");
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
#pragma warning restore CA1416
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await RunProcessAsync("security", $"delete-generic-password -s \"MultiSych\" -a \"{key}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    await RunProcessAsync("secret-tool", $"clear application MultiSych key \"{key}\"");
                }
                
                _logger.Information("Deleted secure secret for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete secure secret for key: {Key}", key);
            }
        }

        private async Task<string?> RunProcessAsync(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.TrimEnd('\r', '\n') : null;
        }

        private async Task RunProcessWithStdinAsync(string fileName, string arguments, string stdinContent)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return;
            await process.StandardInput.WriteAsync(stdinContent);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
        }
    }
}
