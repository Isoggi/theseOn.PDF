# Contributing

Contributions are welcome. For substantial changes, open an issue first to discuss the proposed behavior or design.

## Development setup

1. Install the .NET 8 SDK.
2. Restore and build from the repository root:

   ```powershell
   dotnet restore .\theseOn.PDF.sln
   dotnet build .\theseOn.PDF.sln
   ```

3. Run the API locally:

   ```powershell
   dotnet run --project .\theseOn.PDF\theseOn.PDF.csproj
   ```

   PuppeteerSharp downloads its Chromium build on first use, so the first PDF request requires network access.

## Pull requests

- Keep changes focused and follow the existing ASP.NET Core and C# patterns.
- Include or update HTTP examples when changing the API contract.
- Build the solution before submitting:

  ```powershell
  dotnet build .\theseOn.PDF.sln
  ```

- Describe the change, how it was tested, and any configuration or security implications in the pull request.
- Do not include credentials, personal data, generated build output, or downloaded browser binaries.
