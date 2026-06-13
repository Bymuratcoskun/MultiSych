using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations
{
    public class ErrorReportService : IErrorReporter
    {
        private readonly ILogger _logger;
        private readonly string _reportFolder;

        public ErrorReportService(string? reportFolder = null)
        {
            _logger = Log.ForContext<ErrorReportService>();
            _reportFolder = reportFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Reports");
            Directory.CreateDirectory(_reportFolder);
            SetSecureDirectoryPermissions(_reportFolder);
        }

        public async Task<string> GenerateReportAsync(Exception exception, string format = "text")
        {
            var report = new
            {
                Title = "Exception Report",
                TimestampUtc = DateTime.UtcNow,
                Severity = exception?.GetType().Name,
                Message = exception?.Message,
                StackTrace = exception?.StackTrace,
                Source = exception?.Source,
                InnerException = exception?.InnerException?.ToString()
            };

            return await WriteReportAsync(report.Title, report, format);
        }

        public async Task<string> GenerateReportAsync(string title, string description, string format = "text")
        {
            var report = new
            {
                Title = title,
                TimestampUtc = DateTime.UtcNow,
                Description = description
            };

            return await WriteReportAsync(title, report, format);
        }

        private async Task<string> WriteReportAsync(string title, object report, string format)
        {
            var sanitizedTitle = SanitizeFileName(title);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var extension = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";
            var fileName = Path.Combine(_reportFolder, $"report-{sanitizedTitle}-{timestamp}.{extension}");

            string output;
            if (extension == "json")
            {
                output = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                output = BuildTextReport(report);
            }

            await File.WriteAllTextAsync(fileName, output);
            SetSecureFilePermissions(fileName);
            _logger.Information("Error report created at {ReportPath}", fileName);
            return fileName;
        }

        private void SetSecureDirectoryPermissions(string directoryPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    File.SetUnixFileMode(directoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch
                {
                    // Ignore permission fix failures on unsupported platforms.
                }
            }
        }

        private void SetSecureFilePermissions(string filePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

        private string BuildTextReport(object report)
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            return json;
        }

        private string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", value.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "report";
            return Regex.Replace(sanitized, "[^a-zA-Z0-9-_]", "_");
        }
    }
}
