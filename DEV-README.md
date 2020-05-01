# Building the Application

## Dependencies

- We support Windows 7, 8, and 10.
- These projects are built targeting [.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet-core/2.1).
- Packages used by project:
     * cc-cli
          * Newtonsoft.Json
     * cc-cli.Tests
          * FakeItEasy
          * FluentAssertions
          * nunit
          * NUnit3TestAdapter
          * Microsoft.NET.Test.Sdk
     * Analytics Service
          * Microsoft.ApplicationInsights
          * Microsoft.Extensions.Configuration
     * LoggingService
     * OidcAuthService
          * IdentityModel.OidcClient
          * LocalStorage
          * Microsoft.AspNetCore
          * Microsoft.Identity.Client
          * System.IdentityModel.Tokens.Jwt
     * UpdateService
          * Newtonsoft.Json

## Build

1. Clone the repository to your target destination.
2. Populate the necessary fields in both the _appsettings.json_ & _authconfig.json_ files.
      * This can be done with a build/deploy pipeline such as GitHub Actions or manually by editing the files.
3. Use PowerShell to call `dotnet build` from the solution directory.

## Run
      
- Use PowerShell to call `dotnet run` from the solution directory.

## Debug (Visual Sudio Code)

1. Set up the _launch.json_ and _tasks.json_ files.

      * See example files in the repository.
2. Select the launch configuration.
3. Trigger a debug session with `F5` or `Ctrl + Shift + D`.

## Publish

#### .NET Core 2.1

- Get the "dotnet-warp" tool needed to create the single-file .EXE.

      dotnet tool install -g dotnet-warp 
      dotnet warp .\cc-cli\cc-cli.csproj

#### .NET Core 3.1 (Not Currently Being Used)

- .NET Core 3.0 and newer supports single-file publishing.

      dotnet publish .\cc-cli\cc-cli.csproj -f netcoreapp3.1 -r win-x64 -c Release /p:PublishSingleFile=true
