# Decisions

## Verified decisions

### Layered project separation

The solution separates Core, Infrastructure and Web projects. Web references both Core and Infrastructure; Infrastructure references Core. This is directly represented by project references.

### Interactive Server Blazor

`Program.cs` configures Razor Components with Interactive Server components, and routed pages explicitly use `@rendermode InteractiveServer` where needed.

### Service-level authorization

Ticket authorization is deliberately enforced in Core services/policies as well as reflected in the UI. This is visible in `TicketAccessPolicy`, `TicketService`, `TicketAssignmentService` and `TicketCommentService`.

### EF Core DbContext factory

The Web application registers `AddDbContextFactory<ApplicationDbContext>` rather than directly registering a normal scoped DbContext. This is a verified startup choice.

### Identity roles

Application roles are Employee, Agent and Manager. Identity seeding is performed during application startup.

### Separate browser E2E project

The E2E branch uses a dedicated Playwright project rather than adding browser-test dependencies to the existing bUnit/xUnit component-test project. This keeps browser automation isolated from component/unit tests.

## Inferred rationale

The reasons behind some implementation choices are not explicitly documented in source code. For example, the exact reason for using a DbContext factory instead of a scoped DbContext is **Uncertain** from the inspected source. Do not treat inferred rationale as an established architectural requirement.

## Future-change rule

When changing architecture or testing strategy, update this file only after the implementation has been verified. Project knowledge must not become a second, conflicting source of truth.
