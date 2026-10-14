# Architecture

## Project boundaries

### Core

`KestrelHelpdesk.Core` contains models, authorization policies, interfaces and business/application services. It targets `net10.0` and has no project dependency beyond the .NET SDK.

Important areas:

- `Authorization/` — role names and ticket access policies.
- `Models/` — Ticket, Comment, TicketAttachment and import-related models.
- `Interfaces/` — contracts for repositories, services, current-user access and integrations.
- `Services/` — ticket CRUD, queries, assignment, comments and import rules.
- `Validators/` and `Events/` — validation/event support used by application behavior.

### Infrastructure

`KestrelHelpdesk.Infrastructure` references Core and implements persistence and infrastructure concerns. It contains `ApplicationDbContext`, EF repositories, ASP.NET Core Identity implementation/seeding, local file storage and SkiaSharp image processing.

### Web

`KestrelHelpdesk.Web` references Core and Infrastructure. It is the executable ASP.NET Core application and contains Razor components, Identity UI, browser-facing services, PDF/import adapters, JavaScript and startup configuration.

## Startup and dependency injection

`KestrelHelpdesk.Web/Program.cs`:

1. Adds Razor Components with Interactive Server components.
2. Registers MudBlazor.
3. Registers ticket services, query/assignment/comment/import services, repositories, attachment/file services, current-user and role services.
4. Registers `AddDbContextFactory<ApplicationDbContext>` with SQL Server.
5. Registers health checks using the EF context.
6. Configures Identity cookies and `IdentityCore<ApplicationUser>` with roles and EF stores.
7. Adds authorization and cascading authentication state.
8. Seeds roles/users at startup.
9. Maps `/health`, the authenticated attachment-download endpoint, Razor components and Identity endpoints.

## Runtime flow

Typical ticket interaction:

`Blazor page -> Core service -> repository interface -> Infrastructure repository -> ApplicationDbContext -> SQL Server`

The Web project owns user interaction and navigation. Core services enforce business rules and call interfaces. Infrastructure supplies persistence and other concrete implementations.

## Authorization flow

`IdentityCurrentUserContext` provides the authenticated user to Core through `ICurrentUserContext`. Core authorization policies/services make access decisions. UI components also use `[Authorize]` and `AuthorizeView` for presentation, but service-level checks are the important enforcement layer.

`TicketAccessPolicy.CanView` permits a Manager to view all tickets, the creator to view their own ticket, and an Agent to view a ticket assigned to that Agent. `CanEdit` currently delegates to `CanView`.

## Persistence

`ApplicationDbContext` derives from `IdentityDbContext<ApplicationUser>` and exposes `Tickets`, `Comments` and `TicketAttachments`. Ticket attachments have required metadata fields, a foreign-key relationship to Ticket and an index on `TicketId`. EF Core migrations define the database schema.

## Browser UI

Razor pages under `KestrelHelpdesk.Web/Components/Pages` expose routes for the ticket board, ticket creation/edit/details, dashboard, Excel import and role assignment. The UI uses MudBlazor plus normal HTML controls. Browser-specific JavaScript is used for interactions such as Chart.js rendering, clipboard access and ticket-form focus.

## Extension points

- Add business behavior in Core services/interfaces.
- Add persistence behavior through repository interfaces and Infrastructure implementations.
- Add UI routes/components under Web Components.
- Register new concrete implementations in `Program.cs`.
- Add E2E behavior under the dedicated Playwright project rather than the existing unit/component test project.

## Evidence

Primary architecture evidence: `KestrelHelpdesk.slnx`, all three application `.csproj` files, `KestrelHelpdesk.Web/Program.cs`, `KestrelHelpdesk.Infrastructure/Data/ApplicationDbContext.cs`, Core service/policy files and the route components.
