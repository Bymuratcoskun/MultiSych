using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

/// <summary>
/// Linux için GitHub Releases API üzerinden güncelleme kontrolü ve .deb/.rpm/AppImage indirme.
/// </summary>
public class LinuxUpdateService : IUpdateService
{
    private readonly ILogger _logger = Log.ForContext<LinuxUpdateService>();
    private readonly IHttpClientFactory _httpClientFactory;
    private const string GithubReleasesApiUrl = "https://api.github.com/repos/Bymuratt/MultiSych/releases/latest";

    public LinuxUpdateService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string CurrentVersion => GetCurrentVersion();

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MultiSych-Updater/1.0");

            var response = await client.GetAsync(GithubReleasesApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("GitHub API returned {Status} for update check.", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var latestTag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            var latestVersion = latestTag.TrimStart('v');
            var releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

            if (!IsNewerVersion(latestVersion, CurrentVersion))
            {
                _logger.Information("Already on latest version {Version}.", CurrentVersion);
                return null;
            }

            var downloadUrl = FindBestAssetUrl(root);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger.Warning("No suitable Linux package found in release {Tag}.", latestTag);
                return null;
            }

            _logger.Information("Update available: {Latest} (current: {Current})", latestVersion, CurrentVersion);
            return new UpdateInfo(latestVersion, releaseNotes, downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check for updates from GitHub.");
            return null;
        }
    }

    public async Task<bool> DownloadAndInstallAsync(UpdateInfo update)
    {
        var ext = Path.GetExtension(new Uri(update.DownloadUrl).AbsolutePath).ToLower();
        var tempFile = Path.Combine(Path.GetTempPath(), $"multisych-update{ext}");

        try
        {
            _logger.Information("Downloading update {Version} from {Url}", update.Version, update.DownloadUrl);
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MultiSych-Updater/1.0");

            using var stream = await client.GetStreamAsync(update.DownloadUrl);
            using var fileStream = File.Create(tempFile);
            await stream.CopyToAsync(fileStream);

            _logger.Information("Download complete. Installing {File}", tempFile);
            return await InstallPackageAsync(tempFile, ext);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to download or install update.");
            return false;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static async Task<bool> InstallPackageAsync(string packagePath, string ext)
    {
        // Paket türüne göre doğru yükleyiciyi seç
        var (installer, args) = ext switch
        {
            ".deb" => ("pkexec", $"apt install -y \"{packagePath}\""),
            ".rpm" => ("pkexec", $"rpm -U \"{packagePath}\""),
            ".appimage" => (null, null), // AppImage: sadece çalıştır
            _ => (null, null)
        };

        if (ext == ".appimage")
        {
            // AppImage'ı çalıştırılabilir yap ve mevcut süreci yeni ile değiştir
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
            Directory.CreateDirectory(appDir);
            var dest = Path.Combine(appDir, "multisych.AppImage");
            File.Copy(packagePath, dest, overwrite: true);
            Process.Start("chmod", $"+x \"{dest}\"")?.WaitForExit();
            return true;
        }

        if (installer == null) return false;

        var psi = new ProcessStartInfo(installer, args!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return false;
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0;
    }

    private static string FindBestAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)) return string.Empty;

        // Tercih sırası: .deb > .rpm > .AppImage
        string deb = string.Empty, rpm = string.Empty, appimage = string.Empty;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var np) ? np.GetString() ?? string.Empty : string.Empty;
            var url = asset.TryGetProperty("browser_download_url", out var up) ? up.GetString() ?? string.Empty : string.Empty;

            if (name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase)) deb = url;
            else if (name.EndsWith(".rpm", StringComparison.OrdinalIgnoreCase)) rpm = url;
            else if (name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)) appimage = url;
        }

        return !string.IsNullOrEmpty(deb) ? deb
             : !string.IsNullOrEmpty(rpm) ? rpm
             : appimage;
    }

    private static string GetCurrentVersion()
    {
        return typeof(LinuxUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestV) || !Version.TryParse(current, out var currentV))
            return false;
        return latestV > currentV;
    }
}
