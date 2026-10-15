# Kestrel Helpdesk Playwright E2E Integration Test Guide

## 1. What this test suite does

`KestrelHelpdesk.E2E` is a dedicated browser-level integration test project for the Kestrel Helpdesk application. It uses Playwright for .NET with xUnit v3 and drives the real running web application through Chromium.

The suite covers the main user journeys and security boundaries discovered in the application:

- health endpoint;
- login and invalid-login behavior;
- Employee ticket creation and initial `Open` status;
- Employee ticket editing and status changes;
- ticket comments;
- attachment upload and download;
- Agent ticket creation and self-assignment;
- Employee/Agent/Manager ticket visibility boundaries;
- Employee, Agent and Manager dashboard behavior;
- Manager-only role assignment access;
- Excel import page availability;
- PDF export download;
- board/details navigation.

The tests intentionally use unique ticket titles and do not assume a fixed ticket count.

## 2. Prerequisites

Install/verify:

- .NET 10 SDK;
- SQL Server and a working Kestrel Helpdesk database;
- PowerShell (`pwsh`) for the Playwright browser installation script;
- the repository dependencies restored successfully.

The application's normal development configuration is documented in the repository README. The application seeds its development roles/users during startup through `IdentitySeeder`.

Do not put passwords or connection strings into the E2E source files or commit them to Git.

## 3. Configure the application

From the repository root, configure the database connection as described by `README.md`/the application's normal user-secrets configuration.

Then start the application with the HTTP development profile:

```powershell
dotnet run --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj --launch-profile http
```

The repository's launch settings define the HTTP URL as:

```text
http://localhost:5062
```

Keep this terminal running while the E2E tests execute.

## 4. Configure E2E environment variables

The test suite reads credentials from environment variables instead of storing passwords in source control.

PowerShell example:

```powershell
$env:KESTREL_E2E_BASE_URL = "http://localhost:5062"
$env:KESTREL_E2E_EMPLOYEE_EMAIL = "<development Employee email from README>"
$env:KESTREL_E2E_EMPLOYEE_PASSWORD = "<development Employee password from README>"
$env:KESTREL_E2E_AGENT_EMAIL = "<development Agent email from README>"
$env:KESTREL_E2E_AGENT_PASSWORD = "<development Agent password from README>"
$env:KESTREL_E2E_MANAGER_EMAIL = "<development Manager email from README>"
$env:KESTREL_E2E_MANAGER_PASSWORD = "<development Manager password from README>"
```

Replace the values with the development/demo credentials already documented by the repository. Do not commit these values.

The base URL defaults to `http://localhost:5062`, but setting it explicitly makes the test environment obvious.

## 5. Restore and build

From the repository root:

```powershell
dotnet restore .\KestrelHelpdesk.slnx
dotnet build .\KestrelHelpdesk.slnx
```

Then build the E2E project:

```powershell
dotnet build .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
```

A successful build only proves the test code compiled. It does not prove that the running application, database and browser environment are working.

## 6. Install Playwright browsers

After building the E2E project, install Chromium using the Playwright script generated in the test project's output directory:

```powershell
pwsh .\KestrelHelpdesk.E2E\bin\Debug\net10.0\playwright.ps1 install chromium
```

If you later upgrade the Playwright package, rebuild the project and run the browser installation again.

Official Playwright .NET installation documentation:
https://playwright.dev/dotnet/docs/intro

## 7. Run the complete E2E suite

With the application still running:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
```

For more detailed test output:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --logger "console;verbosity=detailed"
```

## 8. How to know the application is working

Treat the run as successful when all of the following are true:

1. The E2E project builds without errors.
2. Playwright Chromium is installed successfully.
3. The application starts without database/Identity errors.
4. `dotnet test` reports all discovered E2E tests as passed.
5. There are no failed or errored tests.
6. The important role-specific scenarios pass, not just the login smoke test.
7. Download tests successfully receive the expected files.
8. The application remains reachable after the test run.

A green build by itself is not enough. The E2E result is the evidence that browser actions reached the real application and the expected user-visible state was produced.

## 9. Run a specific test class

Run only ticket workflow tests:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
```

Run only role/dashboard tests:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~RoleAndDashboardTests"
```

Run one test by name:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~Employee_CreatesTicket_AndInitialStatusIsOpen"
```

## 10. Run tests with a different application URL

For another local port or a test/staging environment:

```powershell
$env:KESTREL_E2E_BASE_URL = "http://localhost:7000"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
```

The application must already be running at that URL and must contain the expected test users/roles/data prerequisites.

## 11. Run in headed/debug mode

Playwright can run headed with the Playwright Inspector. In PowerShell:

```powershell
$env:PWDEBUG = "1"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
```

This is useful when a locator, navigation or form interaction is failing and you need to watch the browser.

After debugging, clear the variable for the normal headless run:

```powershell
Remove-Item Env:PWDEBUG
```

Official debugging documentation:
https://playwright.dev/dotnet/docs/running-tests

## 12. Collect Playwright traces

The test base supports optional browser tracing. Enable it with:

```powershell
$env:KESTREL_E2E_TRACE = "true"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
```

Trace ZIP files are written to:

```text
TestResults/Traces/
```

Open a trace with the Playwright Trace Viewer. The trace is useful for inspecting screenshots, DOM snapshots, network activity and browser actions around a failure.

Official Playwright tracing documentation:
https://playwright.dev/dotnet/docs/api/class-tracing

Disable tracing for normal runs when it is not needed:

```powershell
Remove-Item Env:KESTREL_E2E_TRACE
```

## 13. Save a machine-readable test result

Use the .NET test runner's TRX logger:

```powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj `
  --logger "trx;LogFileName=e2e-results.trx" `
  --results-directory .\TestResults\E2E
```

The resulting file can be inspected by Visual Studio, CI systems and other tools that understand TRX.

## 14. Understanding failures

### Application connection failure

Typical symptom: tests fail while navigating to the configured base URL.

Check:

- the Web project is running;
- `KESTREL_E2E_BASE_URL` is correct;
- SQL Server is available;
- the application's connection string is valid;
- the application reached startup without Identity/EF errors.

### Login failure

Check:

- the three E2E email/password environment variables are set;
- the application database is the expected development database;
- `IdentitySeeder` ran successfully;
- the seeded development accounts still have the expected roles.

Do not solve this by putting passwords into test source files.

### Locator failure

Check the failing page in the browser and compare the current UI labels/roles/IDs with the source. The E2E tests intentionally prefer semantic locators. Do not immediately replace a stable locator with a long CSS selector.

### Timing/flakiness failure

Do not add `Thread.Sleep` or arbitrary browser delays. Playwright actions and web-first assertions already wait for the application state. If a test is flaky, determine which actual browser/application state is not being awaited.

### Authorization failure

Confirm the test account's role and the service-level authorization rule. UI visibility is not the only authorization mechanism in this application.

### Database-state failure

The suite creates unique tickets and does not rely on fixed ticket counts. If a test still depends on shared state, inspect the failing workflow before changing the test to use arbitrary database assumptions.

## 15. Test result checklist

Before considering the E2E suite healthy, verify:

- [ ] Application starts successfully.
- [ ] Database connection/EF health check is working.
- [ ] Employee can log in.
- [ ] Agent can log in.
- [ ] Manager can log in.
- [ ] Invalid credentials are rejected.
- [ ] Employee can create a ticket.
- [ ] New tickets start as Open.
- [ ] Employee can edit their ticket.
- [ ] Employee can change status.
- [ ] Employee can comment.
- [ ] Attachment upload/download works.
- [ ] Agent can assign their created ticket to themselves.
- [ ] Agent/Employee visibility boundaries hold.
- [ ] Manager can see the relevant ticket.
- [ ] Employee, Agent and Manager dashboards load with their correct scopes.
- [ ] Manager role assignment page is protected.
- [ ] Excel import page is reachable by an authorized user.
- [ ] PDF export produces a download.
- [ ] Navigation between board and details works.

## 16. Maintenance rule

When application behavior changes, update the E2E tests only after verifying the new behavior in source code. Use `Commands/update-project-knowledge.md` to keep `.ai/project-knowledge/` current.

For a new feature, use `Commands/create-e2e-testing.md` with an explicit scope instead of asking an agent to blindly rewrite the entire E2E suite.

## 17. Important distinction

These are true browser-level integration tests against a running application. They are not substitutes for unit/component tests in `KestrelHelpdesk.Tests`.

Use:

- `KestrelHelpdesk.Tests` for fast unit/component behavior;
- `KestrelHelpdesk.E2E` for real browser/user journeys and cross-layer integration.
