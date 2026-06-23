# MultiSych Smoke Checklist

## Preconditions
- .NET 10 SDK installed (`dotnet --version`)
- Local database migrated
- Optional: `.env` contains non-placeholder OAuth and AI provider settings

## Fast CLI Checks
1. Run help command:
   - `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj help`
2. List accounts:
   - `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj list-accounts`
3. Sync command sanity:
   - `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj sync-all`

## Desktop Startup Checks
1. Start app:
   - `dotnet run --project MultiSych.Desktop/MultiSych.Desktop.csproj`
2. Verify startup logs include:
   - `MultiSych Desktop Application Starting...`
   - `Application started. Press Ctrl+C to shut down.`
3. Confirm app exits cleanly without background-service error spam.

## UI Checks
1. Open `Dashboard` and verify counters/logs render.
2. Open `File Explorer` and test drag-drop file upload path.
3. Open `Document Analyzer` and drag-drop a `.pdf`.
4. Export summary as `.pdf` and `.docx`.
5. Open `Settings` and save at least one AI key entry.

## Packaging Checks
1. Build + test + publish with verifier:
   - `./scripts/verify.sh`
2. Confirm output exists:
   - `MultiSych.Desktop/bin/Release/net10.0/publish/`

## Security Checks
1. Verify vulnerable package scan is clean:
   - `dotnet list MultiSych.Desktop/MultiSych.Desktop.csproj package --vulnerable --include-transitive`
