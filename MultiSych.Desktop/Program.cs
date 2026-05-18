using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiSych.Desktop.Services;
using MultiSych.Services.Configuration;
using MultiSych.Services.Data;
using MultiSych.Services.Implementations;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Security;
using Serilog;
using SQLitePCL;

namespace MultiSych.Desktop;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/multisych-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("MultiSych Desktop Application Starting...");
            SecurityHelper.LoadEnvironmentFiles(new[] { "multisych-security.env", ".env" });

            if (args.Length >= 3 && args[0] == "--set-ai-key")
            {
                var provider = args[1].ToLowerInvariant();
                var key = args[2];
                var envKey = provider switch
                {
                    "copilot" => "COPILOT_API_KEY",
                    "gemini" => "GEMINI_API_KEY",
                    "yandex" => "YANDEX_AI_API_KEY",
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    SecurityHelper.SaveEnvironmentVariable(envKey, key);
                    Console.WriteLine($"Saved {envKey} to .env");
                    return 0;
                }

                Console.WriteLine("Unknown provider. Use: copilot|gemini|yandex");
                return 2;
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    if (e.ExceptionObject is Exception ex)
                        Log.Fatal(ex, "Unhandled exception (AppDomain.CurrentDomain)");
                    else
                        Log.Fatal("Unhandled non-exception object: {Obj}", e.ExceptionObject);
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                try
                {
                    Exception exObj = e.Exception as Exception ?? new Exception("UnobservedTaskException: null");

                    if (exObj is AggregateException agg)
                    {
                        foreach (var inner in agg.Flatten().InnerExceptions)
                        {
                            if (inner is TaskCanceledException)
                                Log.Warning(inner, "Ignored TaskCanceledException in UnobservedTaskException");
                            else
                                Log.Error(inner, "Unobserved task exception");
                        }
                    }
                    else
                    {
                        if (exObj is TaskCanceledException tce)
                            Log.Warning(tce, "Ignored TaskCanceledException in UnobservedTaskException");
                        else
                            Log.Error(exObj, "Unobserved task exception");
                    }

                    e.SetObserved();
                }
                catch (Exception ex)
                {
                    try { Log.Error(ex, "Exception while handling UnobservedTaskException"); } catch { }
                }
            };

            Batteries_V2.Init();
            var config = CreateConfiguration();
            var security = config.Security ?? new SecuritySettings();

            var storagePassword = Environment.GetEnvironmentVariable("MULTISYCH_STORAGE_PASSWORD");
            if (security.EncryptStorage && string.IsNullOrWhiteSpace(storagePassword))
            {
                Console.WriteLine("Error: MULTISYCH_STORAGE_PASSWORD must be set when MULTISYCH_ENCRYPT_STORAGE=true.");
                return 1;
            }

            var host = CreateHost(args, config, storagePassword);
            ServiceProvider = host.Services;
            host.StartAsync().GetAwaiter().GetResult();

            if (args.Length > 0)
            {
                var handled = RunCommandLineAsync(args, ServiceProvider, config).GetAwaiter().GetResult();
                if (handled)
                {
                    host.StopAsync().GetAwaiter().GetResult();
                    return 0;
                }
            }

            // Masaüstü uygulamalarında terminalden (Console.ReadLine) girdi beklemek arayüzü dondurur.
            // Bu güvenlik kontrolleri şimdilik atlanıyor. İleride Avalonia UI (Örn: Login penceresi) üzerinden yapılacaktır.

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            host.StopAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (HostAbortedException)
        {
            // EF Core CLI araçları (Migrations) veritabanı ayarlarını okuduktan sonra
            // uygulamanın arayüzünü açmamak için bu hatayı fırlatarak çalışmayı durdurur. Bu beklenen bir durumdur.
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .LogToTrace();

    private static MultiSychConfig CreateConfiguration()
    {
        return new MultiSychConfig
        {
            Google = new GoogleSettings
            {
                ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? string.Empty
            },
            Microsoft = new MicrosoftSettings
            {
                ClientId = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET") ?? string.Empty,
                TenantId = Environment.GetEnvironmentVariable("MICROSOFT_TENANT_ID") ?? string.Empty
            },
            Yandex = new YandexSettings
            {
                ClientId = Environment.GetEnvironmentVariable("YANDEX_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("YANDEX_CLIENT_SECRET") ?? string.Empty
            },
            AI = new AISettings
            {
                CopilotApiKey = Environment.GetEnvironmentVariable("COPILOT_API_KEY") ?? string.Empty,
                GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty,
                YandexAiApiKey = Environment.GetEnvironmentVariable("YANDEX_AI_API_KEY") ?? string.Empty
            },
            Security = new SecuritySettings
            {
                UseLocalOnly = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_LOCAL_ONLY"), "true", StringComparison.OrdinalIgnoreCase),
                RequireStartupPassword = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD")),
                EnableTwoFactorAuth = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_ENABLE_2FA"), "true", StringComparison.OrdinalIgnoreCase),
                TwoFactorSecret = Environment.GetEnvironmentVariable("MULTISYCH_2FA_SECRET"),
                EncryptStorage = string.Equals(Environment.GetEnvironmentVariable("MULTISYCH_ENCRYPT_STORAGE"), "true", StringComparison.OrdinalIgnoreCase),
                ReportFolder = Environment.GetEnvironmentVariable("MULTISYCH_REPORT_FOLDER") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "Reports")
            }
        };
    }

    private static IHost CreateHost(string[] args, MultiSychConfig config, string? storagePassword)
    {
        var databasePath = config.Database?.DatabasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSych", "multisych.db");
        var databaseFolder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseFolder))
        {
            Directory.CreateDirectory(databaseFolder);
        }

        var security = config.Security ?? new SecuritySettings();
        var connectionString = SecurityHelper.BuildSqlCipherConnectionString(databasePath, storagePassword, security.EncryptStorage);

        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // 1. Arka plan senkronizasyonunu, HTTP havuzunu ve Kanal (Signal) servislerini kaydediyoruz
                services.AddMultiSychBackgroundServices();

                services.AddSingleton(config);
                // 2. Uygulama arka plan görevleri (performans) için Factory
                services.AddDbContextFactory<LocalCacheDbContext>(options => options.UseSqlite(connectionString));
                // 3. EF Core Migrations (Tablo oluşturma) terminal araçlarının veritabanını bulabilmesi için standart DbContext
                services.AddDbContext<LocalCacheDbContext>(options => options.UseSqlite(connectionString));
                services.AddSingleton<IAccountStore, AccountStoreService>();
                services.AddSingleton<IAuthenticationService, GoogleAuthenticationService>();
                services.AddSingleton<IPlatformMountProvider, PlatformMountProvider>();
                services.AddSingleton<IVirtualDriveService, VirtualDriveService>();
                services.AddSingleton<IEmailService, CloudEmailService>();
                services.AddSingleton<ICalendarService, CloudCalendarService>();
                services.AddSingleton<IStorageService, CloudStorageService>();
                services.AddSingleton<ISecureStorageService, SecureStorageService>();
                services.AddSingleton<ISpeechService, WhisperSpeechService>();
                services.AddSingleton<IAudioRecordingService, NAudioRecordingService>();
                services.AddSingleton<IErrorReporter>(provider => new ErrorReportService(security.ReportFolder));
                services.AddSingleton<IWindowService, WindowService>();
                // 4. Arayüzün ihtiyaç duyduğu yapay zeka servisini ekliyoruz
                services.AddSingleton<IAIService, HybridAIService>();
            })
            .Build();
    }

    private static async Task<bool> RunCommandLineAsync(string[] args, IServiceProvider services, MultiSychConfig config)
    {
        if (args.Length == 0)
            return false;

        var command = args[0].ToLowerInvariant();
        var authService = services.GetService(typeof(IAuthenticationService)) as IAuthenticationService;
        var accountStore = services.GetService(typeof(IAccountStore)) as IAccountStore;
        var emailService = services.GetService(typeof(IEmailService)) as IEmailService;
        var calendarService = services.GetService(typeof(ICalendarService)) as ICalendarService;
        var storageService = services.GetService(typeof(IStorageService)) as IStorageService;
        var aiService = services.GetService(typeof(IAIService)) as IAIService;

        static void PrintHelp()
        {
            Console.WriteLine("MultiSych CLI commands:");
            Console.WriteLine("  auth-google       Authenticate with a Google account");
            Console.WriteLine("  auth-microsoft    Authenticate with a Microsoft account");
            Console.WriteLine("  auth-yandex       Authenticate with a Yandex account");
            Console.WriteLine("  list-accounts     List saved accounts");
            Console.WriteLine("  sync-all          Trigger a sync cycle for all accounts");
            Console.WriteLine("  ai-chat           Start a simple AI chat session");
            Console.WriteLine("  setup-security    Create or update local security settings");
            Console.WriteLine("  help              Show this help text");
            Console.WriteLine("  --set-ai-key <provider> <key>   Store API key for an AI provider in .env");
        }

        try
        {
            switch (command)
            {
                case "auth-google":
                    if (authService == null || accountStore == null)
                    {
                        Console.WriteLine("Authentication or account store service is not available.");
                        return true;
                    }

                    var google = config.Google;
                    if (google == null || string.IsNullOrWhiteSpace(google.ClientId) || string.IsNullOrWhiteSpace(google.ClientSecret))
                    {
                        Console.WriteLine("Google client credentials are not configured.");
                        return true;
                    }

                    var googleCredentials = await authService.AuthenticateGoogleAsync(google.ClientId, google.ClientSecret, google.RedirectUrl);
                    await accountStore.SaveAccountAsync(googleCredentials);
                    Console.WriteLine($"Google account saved: {googleCredentials.Email}");
                    return true;

                case "auth-microsoft":
                    if (authService == null || accountStore == null)
                    {
                        Console.WriteLine("Authentication or account store service is not available.");
                        return true;
                    }

                    var microsoft = config.Microsoft;
                    if (microsoft == null || string.IsNullOrWhiteSpace(microsoft.ClientId) || string.IsNullOrWhiteSpace(microsoft.ClientSecret))
                    {
                        Console.WriteLine("Microsoft client credentials are not configured.");
                        return true;
                    }

                    var microsoftCredentials = await authService.AuthenticateMicrosoftAsync(microsoft.ClientId, microsoft.ClientSecret, microsoft.RedirectUrl, microsoft.TenantId);
                    await accountStore.SaveAccountAsync(microsoftCredentials);
                    Console.WriteLine($"Microsoft account saved: {microsoftCredentials.Email}");
                    return true;

                case "auth-yandex":
                    if (authService == null || accountStore == null)
                    {
                        Console.WriteLine("Authentication or account store service is not available.");
                        return true;
                    }

                    var yandex = config.Yandex;
                    if (yandex == null || string.IsNullOrWhiteSpace(yandex.ClientId) || string.IsNullOrWhiteSpace(yandex.ClientSecret))
                    {
                        Console.WriteLine("Yandex client credentials are not configured.");
                        return true;
                    }

                    var yandexCredentials = await authService.AuthenticateYandexAsync(yandex.ClientId, yandex.ClientSecret, yandex.RedirectUrl);
                    await accountStore.SaveAccountAsync(yandexCredentials);
                    Console.WriteLine($"Yandex account saved: {yandexCredentials.Email}");
                    return true;

                case "list-accounts":
                    if (accountStore == null)
                    {
                        Console.WriteLine("Account store service is not available.");
                        return true;
                    }

                    var accounts = await accountStore.GetAccountsAsync();
                    if (accounts.Count == 0)
                    {
                        Console.WriteLine("No accounts have been saved yet.");
                        return true;
                    }

                    Console.WriteLine("Connected accounts:");
                    foreach (var account in accounts)
                    {
                        Console.WriteLine($"- {account.Provider}: {account.Email} (Expires: {account.ExpiresAt:O})");
                    }
                    return true;

                case "sync-all":
                    if (accountStore == null || emailService == null || calendarService == null || storageService == null)
                    {
                        Console.WriteLine("Sync services are not fully available.");
                        return true;
                    }

                    var syncAccounts = await accountStore.GetAccountsAsync();
                    if (syncAccounts.Count == 0)
                    {
                        Console.WriteLine("No accounts configured to sync.");
                        return true;
                    }

                    foreach (var account in syncAccounts)
                    {
                        Console.WriteLine($"Syncing account: {account.Provider} - {account.Email}");
                        await SafeSyncAsync(() => emailService.SyncEmailsAsync(account), account.Provider, "emails");
                        await SafeSyncAsync(() => calendarService.SyncEventsAsync(account), account.Provider, "calendar");
                        await SafeSyncAsync(() => storageService.SyncStorageAsync(account), account.Provider, "storage");
                    }

                    Console.WriteLine("Sync completed.");
                    return true;

                case "ai-chat":
                    if (aiService == null)
                    {
                        Console.WriteLine("AI service is not available.");
                        return true;
                    }

                    Console.WriteLine("Starting AI chat session. Type 'exit' to quit.");
                    while (true)
                    {
                        Console.Write("> ");
                        var input = Console.ReadLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                            break;

                        var response = await aiService.SendMessageAsync(input, new List<string>(), config.AI?.DefaultProvider ?? "hybrid");
                        Console.WriteLine(response);
                    }
                    return true;

                case "setup-security":
                    await SecurityHelper.RunSecuritySetupAsync();
                    return true;

                case "help":
                    PrintHelp();
                    return true;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Command failed: {ex.Message}");
            return true;
        }

        static async Task SafeSyncAsync(Func<Task> action, string? provider, string category)
        {
            try
            {
                await action();
                Console.WriteLine($"  {category} sync completed for {provider ?? "unknown"}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {category} sync failed for {provider ?? "unknown"}: {ex.Message}");
            }
        }
    }

    private static async Task<bool> EnsureLocalDataConsentAsync(SecuritySettings security)
    {
        if (security.UseLocalOnly)
            return true;

        Console.Write("Store personal data locally and keep all account data on this machine? (yes/no): ");
        var answer = Console.ReadLine()?.Trim().ToLower();
        return answer == "yes" || answer == "y";
    }

    private static bool VerifyStartupSecurity(SecuritySettings security)
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

    private static string ReadPassword()
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
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                var codeBytes = Encoding.UTF8.GetBytes(code);
                if (CryptographicOperations.FixedTimeEquals(expectedBytes, codeBytes))
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

    private static async Task RunSecuritySetupAsync()
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

    private static string ReadPasswordWithPrompt(string prompt)
    {
        Console.Write(prompt);
        var password = ReadPassword();
        Console.WriteLine();
        return password;
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
