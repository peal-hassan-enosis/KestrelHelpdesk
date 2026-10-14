# Technology Stack

## Verified

| Area | Technology | Evidence |
|---|---|---|
| Runtime | .NET 10 | `KestrelHelpdesk.*.csproj` target `net10.0` |
| Web | ASP.NET Core Blazor Web App | `KestrelHelpdesk.Web.csproj`, `Program.cs`, Razor components |
| Rendering | Interactive Server | `Program.cs` and page `@rendermode InteractiveServer` declarations |
| UI library | MudBlazor 9.11.0 | `KestrelHelpdesk.Web.csproj` |
| ORM/database | EF Core SQL Server 10.0.12 | Web/Infrastructure project files and `ApplicationDbContext` |
| Identity | ASP.NET Core Identity EF Core 10.0.12 | Web/Infrastructure project files and `Program.cs` |
| Excel | NPOI 2.8.1 | `KestrelHelpdesk.Web.csproj` |
| PDF | iText 9.8.0 + Bouncy Castle adapter 9.8.0 | `KestrelHelpdesk.Web.csproj` |
| Image processing | SkiaSharp 4.151.1 | `KestrelHelpdesk.Infrastructure.csproj` |
| Existing test runner | xUnit v3 4.0.2 | `KestrelHelpdesk.Tests.csproj` |
| Existing component testing | bUnit 2.11.3 | `KestrelHelpdesk.Tests.csproj` |
| Test SDK | Microsoft.NET.Test.Sdk 18.10.1 | `KestrelHelpdesk.Tests.csproj` |
| Persistence | SQL Server | `UseSqlServer` in `Program.cs` and EF context |

## Solution projects

`KestrelHelpdesk.slnx` currently contains:

- `KestrelHelpdesk.Core`
- `KestrelHelpdesk.Infrastructure`
- `KestrelHelpdesk.Web`

The existing test project is not listed in the solution file at the current snapshot.

## Application URLs

`KestrelHelpdesk.Web/Properties/launchSettings.json` defines:

- HTTP development URL: `http://localhost:5062`
- HTTPS development URL: `https://localhost:7016` plus HTTP 5062

For local browser automation, HTTP 5062 avoids development certificate issues unless HTTPS behavior itself is under test.

## Browser/E2E tooling

At the current source snapshot before this branch's E2E implementation, Playwright is not referenced by the existing application or test projects. The E2E branch introduces Playwright separately rather than mixing browser dependencies into the existing unit/component test project.
