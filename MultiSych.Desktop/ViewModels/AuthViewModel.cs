using System;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Threading.Tasks;
using ReactiveUI;
using MultiSych.Services.Configuration;
using MultiSych.Services.Interfaces;

namespace MultiSych.Desktop.ViewModels;

public class AuthViewModel : ReactiveObject
{
    private readonly Action<bool> _onAuthComplete;
    private readonly MultiSychConfig _config;
    private readonly ISecureStorageService _secureStorage;
    private string _password = string.Empty;
    private string _twoFactorCode = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _rememberMe;

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string TwoFactorCode
    {
        get => _twoFactorCode;
        set => this.RaiseAndSetIfChanged(ref _twoFactorCode, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => this.RaiseAndSetIfChanged(ref _rememberMe, value);
    }

    public ICommand LoginCommand { get; }

    public AuthViewModel(MultiSychConfig config, ISecureStorageService secureStorage, Action<bool> onAuthComplete)
    {
        _config = config;
        _secureStorage = secureStorage;
        _onAuthComplete = onAuthComplete;
        LoginCommand = ReactiveCommand.CreateFromTask(AuthenticateAsync);
    }

    private async Task AuthenticateAsync()
    {
        bool isSuccess = true;

        if (_config.Security?.RequireStartupPassword == true)
        {
            var storedPassword = Environment.GetEnvironmentVariable("MULTISYCH_STARTUP_PASSWORD") ?? string.Empty;
            if (Password != storedPassword)
            {
                isSuccess = false;
            }
        }

        if (isSuccess && _config.Security?.EnableTwoFactorAuth == true)
        {
            var secret = _config.Security.TwoFactorSecret;
            if (!string.IsNullOrEmpty(secret) && !VerifyTotp(secret, TwoFactorCode))
            {
                isSuccess = false;
            }
        }
        
        if (isSuccess)
        {
            if (RememberMe)
            {
                await _secureStorage.SaveSecretAsync("REMEMBER_ME_UNTIL", DateTime.UtcNow.AddDays(30).ToString("o"));
            }
            _onAuthComplete(true);
        }
        else ErrorMessage = "Geçersiz kimlik bilgileri.";
    }

    private bool VerifyTotp(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            byte[] secret = Base32Decode(secretBase32);
            long timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

            for (int i = -1; i <= 1; i++) // Zaman kaymasına karşı ±1 adım (30 saniye) tolerans
            {
                if (GenerateTotp(secret, timeStep + i) == code) return true;
            }
        }
        catch { }
        return false;
    }

    private string GenerateTotp(byte[] secret, long iterationNumber)
    {
        byte[] counter = BitConverter.GetBytes(iterationNumber);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        using var hmac = new HMACSHA1(secret);
        byte[] hash = hmac.ComputeHash(counter);
        int offset = hash[hash.Length - 1] & 0xf;

        int binary = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) | ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff);
        return (binary % 1000000).ToString("D6");
    }

    private byte[] Base32Decode(string base32)
    {
        base32 = base32.TrimEnd('=').ToUpperInvariant();
        byte[] bytes = new byte[base32.Length * 5 / 8];
        int bitIndex = 0, byteIndex = 0, currentByte = 0;

        foreach (char c in base32)
        {
            int val = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c);
            if (val < 0) continue;

            currentByte = (currentByte << 5) | val;
            bitIndex += 5;

            if (bitIndex >= 8)
            {
                bitIndex -= 8;
                bytes[byteIndex++] = (byte)(currentByte >> bitIndex);
            }
        }
        return bytes;
    }
}