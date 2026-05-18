# MultiSych - Copilot Development Instructions

## Project Overview
MultiSych is a cross-platform C# desktop application that synchronizes email, calendar, and cloud storage across Google, Microsoft, and Yandex accounts with hybrid AI support (Copilot, Gemini, Yandex AI).

## Architecture

### Project Structure
- **MultiSych.Core**: Core business logic and data processing
- **MultiSych.Services**: Service layer with authentication, email, calendar, storage, and AI services
- **MultiSych.Desktop**: Console application (will be extended with UI)

### Design Patterns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Service Interfaces**: Clear abstraction for extensibility
- **Async/Await**: Asynchronous operations throughout
- **Hybrid Pattern**: AI service supports multiple providers with fallback

## Development Guidelines

### Service Implementation
When implementing new services:
1. Define interface in `Interfaces/` folder
2. Implement in `Implementations/` folder
3. Register in DI container (Program.cs in Desktop project)
4. Add logging using Serilog

### Adding New API Integrations
1. Add required NuGet packages
2. Create OAuth2/auth implementation
3. Implement service interface methods
4. Add configuration settings in MultiSychConfig

### Logging
Use Serilog for all logging:
```csharp
private readonly ILogger _logger;
_logger = Log.ForContext<YourClass>();
_logger.Information("Message");
_logger.Error(ex, "Error message");
```

## Current Implementation Status

### Completed
- Service interfaces for Email, Calendar, Storage, Authentication, AI
- Models for accounts, emails, events, files
- Base implementations (stubs) for HybridAIService and GoogleAuthenticationService
- Dependency injection setup
- Configuration classes
- Logging setup (Serilog)
- CLI interface for Desktop app

### Next Steps
1. Implement Google OAuth2 authentication flow
2. Implement Email sync service
3. Implement Calendar sync service
4. Implement Storage sync service
5. Integrate with Copilot, Gemini, Yandex AI APIs
6. Create database layer for offline storage
7. Build UI (WPF or Avalonia)

## Testing
When implementing new features:
- Add unit tests in a separate MultiSych.Tests project
- Test each service independently
- Mock external API calls

## Build & Run

```bash
# Build solution
dotnet build

# Run Desktop app
cd MultiSych.Desktop
dotnet run

# Run specific project
dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj
```

## NuGet Packages Used
- Google.Apis.* - Google API client libraries
- Microsoft.Graph - Microsoft Graph API
- Azure.Identity - Azure authentication
- RestSharp - HTTP client
- Newtonsoft.Json - JSON serialization
- Serilog - Structured logging
- Microsoft.Extensions.DependencyInjection - DI container

## Configuration
Environment variables required:
- GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET
- MICROSOFT_CLIENT_ID, MICROSOFT_CLIENT_SECRET
- YANDEX_CLIENT_ID, YANDEX_CLIENT_SECRET
- COPILOT_API_KEY
- GEMINI_API_KEY
- YANDEX_AI_API_KEY

## Important Notes
- All API methods should be async
- Use interface-based design for extensibility
- Handle token expiration and refresh automatically
- Log all external API calls and errors
- Support both .NET on Windows and Linux

## Resources
- Google APIs: https://developers.google.com/
- Microsoft Graph: https://graph.microsoft.com/
- Yandex APIs: https://yandex.com/dev/
- Serilog: https://serilog.net/
- Async/Await: https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming
