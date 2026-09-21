using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class SecureStorageService : ISecureStorageService
    {
        private readonly ILogger _logger;
        public SecureStorageService()
        {
            _logger = Log.ForContext<SecureStorageService>();
        }

        public async Task SaveSecretAsync(string key, string value)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
