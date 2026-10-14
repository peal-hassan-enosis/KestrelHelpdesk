# Conventions

## Observed source conventions

- C# nullable reference types and implicit usings are enabled across projects.
- Application code uses async service/repository methods returning `Task`/`Task<T>`.
- Services receive dependencies through constructor injection and are registered in `Program.cs`.
- Core services perform current-user checks and business authorization rather than trusting UI state.
- Blazor pages commonly declare `@page`, `[Authorize]`, `@rendermode InteractiveServer`, injected service contracts and private async workflow methods.
- Data validation uses DataAnnotations on domain models and explicit validation in `TicketService`.
- UI role visibility uses `[Authorize]` and `AuthorizeView`; service authorization remains separate.
- Playwright tests in this branch should prefer semantic locators (`GetByRole`, `GetByLabel`, `GetByPlaceholder`, `GetByTestId`) and Playwright web-first assertions.
- Test data should use unique values rather than fixed database counts.

## UI/testability conventions observed

Ticket form controls expose stable labels/IDs such as:

- `ticket-title`
- `ticket-category`
- `ticket-priority`
- `ticket-status`
- `ticket-description`

Ticket details exposes a comment textarea with placeholder `Write a response...` and a `Post Comment` button. The ticket board exposes `Create ticket`, `View`, `Edit`, status filters and Agent scope buttons.

## Do not infer

The project does not establish a universal `data-testid` convention in the inspected source. E2E tests should therefore use accessible labels/roles or stable IDs already present instead of inventing a test-ID convention.

## Testing separation

The existing `KestrelHelpdesk.Tests` project is a unit/component test project using xUnit v3 and bUnit. Browser E2E tests belong in the dedicated E2E project introduced by this branch.
