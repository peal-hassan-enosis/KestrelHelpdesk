# Kestrel Helpdesk

Kestrel Helpdesk is a role-based support ticket management application for teams that need a clear way to submit requests, assign ownership, communicate progress, and track resolution. It brings ticket tracking, comments, supporting files, dashboard reporting, Excel import, PDF export, and manager-controlled access into one responsive web application.

> **Environment note:** The accounts and passwords below are seeded development/demo credentials. Do not use them for a production deployment. Before a client or public handover, externalize seed credentials, rotate the defaults, and apply your organization's security policies.

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
- [Configuration reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)
- [Security and production handover](#security-and-production-handover)

## Business overview

Support requests become difficult to manage when details are scattered across email and chat, ownership is unclear, or there is no shared view of progress. Kestrel Helpdesk centralizes that workflow:

1. A team member creates a ticket with a title, description, category, and priority.
2. The application records the authenticated creator and sets the initial status to **Open**.
3. An Agent or Manager can coordinate assignment, while ticket visibility is checked against the signed-in user's role and relationship to the ticket.
4. Users add comments and permitted supporting files so the request's context remains together.
5. The ticket board and dashboard show current status and ownership.
6. Users can import tickets from a validated Excel workbook and export the current or full permitted ticket view to PDF.

The aim is to make status, ownership, and request history easy to understand. Authorization rules are enforced in application services as well as reflected in the UI; hiding a control is not the only security mechanism.

## Features and workflows

### Ticket lifecycle

- Create, view, and edit support tickets.
- Capture title, description, category, and priority.
- Categories: **Hardware**, **Software**, **Network**, **Access**.
- Priorities: **Low**, **Medium**, **High**, **Critical**.
- Statuses: **Open**, **In Progress**, **Resolved**.
- Every ticket is created as **Open**. Users cannot choose another initial status.
- The authenticated user is recorded as the creator; creator identity is not accepted from form input.
- The board supports server-side paging, sorting, status filtering, and debounced title/description search.
- Ticket details show status, priority, assignee, creation time, comments, and attachments.

### Assignment and workload

- Managers can assign tickets to any valid Agent or leave them unassigned.
- Agents can assign tickets they created to themselves or remove their own assignment, subject to service-level authorization.
- Agents can switch between **Assigned to me** and **Created by me** views.
- Employee views are limited to tickets they created.
- Managers can view all tickets.
- The manager-only role assignment screen lets a Manager assign **Employee** or **Agent** to registered users and switch between those two roles for users who already have an application role.

### Dashboard

- Shows total tickets in the current permitted view and counts for Open, In Progress, and Resolved.
- Renders a Chart.js status chart using role/scope-filtered ticket data.
- Agents can switch the dashboard between assigned tickets and tickets they created.
- An empty state is shown when the selected view contains no tickets.
- Employees see a summary for their own created tickets; Managers see the helpdesk-wide summary.

### Comments

- Authorized users can add comments to tickets they can view.
- The comment author is taken from the authenticated account, and the timestamp is recorded by the server.
- Comments appear in chronological order on the ticket details page.
- Ticket-change events can refresh relevant UI views when a ticket's status is changed.

### Attachments

- Up to **3 attachments per ticket**.
- Maximum **10 MB per file**.
- Supported extensions: .pdf, .png, .jpg, .jpeg, .gif, .webp, .txt, .csv, .doc, .docx, .xls, .xlsx, .ppt, .pptx, and .zip.
- Files are stored in a non-public server-side directory, not under wwwroot.
- SQL Server stores metadata such as the original filename, generated stored filename, content type, size, uploader, upload time, and ticket relationship.
- Download requests require authentication and verify ticket access before returning the file.
- The form validates file count, size, extension, and requested attachment changes.

### Excel ticket import

- Accepts .xlsx workbooks up to **10 MB** with a maximum of **10,000 data rows**.
- Reads the first worksheet. Required columns are Title, Description, Priority, and Category. An optional Assigned To column can contain Agent email addresses.
- The workbook is validated before the user confirms import. Validation errors identify the worksheet row.
- A Status column is rejected because imported tickets are always created as **Open**.
- A CreatedBy column is rejected because the authenticated importing user is used as the creator.
- Assignment information is validated against registered Agents. Only Managers can apply assignments during import; non-Managers are warned if assignment data is present and can continue without assignments.
- Validated tickets are inserted as a batch.

A simple workbook header row can look like this:

~~~text
Title | Description | Priority | Category | Assigned To
VPN access request | Unable to connect to the company VPN. | High | Network | agent@kestrelhelpdesk.com
~~~

The Assigned To column is optional. Use the category and priority names listed above. Do not add Status or CreatedBy columns.

### PDF export

- Export the tickets on the current board page or all tickets in the current permitted view.
- Exports use the active ticket scope, filters, and sort order.
- PDF generation is handled by the PDF service, while a JavaScript module triggers the browser download.

### Identity and role onboarding

- Users can sign in or register through ASP.NET Core Identity.
- A newly registered user has no application role and is directed to **Role Assignment Pending**.
- Until a Manager assigns a role, the user cannot use protected ticket features such as the ticket board, dashboard, ticket creation, or Excel import.
- The Manager opens **Role Assignment**, selects **New Registered User**, and assigns Employee or Agent.
- After a role is assigned, the user should sign out and sign in again so the session receives refreshed role claims.
- The role assignment UI and service do not allow the Manager account to be reassigned or another Manager role to be granted through this screen.

### Responsive shared UI

The application uses one shared visual system for navigation, page headers, forms, data grids, cards, alerts, dashboard metrics, and authentication screens. The layout adapts to smaller screens, and navigation entries include icons and active-page states.

## Roles and permissions

| Capability | Employee | Agent | Manager |
|---|---|---|---|
| View tickets | Tickets created by the employee | Assigned tickets and tickets created by the Agent, through the applicable views | All tickets |
| Create tickets | Yes | Yes | Yes |
| Initial ticket status | Always Open | Always Open | Always Open |
| Edit tickets | Tickets the employee can access | Tickets the Agent can access | Tickets the Manager can access; status is not changed through the Manager edit workflow |
| Change status during edit | Yes | Yes | No, under the current service rule |
| Add comments | Tickets the user can access | Tickets the user can access | Tickets the user can access |
| Add/download permitted attachments | Tickets the user can access | Tickets the user can access | Tickets the user can access |
| Assign tickets | No | Can assign a ticket they created to self or unassign it | Can assign/unassign tickets to valid Agents |
| Excel import | Yes, without applying assignments | Yes, without applying assignments | Yes, including validated Agent assignments |
| PDF export | Current permitted view | Current permitted view/scope | Current permitted view, including all tickets |
| Dashboard | Own created tickets | Assigned or created-by-me scope | Helpdesk-wide |
| Assign user roles | No | No | Yes, Employee or Agent only |

Backend services re-check authorization. UI role checks are for presentation and usability, not a replacement for service authorization.

## Application routes

| Route | Purpose | Access |
|---|---|---|
| / | Public landing page; signed-in users are redirected to their application area | Public |
| /Account/Login | Sign in | Public |
| /Account/Register | Register a new account | Public |
| /pending-role | Explains that a registered user needs a role | Signed-in users without an application role |
| /dashboard | Status metrics and chart | Employee, Agent, Manager |
| /tickets | Searchable, sortable ticket board | Employee, Agent, Manager |
| /tickets/new | Create ticket | Employee, Agent, Manager |
| /tickets/{id} | Ticket details, comments, and attachments | Role and ticket-access rules apply |
| /tickets/{id}/edit | Edit ticket | Role and ticket-access rules apply |
| /tickets/import-excel | Validate and import tickets from Excel | Employee, Agent, Manager |
| /role-assignment | Assign Employee/Agent roles | Manager only |
| /health | Application/database health check | Health endpoint |

The attachment download endpoint is /tickets/{ticketId}/attachments/{attachmentId}. It requires authentication and performs ticket-level authorization.

## Technical architecture

Kestrel Helpdesk is a .NET 10 Blazor Web App using Interactive Server rendering, ASP.NET Core Identity, Entity Framework Core, and SQL Server. The solution separates web presentation, core ticket/domain behavior, and infrastructure concerns.

### Project structure

~~~text
KestrelHelpdesk/
├── KestrelHelpdesk.slnx
├── KestrelHelpdesk.Core/
│   ├── Authorization/       # Roles, user context models, access and attachment policies
│   ├── Events/              # Ticket change events
│   ├── Interfaces/          # Service, repository and storage contracts
│   ├── Models/              # Ticket, Comment, Attachment and import models
│   └── Services/            # Ticket, query, assignment, comment and import rules
├── KestrelHelpdesk.Infrastructure/
│   ├── Data/                # EF Core ApplicationDbContext
│   ├── FileStorage/         # Local non-public attachment storage
│   ├── Identity/            # Identity user, seeding, directory and role management
│   ├── Migrations/          # EF Core migrations and model snapshot
│   └── Repositories/        # SQL Server/EF Core data access
├── KestrelHelpdesk.Web/
│   ├── Components/          # Blazor pages, layouts, account and shared UI
│   ├── Security/            # Web implementation of current-user context
│   ├── Services/            # Browser integration, import adapter, PDF and attachment services
│   ├── wwwroot/             # CSS, JavaScript and static assets
│   ├── Program.cs           # Dependency injection, middleware and HTTP endpoints
│   └── appsettings*.json    # Non-secret application configuration
├── KestrelHelpdesk.Tests/   # xUnit v3 and bUnit tests
├── Scripts/                 # Optional SQL scripts for legacy data and performance testing
└── Dockerfile
~~~

### Dependency direction

- **Web** owns Blazor components, user interactions, authentication UI, HTTP endpoint wiring, browser JavaScript integration, Excel stream parsing, and PDF download handling.
- **Core** owns business models, role/access policies, application service contracts, and ticket workflow rules. Core does not depend on Razor components or MudBlazor.
- **Infrastructure** implements data repositories, EF Core persistence, Identity operations, and physical file storage.
- **Tests** exercise selected components and policy behavior through bUnit and xUnit.
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
~~~

### Main technical components

- **UI:** Blazor Interactive Server, MudBlazor, Bootstrap utilities, shared CSS theme, and Razor components.
- **Authentication/authorization:** ASP.NET Core Identity cookie authentication and role-based authorization for Employee, Agent, and Manager.
- **Persistence:** EF Core 10 and SQL Server, configured through ApplicationDbContext and IDbContextFactory.
- **Core services:** Ticket CRUD/access checks, query/paging, assignment, comments, and import validation.
- **Infrastructure services:** SQL repositories, Identity seeding and role management, and file storage.
- **Excel:** NPOI reads and validates .xlsx workbooks.
- **PDF:** iText generates export documents.
- **Charts:** Chart.js renders dashboard status charts in the browser.
- **Browser helpers:** JavaScript modules support chart rendering, PDF downloads, title-field focus, clipboard actions, and local-time formatting.
- **Health checks:** /health includes a database context check.

## Data and file storage

### SQL Server

The application expects a connection string named **ConnectionStrings:DefaultConnection**. It is intentionally not checked into appsettings.json; configure it with User Secrets for local development or environment variables/secret management for Docker and deployment.

EF Core migrations are in KestrelHelpdesk.Infrastructure/Migrations. Apply the migration chain to the intended database before first use. At application startup, the Identity seeder creates the three application roles and creates the default demo accounts if they do not already exist.

### Attachment files

The default storage location is FileStorage/Attachments relative to the Web application's content root. FileStorage:AttachmentsPath can override it. In Docker, mount a persistent volume at /app/FileStorage/Attachments so uploaded files survive container replacement.

**Back up both SQL Server and the attachment directory/volume.** The database contains attachment metadata; physical files are stored separately.

## Prerequisites

For local development:

- .NET 10 SDK.
- A version of Visual Studio that supports .NET 10 and the **ASP.NET and web development** workload, or the .NET CLI.
- SQL Server Developer or another supported SQL Server edition, with permission to create/update a database.
- SQL Server Management Studio (SSMS) is optional but useful for inspecting data and running optional SQL scripts.
- Browser internet connectivity for the Chart.js module currently loaded from jsDelivr.

For Docker:

- Docker Desktop on Windows or a compatible Docker Engine.
- A SQL Server instance reachable from the container over TCP.
- A connection string with valid access to the target database.

## Run with Visual Studio

### 1. Clone and open the solution

Clone the repository, then open KestrelHelpdesk.slnx in Visual Studio.

### 2. Configure SQL Server

Ensure SQL Server is running and your development account can create/update the KestrelHelpdesk database. Use a connection string suitable for the instance.

Example with SQL authentication:

~~~text
Server=localhost;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True
~~~

Example with Windows integrated authentication, where supported by your SQL Server setup:

~~~text
Server=localhost;Database=KestrelHelpdesk;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
~~~

Replace server/instance and credential placeholders with your own local values. Do not commit a real password.

### 3. Store the connection string in User Secrets

The Web project already defines a User Secrets ID. From the repository root, run this in PowerShell:

~~~powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

Alternatively, in Visual Studio, right-click **KestrelHelpdesk.Web** → **Manage User Secrets** and add the following JSON with your own connection string:

~~~json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
~~~

User Secrets are for local development, not production secret management.

### 4. Restore and build

~~~powershell
dotnet restore .\KestrelHelpdesk.slnx
dotnet build .\KestrelHelpdesk.slnx
~~~

### 5. Apply database migrations

If dotnet ef is not installed, install the matching EF Core tool once:

~~~powershell
dotnet tool install --global dotnet-ef --version 10.0.12
~~~

Then, from the repository root:

~~~powershell
dotnet ef database update --project .\KestrelHelpdesk.Infrastructure\KestrelHelpdesk.Infrastructure.csproj --startup-project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

This applies the migrations to the database selected by DefaultConnection. Confirm the target database before running it, especially when switching between development and staging databases.

### 6. Run the Web project

Set **KestrelHelpdesk.Web** as the startup project and press **F5** (or **Ctrl+F5**). Use the HTTPS URL shown by Visual Studio.

The application startup seeder creates the Employee, Agent, and Manager roles and creates the default demo accounts if those accounts do not already exist.

## Run from the CLI

After configuring User Secrets and applying migrations:

~~~powershell
dotnet run --project .\KestrelHelpdesk.Web\KestrelHelpdesk.Web.csproj
~~~

Open the HTTPS/HTTP address printed by the application. To run tests:

~~~powershell
dotnet test .\KestrelHelpdesk.slnx
~~~

## Run with Docker

The Dockerfile builds and runs the **Web application only**. It does not start a SQL Server container; SQL Server must be provided separately. On Windows with Docker Desktop, the Web container can reach SQL Server on the host using host.docker.internal if SQL Server listens on a reachable TCP port.

### 1. Prepare the database on the host

Configure the Web project's User Secrets for the local SQL Server and apply EF Core migrations using the steps in [Run with Visual Studio](#run-with-visual-studio). The database must be migrated before first use of the container. Ensure SQL Server accepts TCP connections (commonly port 1433). Substitute another port below if your instance uses one.

### 2. Build the image

From the repository root:

~~~powershell
docker build -t kestrelhelpdesk:local -f Dockerfile .
~~~

### 3. Start the container

Replace the database login/password placeholders with local development credentials. Do not put production secrets into command history or documentation.

~~~powershell
docker run --name kestrelhelpdesk `
  -p 8080:8080 `
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=KestrelHelpdesk;User Id=YOUR_SQL_LOGIN;Password=YOUR_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" `
  -e "FileStorage__AttachmentsPath=/app/FileStorage/Attachments" `
  -v kestrelhelpdesk-attachments:/app/FileStorage/Attachments `
  kestrelhelpdesk:local
~~~

Open http://localhost:8080.

The named Docker volume kestrelhelpdesk-attachments persists uploaded files across container replacement. SQL Server backups and this volume should be included in any backup/restore plan. The environment variable ConnectionStrings__DefaultConnection maps to the .NET configuration key ConnectionStrings:DefaultConnection.

To stop and remove the container:

~~~powershell
docker stop kestrelhelpdesk
docker rm kestrelhelpdesk
~~~

The named attachment volume is retained unless explicitly removed. To start the same container again instead of recreating it, use docker start kestrelhelpdesk.

> **Linux Docker note:** host.docker.internal is available by default in Docker Desktop. On other Linux Docker setups, configure a host-gateway mapping or put the app and SQL Server on the same Docker network and use the SQL Server service/container hostname.

## Seed accounts and onboarding

The current Identity seeder defines these demo credentials for a fresh development database:

| Role | Email | Password |
|---|---|---|
| Employee | employee@kestrelhelpdesk.com | Employee123! |
| Agent | agent@kestrelhelpdesk.com | Agent123! |
| Manager | manager@kestrelhelpdesk.com | Manager123! |

**There is currently one seeded Manager account:** manager@kestrelhelpdesk.com. The role assignment page does not grant the Manager role or allow this account to be reassigned. It manages Employee and Agent roles only.

### Assign a role to a newly registered user

1. The user opens Register and creates an account.
2. The account is signed in but has no application role, so it is directed to Role Assignment Pending. Ticket board, dashboard, ticket creation, and Excel import remain restricted.
3. Sign in as the Manager using the demo account above.
4. Open **Role Assignment** from the sidebar.
5. Select the **New Registered User** tab.
6. Choose **Employee** or **Agent** for the new account.
7. Ask the user to sign out and sign in again so the session receives refreshed role claims.

The **Already Assigned** tab can change an existing Employee to Agent or Agent to Employee. The Manager is excluded from this list.

If demo accounts already exist in a persistent database, the seeder does not reset their passwords. The passwords shown above are initial values used when the accounts are first created. If a password has already changed, use the current password or reset it through an authorized administration procedure.

## Optional SQL scripts

Scripts are provided in the repository's Scripts directory.

### Topic09Authorization.sql

A compatibility/backfill script for an older development database. It conditionally adds the assignment column and index when missing, and maps older sample creator names/emails to Identity user IDs. A fresh database created from the current EF Core migration chain should not need this historical adjustment script. Review the target schema and take a backup before using it against an existing database.

### Topic13Performance.sql

Adds generated test tickets until the Tickets table contains 10,000 rows. It requires the seeded Employee, Agent, and Manager users to exist first. Use SSMS to connect to the intended KestrelHelpdesk database and run the script only when you want a large dataset for checking board search, paging, rendering, and dashboard behavior. It does not add another 10,000 rows every time; if the table already contains at least 10,000 records, it inserts nothing.

These scripts are optional. They are not required for the application to start on a clean database.

## Build and test

From the repository root:

~~~powershell
dotnet restore .\KestrelHelpdesk.slnx
dotnet build .\KestrelHelpdesk.slnx
dotnet test .\KestrelHelpdesk.slnx
~~~

The test project uses **xUnit v3** and **bUnit**. Current tests cover selected UI behavior and policy rules such as ticket visibility and attachment restrictions. A successful build confirms compilation; it does not by itself prove SQL Server connectivity or that every user workflow has been exercised.

Useful smoke tests:

- Sign in as each seeded role and validate board/dashboard scope.
- Verify ticket creation/editing, comments, attachment upload/download/delete, and PDF export.
- Import a valid .xlsx workbook, then try an invalid workbook and a workbook with assignment data as a non-Manager.
- Register a user, verify the pending-role experience, assign the role as Manager, and sign in again as the new user.
- When changing the EF model, check whether model changes are pending before generating or applying migrations.

## Configuration reference

| Key | Purpose | Example/default |
|---|---|---|
| ConnectionStrings:DefaultConnection | SQL Server connection | Configure with User Secrets or an environment variable; no default is committed |
| FileStorage:AttachmentsPath | Directory for uploaded files | FileStorage/Attachments under the app content root; Docker example: /app/FileStorage/Attachments |
| ASPNETCORE_HTTP_PORTS | Container HTTP listener | 8080 in the Dockerfile |
| ASPNETCORE_ENVIRONMENT | Runtime environment | Use Development for local debugging; use a production environment when deployed |

For nested configuration keys in environment variables, use double underscores. For example, ConnectionStrings:DefaultConnection becomes ConnectionStrings__DefaultConnection.

## Troubleshooting

### The app cannot connect to SQL Server

- Confirm the SQL Server service is running and listening on the configured instance/port.
- Verify the connection string is present in User Secrets locally or passed as an environment variable for Docker.
- For Docker Desktop on Windows, use host.docker.internal,1433 to reach SQL Server on the host, not localhost (inside a container, localhost points to the container).
- Confirm the SQL login has database access and the TLS settings match your local certificate setup.

### Database objects are missing

Apply the current EF Core migrations to the intended database using dotnet ef database update. Do not run SQL scripts until you have confirmed which migrations and schema changes the database already contains.

### A registered user cannot access tickets

This is expected until a Manager assigns Employee or Agent. Open **Role Assignment → New Registered User**, assign a role, then sign out and sign in again.

### The dashboard contains no data

Dashboard counts follow the user's role and selected scope. Employees see their own created tickets; Agents see their selected assigned/created view; Managers see all tickets. A clean database has demo users but does not automatically create sample tickets. Create tickets in the UI or optionally run Scripts/Topic13Performance.sql.

### Uploaded files disappear after recreating the Docker container

Ensure the named volume kestrelhelpdesk-attachments is mounted to /app/FileStorage/Attachments. Both attachment metadata and the physical files must be retained.

### EF Core reports pending model changes

A pending-model-changes warning means the current EF model and migration snapshot may not be aligned. Review the model and existing migrations first; do not automatically generate or apply extra migrations against a shared/staging database without reviewing the generated migration.

## Security and production handover

Before treating the application as production-ready:

- Replace and externalize the hard-coded demo seed passwords in IdentitySeeder. Prefer secure configuration or a secrets manager and ensure demo accounts are disabled or rotated in the client environment.
- Use production-grade SQL Server credentials, least-privilege access, backups, TLS, and a controlled migration process.
- Keep connection strings and storage credentials out of source control, tickets, and public documentation.
- Protect and back up attachment storage as well as SQL Server.
- Use the Production environment; do not enable detailed development errors for end users.
- Review retention, audit, monitoring, rate limiting, account recovery, password policies, and operational alerting against client requirements.
- Local file storage is used for attachments. For a multi-instance deployment, plan shared durable storage or an object-storage service so every instance can access the same files.
- Chart.js is currently imported from jsDelivr. Deployments with strict outbound network policies should vendor the library or provide an approved static asset source.

This README describes the current implementation and its development setup. Production deployment should include a separate environment-specific configuration and security review.
