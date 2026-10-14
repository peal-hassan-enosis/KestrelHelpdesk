# Command: Create Project Knowledge

## Purpose

This file is an executable instruction specification for an AI coding agent. It is repository-independent and must work against an arbitrary software repository.

When the user asks the agent to execute this command, the agent must inspect the repository itself and create a verified project-knowledge base. The agent must not ask the user to attach or enumerate source files merely because the repository is large.

## Execution contract

1. Treat this file as instructions to execute, not as documentation to edit.
2. Start at the repository root.
3. Discover the repository structure yourself. Do not ask the user to provide Program.cs, project files, services, models, controllers, database contexts, or other source files.
4. Read README.md and other high-level documentation first when present, but use the actual source/configuration as the source of truth.
5. Never invent an implementation detail. If a claim cannot be verified, write `Uncertain` and explain what evidence is missing.
6. Create or update `.ai/project-knowledge/` and populate the required files before declaring the task complete.
7. Do not modify this command file while executing it.

## Repository discovery

Inspect the repository systematically:

- solution/workspace files (`*.sln`, `*.slnx`, package/workspace manifests)
- all application/test project files (`*.csproj`, `package.json`, `pyproject.toml`, etc.)
- dependency manifests and lock files
- startup/configuration files
- domain models, entities, enums and value objects
- interfaces and implementations
- services/business logic
- repositories/data-access layers
- database contexts, migrations and schema definitions
- authentication/authorization and user/role handling
- API controllers/endpoints, pages, components, views and routing
- frontend JavaScript/TypeScript and browser integrations
- tests and test configuration
- external integrations
- Docker/CI/build configuration
- important scripts and developer workflows

Use directory listings, repository maps and targeted file inspection to progressively narrow the source set. Do not attempt to read every file indiscriminately. Follow references from important entry points to the implementations they call.

## Evidence rules

For every important architectural, domain, workflow, security, dependency or testing claim:

- inspect the relevant implementation;
- record concrete repository paths in the knowledge file;
- distinguish observed behavior from inference;
- mark unsupported or ambiguous statements `Uncertain`.

Do not infer behavior from filenames alone. For example, an interface existing does not prove it is used, and a package being installed does not prove the feature is active.

## Required output

Create exactly these seven knowledge files under `.ai/project-knowledge/`:

1. `index.md` - entry point, project purpose, major areas, knowledge-file map, important source locations and uncertainties.
2. `tech-stack.md` - verified languages, frameworks, packages, runtimes, databases, build/test tooling and exact versions from authoritative files.
3. `architecture.md` - actual project boundaries, dependency direction, startup/DI configuration, runtime flow, persistence flow, authentication flow and extension points. This is the highest-priority file.
4. `conventions.md` - conventions actually observed in source code: naming, DI, async patterns, component structure, validation, error handling, testing, selectors and other recurring patterns. Do not turn generic best practices into project conventions.
5. `domain.md` - actual entities, enums, relationships, lifecycle, business rules and authorization. Clearly distinguish UI restrictions, service-level enforcement and database constraints.
6. `workflows.md` - real end-to-end code paths from UI/API entry point through services/repositories/integrations to persistence, including authorization, validation and important side effects.
7. `decisions.md` - verified technical/architectural decisions and, separately, any inferred rationale labelled `Uncertain`.

## Knowledge quality requirements

The knowledge base must be useful to a future coding agent, not generic documentation.

Prefer:

- concrete class/interface/project/file names;
- real call paths;
- real routes/endpoints;
- actual dependency registration;
- actual role/permission rules;
- actual data flow;
- actual validation and error behavior;
- actual testing conventions;
- concise, high-density notes.

Avoid:

- generic explanations of frameworks;
- generic clean-code advice;
- invented features;
- duplicated information;
- temporary implementation details that do not affect future development;
- secrets, passwords, tokens, connection strings or other sensitive values.

## Verification before completion

Before finishing:

1. Verify all seven files exist and are populated.
2. Re-check important claims against source code.
3. Remove unsupported claims.
4. Verify technology versions against authoritative project files.
5. Verify dependency direction against actual project references.
6. Verify business rules against the service/domain implementation where possible.
7. Verify workflows against real entry points and call paths.
8. Verify authentication/authorization claims against actual configuration and enforcement.
9. Verify no secrets were copied into the knowledge base.
10. Report the files created/updated, major areas discovered, and important uncertainties.

## Source-of-truth rule

Project knowledge is an acceleration layer, not the source of truth. If knowledge conflicts with source code, source code wins. If knowledge is incomplete, inspect the source code before implementing behavior.
