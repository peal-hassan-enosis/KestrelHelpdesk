# Kestrel Helpdesk

Kestrel Helpdesk is a role-based support ticket management application built with .NET 10, Blazor Interactive Server, ASP.NET Core Identity, Entity Framework Core, and SQL Server. It provides ticket tracking, assignment, comments, attachments, dashboard reporting, Excel import, PDF export, and manager-controlled role assignment.

> **Environment note:** The accounts and passwords documented below are seeded development/demo credentials. Do not use them for production. Before a client or public handover, externalize seed credentials, rotate the defaults, and apply your organization's security policies.

## Contents

- [Business overview](#business-overview)
- [Features and workflows](#features-and-workflows)
- [Roles and permissions](#roles-and-permissions)
- [Application routes](#application-routes)
- [Technical architecture](#technical-architecture)
- [Data and file storage](#data-and-file-storage)
- [Prerequisites](#prerequisites)
- [Run with Visual Studio](#run-with-visual-studio)
- [Run from the CLI](#run-from-the-cli)
- [Run with Docker](#run-with-docker)
- [Seed accounts and onboarding](#seed-accounts-and-onboarding)
- [Optional SQL scripts](#optional-sql-scripts)
- [Build and test](#build-and-test)
- [Playwright E2E tests](#playwright-e2e-tests)
- [Configuration reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)
- [Security and production handover](#security-and-production-handover)

## Business overview

Kestrel Helpdesk centralizes support requests so that ownership, status, communication, and supporting evidence remain together.

Typical workflow:

1. A user creates a ticket with a title, description, category, and priority.
2. The application records the authenticated creator and always creates the ticket with status **Open**.
3. Agents and Managers can coordinate assignment according to their role and the ticket-access rules.
4. Authorized users can add comments and supporting attachments.
5. The ticket board and dashboard expose the information permitted for the signed-in user.
6. Authorized users can import validated Excel workbooks and export the permitted ticket view to PDF.

Authorization is enforced in application services as well as reflected in the UI. Hiding a button or navigation item is not the security boundary.

## Features and workflows

### Ticket lifecycle

- Create, view, and edit support tickets.
- Capture title, description, category, priority, and status during permitted edits.
- Categories: **Hardware**, **Software**, **Network**, **Access**.
- Priorities: **Low**, **Medium**, **High**, **Critical**.
- Statuses: **Open**, **In Progress**, **Resolved**.
- Every newly created ticket is forced to **Open**. The initial status cannot be selected by the creator.
- The authenticated user is recorded as the creator; creator identity is not accepted from form input.
- The board supports server-side paging, sorting, status filtering, and debounced title/description search.
- Ticket details show status, priority, assignee, creation time, comments, and attachments.

### Assignment and workload

- Managers can assign tickets to valid Agents or leave tickets unassigned.
- Agents can assign a ticket they created to themselves or remove their own assignment, subject to service-level authorization.
- Agents can switch between **Assigned to me** and **Created by me** views.
- Employees see tickets they created.
- Managers can view all tickets.
- If a user is changed from Agent to Employee, tickets that are still assigned to that user are treated as unassigned and can be reassigned by a Manager.
- The Manager-only Role Assignment screen can assign **Employee** or **Agent** to registered users. It does not grant or reassign the Manager role.

### Dashboard

- Shows total tickets in the current permitted scope and counts for Open, In Progress, and Resolved.
- Renders a Chart.js status chart using role/scope-filtered ticket data.
- Agents can switch between assigned-ticket and created-by-me scopes.
- Employees see their own created tickets.
- Managers see the helpdesk-wide summary.
- An empty state is shown when the selected scope contains no tickets.

### Comments

- Authorized users can add comments to tickets they can access.
- The comment author comes from the authenticated account and the timestamp is recorded by the server.
- Comments appear chronologically on the ticket details page.
- Ticket-change events can refresh relevant UI views when a ticket changes.

### Attachments

- Up to **3 attachments per ticket**.
- Maximum **10 MB per uploaded file**.
- Supported extensions: `.pdf`, `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, `.txt`, `.csv`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, and `.zip`.
- Image uploads support PNG, JPG/JPEG, GIF, and WebP **still images**. Animated images are rejected.
- The maximum stored image dimension is **1600 pixels on the longest side**. Larger images are resized proportionally; smaller images are not enlarged.
- When an image must be resized, the resized output is stored instead of retaining another copy of the uploaded original. A static GIF that is resized is stored as PNG because the image processor emits a non-animated output.
- Attachment metadata is based on the actual stored output. For example, a resized GIF becomes a `.png` download with `image/png` content type.
- Files are stored in a non-public server-side directory, not under `wwwroot`.
- SQL Server stores attachment metadata including download filename, generated stored filename, content type, stored size, uploader, upload time, and ticket relationship.
- Downloads require authentication and ticket-level authorization.
- Attachment operations validate count, size, extension, image content, and requested changes.

### Excel ticket import

- Accepts `.xlsx` workbooks up to **10 MB** with a maximum of **10,000 data rows**.
- Reads the first worksheet.
- Required columns: `Title`, `Description`, `Priority`, and `Category`.
- Optional column: `Assigned To`, containing Agent email addresses.
- The workbook is validated before confirmation, with validation errors identifying the worksheet row.
- `Status` is rejected because imported tickets are always created as **Open**.
- `CreatedBy` is rejected because the authenticated importing user becomes the creator.
- Assignment information is validated against registered Agents.
- Only Managers can apply assignments during import. Non-Managers can continue without applying assignment data.
- Validated tickets are inserted as a batch.

Example:

~~~text
Title | Description | Priority | Category | Assigned To
VPN access request | Unable to connect to the company VPN. | High | Network | agent@kestrelhelpdesk.com
~~~

The `Assigned To` column is optional. Do not add `Status` or `CreatedBy` columns.

### PDF export and accessibility

- Users can export the tickets on the current board page or all tickets in the current permitted view.
- Exports respect the active ticket scope, filters, and sort order.
- PDF generation is handled by `TicketPdfService`.
- The browser JavaScript module is responsible for triggering the download, while the server endpoint is used for the all-ticket export so large PDF content does not have to pass through a Blazor Base64 payload.
- The PDF service targets **PDF/UA-1** with tagged document structure, document language, descriptive title, title-display preferences, metadata, semantic table headers, and embedded fonts.
- PDF fonts are resolved from Windows system fonts or Linux DejaVu/Liberation font paths. The Docker runtime installs `fonts-dejavu-core`.
- A successful build does not prove PDF/UA compliance. Generated PDFs should be validated with PAC using **PDF/UA-1**, including review of the tag tree and logical reading order.

### Identity and role onboarding

- Users can sign in or register through ASP.NET Core Identity.
- A newly registered user has no application role and is directed to **Role Assignment Pending**.
- Until a Manager assigns Employee or Agent, protected ticket features remain unavailable.
- A Manager opens **Role Assignment**, selects **New Registered User**, and assigns Employee or Agent.
- After a role is assigned, the user should sign out and sign in again so the session receives refreshed role claims.
- The role assignment UI and service do not allow the Manager account to be reassigned or another Manager role to be granted through that screen.

### Responsive shared UI

The application uses a shared visual system for navigation, page headers, forms, grids, cards, alerts, dashboard metrics, and authentication screens. The layout adapts to smaller screens, and navigation entries include icons and active-page states.

## Roles and permissions

| Capability | Employee | Agent | Manager |
|---|---|---|---|
| View tickets | Tickets created by the employee | Assigned tickets and tickets created by the Agent, through the applicable views | All tickets |
| Create tickets | Yes | Yes | Yes |
| Initial ticket status | Always Open | Always Open | Always Open |
| Edit tickets | Tickets the employee can access | Tickets the Agent can access | Tickets the Manager can access; current Manager edit workflow does not change status |
| Change status during edit | Yes | Yes | No, under the current service rule |
| Add comments | Accessible tickets | Accessible tickets | Accessible tickets |
| Add/download permitted attachments | Accessible tickets | Accessible tickets | Accessible tickets |
| Assign tickets | No | Self-assign/unassign tickets the Agent created | Assign/unassign tickets to valid Agents |
| Excel import | Yes, without applying assignments | Yes, without applying assignments | Yes, including validated Agent assignments |
| PDF export | Current permitted view | Current permitted view/scope | Current permitted view, including all tickets |
| Dashboard | Own created tickets | Assigned or created-by-me scope | Helpdesk-wide |
| Assign user roles | No | No | Yes, Employee or Agent only |

Backend services re-check authorization. UI role checks are presentation controls, not the security boundary.

## Application routes

| Route | Purpose | Access |
|---|---|---|
| `/` | Public landing page; signed-in users are redirected to their application area | Public |
| `/Account/Login` | Sign in | Public |
| `/Account/Register` | Register a new account | Public |
| `/pending-role` | Explains that a registered user needs a role | Signed-in users without an application role |
| `/dashboard` | Status metrics and chart | Employee, Agent, Manager |
| `/tickets` | Searchable, sortable ticket board | Employee, Agent, Manager |
| `/tickets/new` | Create ticket | Employee, Agent, Manager |
| `/tickets/{id}` | Ticket details, comments, and attachments | Role and ticket-access rules apply |
| `/tickets/{id}/edit` | Edit ticket | Role and ticket-access rules apply |
| `/tickets/import-excel` | Validate and import tickets from Excel | Employee, Agent, Manager |
| `/role-assignment` | Assign Employee/Agent roles | Manager only |
| `/health` | Application/database health check | Health endpoint |

Attachment downloads use `/tickets/{ticketId}/attachments/{attachmentId}`. The endpoint requires authentication and performs ticket-level authorization.

## Technical architecture

Kestrel Helpdesk is a .NET 10 Blazor Web App using Interactive Server rendering, ASP.NET Core Identity, Entity Framework Core, and SQL Server. The solution separates web presentation, core business behavior, and infrastructure concerns.

### Project structure

~~~text
KestrelHelpdesk/
├── KestrelHelpdesk.slnx
├── KestrelHelpdesk.Core/
│   ├── Authorization/       # Roles, access and attachment policies
│   ├── Constants/           # Shared application limits
│   ├── Events/              # Ticket change events
│   ├── Interfaces/          # Service, repository and storage contracts
│   ├── Models/              # Ticket, comment, attachment and import models
│   └── Services/            # Ticket, query, assignment, comment and import rules
├── KestrelHelpdesk.Infrastructure/
│   ├── Data/                # EF Core ApplicationDbContext
│   ├── FileStorage/         # Local non-public attachment storage
│   ├── Identity/            # Identity user, seeding, directory and role management
│   ├── ImageProcessing/     # Still-image validation and proportional resizing
│   ├── Migrations/          # EF Core migrations and model snapshot
│   └── Repositories/        # SQL Server/EF Core data access
├── KestrelHelpdesk.Web/
│   ├── Components/          # Blazor pages, layouts, account and shared UI
│   ├── Security/            # Web implementation of current-user context
│   ├── Services/            # Browser integration, import, PDF and attachment services
│   ├── wwwroot/             # CSS, JavaScript and static assets
│   ├── Program.cs           # Dependency injection, middleware and HTTP endpoints
│   └── appsettings*.json    # Non-secret application configuration
├── KestrelHelpdesk.Tests/   # xUnit v3 and bUnit unit/component tests
├── KestrelHelpdesk.E2E/     # Playwright browser-level integration tests
├── docs/                    # E2E and development documentation
├── Scripts/                 # Optional SQL scripts
└── Dockerfile
~~~

### Dependency direction

- **Web** owns Blazor components, user interactions, authentication UI, HTTP endpoint wiring, browser JavaScript integration, Excel stream parsing, and PDF download handling.
- **Core** owns business models, role/access policies, application service contracts, and ticket workflow rules. Core does not depend on Razor components or MudBlazor.
- **Infrastructure** implements data repositories, EF Core persistence, Identity operations, image processing, and physical file storage.
- **Tests** exercise selected UI behavior and policy rules through xUnit v3 and bUnit.
- **E2E** drives the real running application through a browser using Playwright and xUnit v3.
- SQL Server is the persistent data store. EF Core migrations define the schema.

### Request and data flow

~~~mermaid
flowchart TD
    Browser[Browser / Blazor UI] --> Web[KestrelHelpdesk.Web]
    Web --> Core[KestrelHelpdesk.Core services and policies]
    Core --> Repo[Repository interfaces]
    Repo --> Infra[KestrelHelpdesk.Infrastructure]
    Infra --> SQL[(SQL Server / EF Core)]
    Web --> Identity[ASP.NET Core Identity]
    Identity --> SQL
    Web --> Storage[Local attachment storage]
    Web --> JS[Browser JavaScript modules]
    E2E[Playwright E2E] --> Browser
~~~

### Main technical components

- **UI:** Blazor Interactive Server, MudBlazor, Bootstrap utilities, shared CSS theme, and Razor components.
- **Authentication/authorization:** ASP.NET Core Identity cookie authentication and role-based authorization for Employee, Agent, and Manager.
- **Persistence:** EF Core 10 and SQL Server, configured through `ApplicationDbContext` and `IDbContextFactory`.
- **Core services:** Ticket CRUD/access checks, query/paging, assignment, comments, and import validation.
- **Infrastructure services:** SQL repositories, Identity seeding and role management, file storage, and SkiaSharp image processing.
- **Excel:** NPOI reads and validates `.xlsx` workbooks.
- **PDF:** iText and the iText PDF/UA add-on generate tagged PDF/UA-1 ticket exports.
- **Charts:** Chart.js renders dashboard status charts in the browser.
- **Browser helpers:** JavaScript modules support chart rendering, PDF downloads, title-field focus, clipboard actions, and local-time formatting.
- **E2E:** Microsoft Playwright for .NET with xUnit v3 and Chromium.
- **Health checks:** `/health` includes a database context check.

## Data and file storage

### SQL Server

The application expects a connection string named **ConnectionStrings:DefaultConnection**. It is intentionally not checked into `appsettings.json`; configure it with User Secrets for local development or environment variables/secret management for Docker and deployment.

EF Core migrations are in `KestrelHelpdesk.Infrastructure/Migrations`. Apply the migration chain to the intended database before first use. At application startup, the Identity seeder creates the three application roles and the default demo accounts if they do not already exist.

### Attachment files

The default storage location is `FileStorage/Attachments` relative to the Web application's content root. `FileStorage:AttachmentsPath` can override it. In Docker, mount a persistent volume at `/app/FileStorage/Attachments` so uploaded files survive container replacement.

**Back up both SQL Server and the attachment directory/volume.** The database contains attachment metadata; physical files are stored separately.

## Prerequisites

For local development:

- .NET 10 SDK.
- A version of Visual Studio that supports .NET 10 and the **ASP.NET and web development** workload, or the .NET CLI.
- SQL Server Developer or another supported SQL Server edition, with permission to create/update a database.
- SQL Server Management Studio (SSMS) is optional but useful for inspecting data and running optional SQL scripts.
- PowerShell for Playwright browser installation when running E2E tests.
- Browser internet connectivity for the Chart.js module currently loaded from jsDelivr.

For Docker:

- Docker Desktop on Windows or a compatible Docker Engine.
- A SQL Server instance reachable from the container over TCP.
- A connection string with valid access to the target database.

## Run with Visual Studio

### 1. Clone and open the solution

Clone the repository, then open `KestrelHelpdesk.slnx` in Visual Studio.

### 2. Configure SQL Server

Ensure SQL Server is running and your development account can create/update the KestrelHelpdesk database.

Example with SQL authentication:

~~~text
Server=localhost;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True
~~~

Example with Windows integrated authentication, where supported by your SQL Server setup:

~~~text
Server=localhost;Database=KestrelHelpdesk;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
~~~

Do not commit a real password.

### 3. Store the connection string in User Secrets

From the repository root:

~~~powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

Alternatively, use **KestrelHelpdesk.Web → Manage User Secrets** in Visual Studio and configure `ConnectionStrings:DefaultConnection`.

### 4. Restore and build

~~~powershell
dotnet restore .\KestrelHelpdesk.slnx
dotnet build .\KestrelHelpdesk.slnx
~~~

### 5. Apply database migrations

If `dotnet ef` is not installed, install the matching EF Core tool once:

~~~powershell
dotnet tool install --global dotnet-ef --version 10.0.12
~~~

Then:

~~~powershell
dotnet ef database update --project .\KestrelHelpdesk.Infrastructure\KestrelHelpdesk.Infrastructure.csproj --startup-project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

Confirm the target database before applying migrations, especially when switching between development and staging databases.

### 6. Run the Web project

Set **KestrelHelpdesk.Web** as the startup project and press **F5** or **Ctrl+F5**. Use the HTTPS URL shown by Visual Studio.

The application startup seeder creates the Employee, Agent, and Manager roles and creates the default demo accounts if they do not already exist.

## Run from the CLI

After configuring User Secrets and applying migrations:

~~~powershell
dotnet run --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

Open the HTTPS/HTTP address printed by the application.

For the HTTP launch profile used by the Playwright E2E suite:

~~~powershell
dotnet run --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj --launch-profile http
~~~

The repository's HTTP development profile uses:

~~~text
http://localhost:5062
~~~

## Run with Docker

The Dockerfile builds and runs the **Web application only**. It does not start SQL Server; SQL Server must be provided separately.

### 1. Prepare the database

Configure the Web project's User Secrets and apply EF Core migrations using the steps above. Ensure SQL Server accepts TCP connections from the container.

### 2. Build the image

~~~powershell
docker build -t kestrelhelpdesk:local -f Dockerfile .
~~~

### 3. Start the container

~~~powershell
docker run --name kestrelhelpdesk `
  -p 8080:8080 `
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" `
  -e "FileStorage__AttachmentsPath=/app/FileStorage/Attachments" `
  -v kestrelhelpdesk-attachments:/app/FileStorage/Attachments `
  kestrelhelpdesk:local
~~~

Open `http://localhost:8080`.

The named Docker volume `kestrelhelpdesk-attachments` persists uploaded files across container replacement. SQL Server backups and this volume should both be included in a backup/restore plan.

To stop and remove the container:

~~~powershell
docker stop kestrelhelpdesk
docker rm kestrelhelpdesk
~~~

The named attachment volume is retained unless explicitly removed.

> **Linux Docker note:** `host.docker.internal` is available by default in Docker Desktop. On other Linux Docker setups, configure a host-gateway mapping or put the app and SQL Server on the same Docker network and use the SQL Server service/container hostname.

## Seed accounts and onboarding

The current Identity seeder defines these demo credentials for a fresh development database:

| Role | Email | Password |
|---|---|---|
| Employee | employee@kestrelhelpdesk.com | Employee123! |
| Agent | agent@kestrelhelpdesk.com | Agent123! |
| Manager | manager@kestrelhelpdesk.com | Manager123! |

**There is currently one seeded Manager account:** `manager@kestrelhelpdesk.com`. The role assignment page does not grant the Manager role or allow this account to be reassigned. It manages Employee and Agent roles only.

### Assign a role to a newly registered user

1. Register a new account.
2. The account is signed in but has no application role, so it is directed to Role Assignment Pending.
3. Sign in as the Manager.
4. Open **Role Assignment** from the sidebar.
5. Select **New Registered User**.
6. Choose **Employee** or **Agent**.
7. Ask the user to sign out and sign in again so the session receives refreshed role claims.

The **Already Assigned** tab can change an existing Employee to Agent or Agent to Employee. The Manager is excluded from this list.

If demo accounts already exist in a persistent database, the seeder does not reset their passwords. The passwords shown above are initial values used when the accounts are first created.

## Optional SQL scripts

Scripts are provided in the `Scripts` directory.

### Topic09Authorization.sql

A compatibility/backfill script for an older development database. A fresh database created from the current EF Core migration chain should not need this historical adjustment script. Review the target schema and take a backup before using it against an existing database.

### Topic13Performance.sql

Adds generated test tickets until the Tickets table contains 10,000 rows. It requires the seeded Employee, Agent, and Manager users to exist first. Run it only against a database where a large test dataset is desired. If the table already contains at least 10,000 records, it inserts nothing.

These scripts are optional and are not required for a clean application startup.

## Build and test

From the repository root, the normal application/unit test commands are:

~~~powershell
dotnet restore .\KestrelHelpdesk.slnx
dotnet build .\KestrelHelpdesk.slnx
dotnet test .\KestrelHelpdesk.Tests\KestrelHelpdesk.Tests.csproj
~~~

The `KestrelHelpdesk.Tests` project uses **xUnit v3** and **bUnit**. It covers selected UI behavior and business/policy rules.

> **Important:** `KestrelHelpdesk.E2E` is a separate browser-level test project and requires a running application plus E2E environment variables. Run it using the [Playwright E2E tests](#playwright-e2e-tests) instructions rather than treating a normal unit-test run as an E2E run.

A successful build or unit-test run does not prove SQL Server connectivity or that every user workflow has been exercised.

## Playwright E2E tests

`KestrelHelpdesk.E2E` is a dedicated browser-level integration test project. It uses **Microsoft Playwright for .NET**, **xUnit v3**, and Chromium against the real running Kestrel Helpdesk application.

The suite currently covers:

- health endpoint;
- login and invalid-login behavior;
- Employee ticket creation and initial Open status;
- Employee ticket editing and status changes;
- ticket comments;
- attachment upload/download;
- Agent ticket creation and self-assignment;
- Employee/Agent/Manager ticket visibility boundaries;
- Employee, Agent, and Manager dashboard behavior;
- Manager-only role assignment access;
- Excel import page availability;
- PDF export download;
- board/details navigation.

The tests generate unique ticket titles and do not depend on a fixed ticket count.

### E2E prerequisites

- .NET 10 SDK.
- SQL Server and a working Kestrel Helpdesk database.
- The application started and reachable at the configured E2E base URL.
- PowerShell (`pwsh`) for the Playwright browser installation script.
- Development/test credentials supplied through environment variables.

### Start the application

Use the HTTP development profile so the default E2E URL is predictable:

~~~powershell
dotnet run --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj --launch-profile http
~~~

The default URL is:

~~~text
http://localhost:5062
~~~

Keep the application running while the E2E tests execute.

### Configure E2E environment variables

The tests deliberately read credentials from environment variables instead of storing passwords in source control.

PowerShell example:

~~~powershell
$env:KESTREL_E2E_BASE_URL = "http://localhost:5062"
$env:KESTREL_E2E_EMPLOYEE_EMAIL = "employee@kestrelhelpdesk.com"
$env:KESTREL_E2E_EMPLOYEE_PASSWORD = "Employee123!"
$env:KESTREL_E2E_AGENT_EMAIL = "agent@kestrelhelpdesk.com"
$env:KESTREL_E2E_AGENT_PASSWORD = "Agent123!"
$env:KESTREL_E2E_MANAGER_EMAIL = "manager@kestrelhelpdesk.com"
$env:KESTREL_E2E_MANAGER_PASSWORD = "Manager123!"
~~~

Use the current development credentials for your environment. Do not commit these environment values or replace the environment-variable approach with passwords in E2E source files.

`KESTREL_E2E_BASE_URL` defaults to `http://localhost:5062`, but setting it explicitly makes the test environment clear.

### Build and install Chromium

~~~powershell
dotnet restore .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
dotnet build .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
pwsh .\KestrelHelpdesk.E2E\bin\Debug\net10.0\playwright.ps1 install chromium
~~~

If the Playwright package is upgraded, rebuild the E2E project and run the browser installation again.

### Run the complete E2E suite

With the application still running:

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
~~~

For detailed output:

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --logger "console;verbosity=detailed"
~~~

A healthy E2E run requires the project to build, Chromium to be installed, the application/database to start correctly, and all discovered E2E tests to pass.

### Run selected E2E tests

Ticket workflow tests:

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
~~~

Role/dashboard tests:

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~RoleAndDashboardTests"
~~~

A single test:

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~Employee_CreatesTicket_AndInitialStatusIsOpen"
~~~

### Use another application URL

~~~powershell
$env:KESTREL_E2E_BASE_URL = "http://localhost:7000"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj
~~~

The application must already be running at that URL and must contain the expected development/test roles and accounts.

### Headed/debug mode

Use Playwright Inspector when diagnosing locators, navigation, or browser interactions:

~~~powershell
$env:PWDEBUG = "1"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
~~~

Clear it afterward:

~~~powershell
Remove-Item Env:PWDEBUG
~~~

### Playwright traces

The E2E test base supports optional tracing:

~~~powershell
$env:KESTREL_E2E_TRACE = "true"
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj --filter "FullyQualifiedName~TicketWorkflowTests"
~~~

Trace ZIP files are written to:

~~~text
TestResults/Traces/
~~~

Disable tracing for normal runs:

~~~powershell
Remove-Item Env:KESTREL_E2E_TRACE
~~~

### Machine-readable E2E results

~~~powershell
dotnet test .\KestrelHelpdesk.E2E\KestrelHelpdesk.E2E.csproj `
  --logger "trx;LogFileName=e2e-results.trx" `
  --results-directory .\TestResults\E2E
~~~

### E2E troubleshooting

**Application connection failure:** verify that the Web project is running, `KESTREL_E2E_BASE_URL` is correct, SQL Server is available, and application startup completed successfully.

**Login failure:** verify the six credential variables, the target database, and the roles created by `IdentitySeeder`. Do not put passwords into test source files.

**Locator failure:** inspect the current UI labels/roles/IDs and prefer stable semantic locators instead of long CSS selectors.

**Timing/flakiness:** do not add `Thread.Sleep` or arbitrary browser delays. Playwright actions and web-first assertions already wait for application state; investigate which real state transition is not being awaited.

**Authorization failure:** verify both the test account's role and the service-level authorization rule. UI visibility is not the only authorization mechanism.

**Database-state failure:** the E2E suite creates unique tickets and does not assume a fixed ticket count. Inspect the failing workflow before introducing shared database assumptions.

For the full E2E workflow guide, see [`docs/E2E-PLAYWRIGHT-GUIDE.md`](docs/E2E-PLAYWRIGHT-GUIDE.md).

## Configuration reference

| Key | Purpose | Example/default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection | Configure with User Secrets or an environment variable; no default is committed |
| `FileStorage:AttachmentsPath` | Directory for uploaded files | `FileStorage/Attachments`; Docker example: `/app/FileStorage/Attachments` |
| `ASPNETCORE_HTTP_PORTS` | Container HTTP listener | `8080` in the Dockerfile |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` for local debugging; production environment when deployed |
| `KESTREL_E2E_BASE_URL` | Base URL used by Playwright | `http://localhost:5062` |
| `KESTREL_E2E_EMPLOYEE_EMAIL` | E2E Employee account | Required by E2E tests |
| `KESTREL_E2E_EMPLOYEE_PASSWORD` | E2E Employee password | Required by E2E tests |
| `KESTREL_E2E_AGENT_EMAIL` | E2E Agent account | Required by E2E tests |
| `KESTREL_E2E_AGENT_PASSWORD` | E2E Agent password | Required by E2E tests |
| `KESTREL_E2E_MANAGER_EMAIL` | E2E Manager account | Required by E2E tests |
| `KESTREL_E2E_MANAGER_PASSWORD` | E2E Manager password | Required by E2E tests |
| `KESTREL_E2E_TRACE` | Enable Playwright tracing | `true` to enable; unset/other values disable |

For nested .NET configuration keys in environment variables, use double underscores. For example, `ConnectionStrings:DefaultConnection` becomes `ConnectionStrings__DefaultConnection`.

## Troubleshooting

### The app cannot connect to SQL Server

- Confirm SQL Server is running and listening on the configured instance/port.
- Verify the connection string is present in User Secrets locally or passed as an environment variable for Docker.
- For Docker Desktop on Windows, use `host.docker.internal,1433` to reach SQL Server on the host; inside a container, `localhost` points to the container.
- Confirm the SQL login has database access and the TLS settings match your local certificate setup.

### Database objects are missing

Apply the current EF Core migrations to the intended database using `dotnet ef database update`. Confirm which migrations and schema changes the database already contains before applying changes to a shared database.

### A registered user cannot access tickets

This is expected until a Manager assigns Employee or Agent. Open **Role Assignment → New Registered User**, assign a role, then sign out and sign in again.

### The dashboard contains no data

Dashboard counts follow the user's role and selected scope. Employees see their own created tickets; Agents see their selected assigned/created view; Managers see all tickets. A clean database has demo users but does not automatically create sample tickets. Create tickets in the UI or optionally run `Scripts/Topic13Performance.sql`.

### Uploaded files disappear after recreating the Docker container

Ensure the named volume `kestrelhelpdesk-attachments` is mounted to `/app/FileStorage/Attachments`. Both attachment metadata and physical files must be retained.

### EF Core reports pending model changes

A pending-model-changes warning means the current EF model and migration snapshot may not be aligned. Review the model and existing migrations first; do not automatically generate or apply extra migrations against a shared/staging database without reviewing the generated migration.

### Visual Studio says a project DLL is locked during rebuild

If an error says that `KestrelHelpdesk.Web` is using `KestrelHelpdesk.Core.dll` or `KestrelHelpdesk.Infrastructure.dll`, stop the running Web/debug process before rebuilding. A running application can keep referenced assemblies locked and cause cascading `Metadata file ... could not be found` errors. The metadata error is often a consequence of the earlier file-lock/build failure rather than the root cause.

## Security and production handover

Before treating the application as production-ready:

- Replace and externalize the seeded demo passwords in `IdentitySeeder`. Prefer secure configuration or a secrets manager and disable/rotate demo accounts in the client environment.
- Use production-grade SQL Server credentials, least-privilege access, backups, TLS, and a controlled migration process.
- Keep connection strings, storage credentials, and test credentials out of source control.
- Protect and back up attachment storage as well as SQL Server.
- Use the Production environment; do not enable detailed development errors for end users.
- Review retention, audit, monitoring, rate limiting, account recovery, password policies, and operational alerting against client requirements.
- Local file storage is used for attachments. For a multi-instance deployment, plan shared durable storage or object storage so every instance can access the same files.
- Chart.js is currently imported from jsDelivr. Deployments with strict outbound network policies should vendor the library or provide an approved static asset source.
- E2E credentials are intended for development/test environments only and must not be reused as production credentials.

This README describes the current implementation and development setup. Production deployment should use environment-specific configuration and undergo a separate security and operational review.
