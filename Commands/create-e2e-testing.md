# Command: Create Playwright E2E Tests

## Purpose

This is a repository-independent executable instruction specification for creating Playwright end-to-end/integration tests for a web application.

The user supplies a scope after the command. The scope may target the entire application, a changed feature/area, a workflow, a route, a component/page, or a set of related files.

Examples of user scope statements:

- `Scope: whole application`
- `Scope: ticket creation and editing`
- `Scope: changed files in the current branch`
- `Scope: /tickets and /tickets/{id}`
- `Scope: dashboard only`
- `Scope: authentication and role-based access`

If no scope is supplied, use `whole application`.

## Execution contract

1. Treat this file as executable instructions, not documentation to edit.
2. Read `.ai/project-knowledge/` first when it exists. It is an acceleration layer and must be verified against source code.
3. Discover the repository and existing testing architecture yourself. Do not ask the user to attach source files.
4. Interpret the user's scope literally and test only the requested area plus the minimum supporting flows required for a meaningful E2E test.
5. If the scope is `whole application`, inventory the application's user-facing workflows and cover the highest-value business paths and role/security boundaries across the application.
6. Never invent routes, selectors, roles, data, APIs, or expected behavior. Inspect the source.
7. Do not modify this command file.

## Phase 1: Understand the application

Inspect:

- project/solution structure;
- project target frameworks and package versions;
- startup and application URLs;
- authentication and authorization;
- seeded/test users and role setup without copying secrets into source-control documentation;
- routes/endpoints/pages/components;
- domain models and business rules;
- services/repositories involved in the scoped workflows;
- existing unit/component/integration/E2E tests;
- test-runner conventions;
- existing browser selectors and accessibility labels;
- data prerequisites and external dependencies.

Use `.ai/project-knowledge/` to accelerate discovery, but verify important claims against the source.

## Phase 2: Choose the E2E architecture

Prefer the repository's existing test framework when it is suitable. If Playwright is requested and the repository is .NET with xUnit v3, use the official `Microsoft.Playwright.Xunit.v3` integration unless the repository already has a stronger established pattern.

Keep browser tests separate from unit/component tests when that separation improves isolation and execution.

Use a configurable base URL rather than hard-coding a developer-specific URL. Prefer an environment variable such as `KESTREL_E2E_BASE_URL` or a repository-appropriate equivalent.

Use isolated browser contexts/test fixtures. Avoid shared browser state between tests.

## Phase 3: Build the test plan from behavior

For each scoped feature, identify:

1. happy path;
2. validation/error path where it is business-significant;
3. authentication/authorization boundary;
4. important state transition;
5. persistence-visible result;
6. important role-specific behavior;
7. relevant file upload/download or browser integration when applicable.

For a whole-application scope, prioritize end-to-end business journeys over exhaustive UI snapshot coverage.

## Locator rules

Prefer stable user-facing and accessibility-oriented locators:

1. `GetByRole`;
2. `GetByLabel`;
3. `GetByPlaceholder`;
4. `GetByTestId` when the application provides stable test IDs;
5. stable IDs/selectors when necessary.

Avoid brittle CSS chains, generated framework classes, DOM indexes and implementation-specific selectors when a semantic locator exists.

If the application lacks stable selectors for an important workflow, use the most stable existing selector and record the limitation rather than modifying production UI solely for convenience unless the user explicitly asks for testability improvements.

## Assertion and synchronization rules

Use Playwright web-first assertions. Do not add arbitrary sleeps or fixed delays.

Prefer assertions that automatically wait for the expected state. Use explicit waits only for real browser events such as downloads, popups, requests or other events that the workflow actually triggers.

Verify user-visible outcomes and business behavior, not implementation details.

## Test data rules

Use deterministic seeded development/test accounts when the repository provides them.

For created data:

- use unique, recognizable test titles/identifiers;
- do not assume a fixed database row count;
- do not rely on test execution order;
- do not modify or delete unrelated user data;
- do not commit real credentials or secrets.

If the application has no safe way to isolate or clean up test data, document that limitation and keep tests as non-destructive as possible.

## Authentication rules

Test login through the real UI when authentication is in scope. Do not bypass authentication by injecting cookies or directly calling internal services unless the test's purpose specifically requires it.

For role-based applications, test both positive and negative access boundaries where they are business-significant.

## Whole-application coverage guidance

When scope is `whole application`, inspect and cover applicable areas such as:

- public landing/navigation;
- login/logout and role onboarding;
- primary CRUD workflows;
- search/filter/sort/paging;
- role-specific visibility and authorization;
- assignment/workload flows;
- comments/conversation;
- attachments/upload/download;
- dashboard/reporting;
- import/export;
- important validation and error states;
- health/availability smoke checks;
- browser JavaScript interactions that are part of important user workflows.

Do not force a test for a feature that does not exist in the repository.

## Scope behavior

If the user supplies a changed-part scope, inspect the changed files and trace their dependencies. Add only the minimum supporting tests needed to exercise the changed behavior end to end.

If the user supplies a single-page/component scope, focus on that page/component's user-visible workflows and its important authorization/state boundaries.

If the user supplies a workflow scope, follow the complete user journey through all participating pages/services/data transitions.

## Implementation quality

Create maintainable tests with:

- a small reusable test base/fixture;
- centralized environment configuration;
- reusable login helpers where appropriate;
- descriptive test names;
- no duplicated selectors or credentials scattered across tests;
- clear test data naming;
- no arbitrary sleeps;
- no production-code coupling beyond stable public behavior.

If a test requires a new test project, add it to the solution/workspace and document installation/run requirements.

## Verification

After implementing tests:

1. restore/build the E2E project;
2. install the required Playwright browser binaries using the generated Playwright script;
3. run the scoped test suite against a running application when possible;
4. fix compile/test failures caused by the implementation;
5. review the tests for brittle selectors, sleeps, hidden assumptions and shared state;
6. verify the tests actually exercise the requested scope;
7. report any tests that could not be executed and why.

## Output

When complete, report:

- test project/files created or changed;
- scope interpreted;
- scenarios covered;
- commands used to build/run tests;
- execution result;
- any environmental prerequisites;
- any uncovered behavior or uncertainties.

## Source-of-truth rule

Project knowledge accelerates the work but never overrides the source code. When knowledge and source disagree, inspect and follow the source.
