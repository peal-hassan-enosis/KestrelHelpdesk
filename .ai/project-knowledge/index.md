# Project Knowledge Index

## Project

**Kestrel Helpdesk** is a role-based support-ticket web application. The README describes ticket creation, assignment, comments, attachments, dashboard reporting, Excel import, PDF export and manager-controlled role assignment.

## Repository structure

- `KestrelHelpdesk.Core/` — domain models, authorization policies, interfaces and application services.
- `KestrelHelpdesk.Infrastructure/` — EF Core/SQL Server persistence, ASP.NET Core Identity, repositories, local file storage and image processing.
- `KestrelHelpdesk.Web/` — Blazor Web App UI, Identity UI, browser integrations, PDF/import services and HTTP endpoint wiring.
- `KestrelHelpdesk.Tests/` — existing xUnit v3/bUnit tests.
- `KestrelHelpdesk.slnx` — solution containing the three application projects at the current baseline.

## Important source locations

- Startup/DI: `KestrelHelpdesk.Web/Program.cs`
- Ticket model: `KestrelHelpdesk.Core/Models/Ticket.cs`
- Comment model: `KestrelHelpdesk.Core/Models/Comment.cs`
- Access policy: `KestrelHelpdesk.Core/Authorization/TicketAccessPolicy.cs`
- Roles: `KestrelHelpdesk.Core/Authorization/AppRoles.cs`
- Ticket business rules: `KestrelHelpdesk.Core/Services/TicketService.cs`
- Ticket queries/scopes: `KestrelHelpdesk.Core/Services/TicketQueryService.cs`
- Assignment rules: `KestrelHelpdesk.Core/Services/TicketAssignmentService.cs`
- Comment rules: `KestrelHelpdesk.Core/Services/TicketCommentService.cs`
- EF context: `KestrelHelpdesk.Infrastructure/Data/ApplicationDbContext.cs`
- Identity seeding: `KestrelHelpdesk.Infrastructure/Identity/IdentitySeeder.cs`
- Login UI: `KestrelHelpdesk.Web/Components/Account/Pages/Login.razor`
- Ticket board: `KestrelHelpdesk.Web/Components/Pages/Tickets.razor`
- Ticket form: `KestrelHelpdesk.Web/Components/Pages/TicketForm.razor`
- New ticket: `KestrelHelpdesk.Web/Components/Pages/NewTicket.razor`
- Edit ticket: `KestrelHelpdesk.Web/Components/Pages/EditTicket.razor`
- Details/comments: `KestrelHelpdesk.Web/Components/Pages/TicketDetails.razor`
- Dashboard: `KestrelHelpdesk.Web/Components/Pages/Dashboard.razor`

## Knowledge files

- `tech-stack.md` — verified technologies and versions.
- `architecture.md` — actual component/project boundaries and runtime flow.
- `conventions.md` — observed coding/UI/testing conventions.
- `domain.md` — entities, roles, rules and authorization.
- `workflows.md` — important user journeys and code paths.
- `decisions.md` — verified architectural choices and clearly marked inferences.

## Source-of-truth reminder

This knowledge base is an accelerator for coding agents. If it conflicts with source code, source code wins.

## Uncertainties

The current repository evidence available for this knowledge snapshot does not establish a dedicated Playwright E2E project yet. The existing `KestrelHelpdesk.Tests` project is a unit/component test project using xUnit v3 and bUnit.
