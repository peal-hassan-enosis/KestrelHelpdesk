# Workflows

## Login

`/Account/Login` uses ASP.NET Core Identity's `SignInManager.PasswordSignInAsync`. The login form uses labels for Email and Password and redirects to a return URL or `/` after success.

## Create ticket

`/tickets/new` -> `TicketForm` -> `NewTicket.CreateTicket` -> `ITicketService.CreateTicketAsync` -> repository -> SQL Server.

The service sets creator, initial status, assignment and creation time. After the ticket is created, attachment changes are applied and the browser navigates to `/tickets/{id}`.

## Ticket board

`/tickets` loads server-side grid data through `ITicketQueryService`. Agents can switch between Assigned and Created by Me. Managers have an unassigned-only filter. Search and status filtering are passed to the query service. Each row exposes View and Edit actions.

## Edit ticket

`/tickets/{id}/edit` loads the ticket through `ITicketService.GetTicketByIdAsync`. `TicketForm` allows status editing for non-Managers; Manager status is displayed but disabled. Submission calls `UpdateTicketAsync`, then attachment changes, then navigates back to ticket details.

## Ticket details and comments

`/tickets/{id}` loads the permitted ticket through `TicketService`. It displays description/status/priority/assignment, attachments, comments and an Add Comment form. Posting a comment calls `TicketCommentService.AddCommentAsync`, which rechecks ticket access and stamps the authenticated author/time.

## Assignment

Assignment UI is on the ticket board. Manager assignment uses a select of known agents. Agent assignment controls appear in the Created by Me scope and allow Assign to Me/Unassign. Both paths eventually call `TicketAssignmentService`, which performs role and ownership validation.

## Dashboard

`/dashboard` determines the current role, selects the default scope and calls `ITicketQueryService.GetTicketStatusCountsAsync`. Agents can change scope. The page renders status metrics and a Chart.js bar chart through `wwwroot/js/dashboard.js`.

## Attachments

`TicketForm` validates client-visible count/size constraints and submits selected files with the ticket. `ITicketAttachmentService` applies changes. Download is exposed as `/tickets/{ticketId}/attachments/{attachmentId}` in `Program.cs`; the download service receives the authenticated principal and rechecks access before returning the file.

## Excel import

The Web project contains `/tickets/import-excel`. The README documents validation of `.xlsx` files, required columns, row limits, status/creator restrictions and Manager-only application of assignment data. The exact import call path should be inspected before changing import behavior.

## PDF export

The ticket board exposes Export page and Export all actions. The README states exports honor the active permitted view, filters and sort order. The PDF service is responsible for document generation while browser JavaScript triggers the download.

## Role onboarding

Identity users without an application role are routed to the pending-role flow. Managers can use `/role-assignment` to assign Employee or Agent. The README notes that users should sign out and sign in again after role assignment so refreshed role claims are present.

## Health

`Program.cs` maps `/health` using ASP.NET Core health checks with an EF Core database check.
