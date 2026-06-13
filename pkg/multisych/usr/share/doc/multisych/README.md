# MultiSych - Multi-Account Cloud Synchronization Platform

MultiSych adalah aplikasi desktop cross-platform yang menghubungkan akun Google, Microsoft, dan Yandex untuk sinkronisasi email, kalender, dan penyimpanan cloud dengan dukungan AI hybrid (Copilot, Gemini, Yandex AI).

## Fitur Utama

### 1. Integrasi Multi-Provider
- **Google**: Gmail, Google Calendar, Google Drive
- **Microsoft**: Outlook, Office 365, OneDrive
- **Yandex**: Yandex Mail, Yandex Calendar, Yandex Disk

### 2. Sinkronisasi Data
- Email dan Attachment Management
- Calendar Events Synchronization
- Cloud Storage Integration
- Virtual Drive Mounting + File Explorer Integration (Windows, placeholder via platform mount provider abstraction)
- Automatic & Manual Sync Options

### 3. AI Hybrid
- **Copilot**: Microsoft Copilot Integration
- **Gemini**: Google Gemini API Integration
- **Yandex AI**: Yandex AI Services Integration
- Multi-turn Conversations
- Email Analysis & Summarization
- Calendar Suggestions from Email
- Dedicated AI Assistant Windows (Standalone chat & settings per provider)
- Provider-specific quick access hotkeys: Ctrl+Shift+C, Ctrl+Shift+G, Ctrl+Shift+Y

### 4. Cross-Platform Support
- Windows (WPF/Console)
- Linux (Console/Avalonia)
- Full .NET 10 Compatibility

## Arsitektur Proyek

```
MultiSych/
├── MultiSych.Core/          # Core business logic
├── MultiSych.Services/      # Service layer
│   ├── Models/              # Data models
│   ├── Interfaces/          # Service contracts
│   ├── Implementations/      # Service implementations
│   └── Configuration/       # App configuration
└── MultiSych.Desktop/       # Desktop UI application
```

## Struktur Services

### Service Interfaces

1. **IAuthenticationService**
   - Google, Microsoft, Yandex OAuth2 authentication
   - Token management and refresh
   - Token expiration checking

2. **IEmailService**
   - Get, send, delete emails
   - Attachment handling
   - Email synchronization
   - Read/unread status management

3. **ICalendarService**
   - Event CRUD operations
   - Date range queries
   - Event synchronization

4. **IStorageService**
   - File listing and search
   - Upload/download/delete operations
   - Directory management
   - Cloud storage synchronization

5. **IAIService**
   - Hybrid AI responses (Copilot/Gemini/Yandex)
   - Multi-turn conversations
   - Email analysis
   - Document summarization
   - Calendar suggestions

## Data Models

### AccountCredentials
```csharp
public class AccountCredentials
{
    public string AccountId { get; set; }
    public string Email { get; set; }
    public string Provider { get; set; } // "Google", "Microsoft", "Yandex"
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> AdditionalProperties { get; set; }
}
```

### EmailMessage, CalendarEvent, CloudFile
- Comprehensive data models for all synchronized entities
- Provider-specific metadata support

## Konfigurasi

### Environment Variables
```bash
# Google Configuration
GOOGLE_CLIENT_ID=your_google_client_id
GOOGLE_CLIENT_SECRET=your_google_client_secret

# Microsoft Configuration
MICROSOFT_CLIENT_ID=your_microsoft_client_id
MICROSOFT_CLIENT_SECRET=your_microsoft_client_secret

# Yandex Configuration
YANDEX_CLIENT_ID=your_yandex_client_id
YANDEX_CLIENT_SECRET=your_yandex_client_secret

# AI Services
COPILOT_API_KEY=your_copilot_api_key
GEMINI_API_KEY=your_gemini_api_key
YANDEX_AI_API_KEY=your_yandex_ai_api_key

# Security Configuration
MULTISYCH_LOCAL_ONLY=true
MULTISYCH_ENCRYPT_STORAGE=true
MULTISYCH_STORAGE_PASSWORD=strong-local-storage-password
MULTISYCH_STARTUP_PASSWORD=strong-startup-password
MULTISYCH_ENABLE_2FA=true
MULTISYCH_2FA_SECRET=your_totp_secret_base32
MULTISYCH_REPORT_FOLDER=/path/to/local/reports
```

> MultiSych uygulaması `multisych-security.env` veya `.env` dosyasını başlangıçta otomatik olarak yükler.
> `setup-security` komutu bu dosyayı oluşturmak ve güvenli ortam değişkenlerini yazmak için kullanılabilir.
> `AI Chat` ayarları panelinde Copilot, Gemini veya Yandex AI anahtarını girip kaydettiğinizde uygulama bu değeri `.env` dosyasına yazacaktır.

### Configuration File
Aplikasi menggunakan `MultiSychConfig` untuk konfigurasyon:
- Google Settings
- Microsoft Settings
- Yandex Settings
- AI Settings
- Database Settings
- Sync Settings

## NuGet Packages

### Authentication & APIs
- `Google.Apis.Auth` - Google API authentication
- `Google.Apis.Gmail.v1` - Gmail API
- `Google.Apis.Calendar.v3` - Google Calendar API
- `Google.Apis.Drive.v3` - Google Drive API
- `Microsoft.Graph` - Microsoft Graph API
- `Azure.Identity` - Azure authentication

### Utilities
- `RestSharp` - HTTP client
- `Newtonsoft.Json` - JSON serialization
- `Serilog` - Structured logging
- `Microsoft.Extensions.DependencyInjection` - Dependency injection

## Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio Code or Visual Studio
- Git

### Installation

#### Option 1: From Source (Development)

1. Clone the repository
```bash
git clone https://github.com/yourusername/MultiSych.git
cd MultiSych
```

2. Restore dependencies
```bash
dotnet restore
```

3. Configure environment variables
```bash
# Create .env file or set environment variables
export GOOGLE_CLIENT_ID=your_id
export GOOGLE_CLIENT_SECRET=your_secret
# ... etc
```

4. Build the solution
```bash
dotnet build
```

5. Run the application
```bash
cd MultiSych.Desktop
dotnet run
```

#### Option 2: Arch Linux/Manjaro Package (Recommended for Users)

For Manjaro KDE and other Arch-based distributions, you can build and install a native package:

1. **Install Dependencies**
```bash
# Install .NET 10 SDK and runtime
sudo pacman -S dotnet-sdk dotnet-runtime

# Install GUI dependencies for Avalonia
sudo pacman -S gtk3 libxss nss atk at-spi2-core cairo gdk-pixbuf2 glib2 pango libx11 libxcomposite libxcursor libxdamage libxext libxfixes libxi libxrandr libxrender libxtst

# Install build tools
sudo pacman -S git base-devel fakeroot
```

2. **Build Package from Local Repository**
```bash
cd /path/to/MultiSych
./build-package.sh
```

> If you are starting from a remote repository, clone your fork or upstream repository first.

3. **Install the Package**
```bash
# Install the created package
sudo pacman -U multisych-1.0.0-1-x86_64.pkg.tar.zst
```

4. **Run MultiSych**
```bash
# From terminal
multisych

# Or find "MultiSych" in your KDE application menu
```

#### Post-Installation Setup

1. **Configure API Keys**
   - Launch MultiSych
   - Go to Settings
   - Add your API keys for Google, Microsoft, Yandex, and AI services

2. **Add Accounts**
   - Use the account management interface to connect your cloud accounts
   - Follow OAuth flows for each provider

3. **Configure Security**
   - Set up encryption passwords
   - Configure 2FA if desired
   - Review privacy settings

### Troubleshooting

#### Common Issues

**Application won't start:**
- Ensure .NET 10 runtime is installed: `dotnet --list-runtimes`
- Check GPU drivers for Avalonia UI issues
- Verify all dependencies are installed

**Authentication fails:**
- Verify API keys are correct
- Check internet connection
- Ensure OAuth redirect URIs are configured in provider consoles

**Package build fails:**
- Ensure all build dependencies are installed
- Check that .NET SDK version matches (10.0)
- Verify git repository access if building from a cloned source

**Virtual drive mounting issues:**
- Currently placeholder implementation
- Windows shell integration requires additional OS-specific extensions
- Linux FUSE integration planned for future releases

#### Logs and Debugging

MultiSych logs to:
- Console output (when run from terminal)
- `~/logs/multisych-.txt` (rolling daily logs)
- Error reports in configured report folder

Enable verbose logging by setting environment variable:
```bash
export MULTISYCH_LOG_LEVEL=Debug
```

## Komando CLI

```
auth-google   - Authenticate with Google account
auth-microsoft - Authenticate with Microsoft account
auth-yandex   - Authenticate with Yandex account
list-accounts - List all connected accounts
sync-all      - Synchronize all data
ai-chat       - Start AI chat session
exit          - Exit application
```

## Development Status

### Completed
- ✅ Project structure setup
- ✅ Service interfaces definition
- ✅ Model classes
- ✅ Dependency Injection setup
- ✅ Logging configuration (Serilog)
- ✅ Basic AI Service framework

### In Progress
- 🔄 Google OAuth2 Implementation
- 🔄 Email synchronization
- 🔄 Calendar synchronization

### TODO
- ⏳ Microsoft Graph integration
- ⏳ Yandex API integration
- ⏳ Copilot API integration
- ⏳ Gemini API integration
- ⏳ Yandex AI integration
- ⏳ Database layer (SQLite/SQL Server)
- ⏳ UI/UX (WPF or Avalonia)
- ⏳ Unit tests
- ⏳ Dedicated Chat UI Windows for each AI provider
- ⏳ Docker containerization
- ⏳ CI/CD pipeline

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## License

This project is licensed under the MIT License - see LICENSE file for details.

## Support

For support, email support@multisych.com or open an issue on GitHub.

---

**MultiSych** - Sinkronisasi mudah, manajemen akun terpadu, produktivitas maksimal.
