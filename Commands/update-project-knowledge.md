# Command: Update Project Knowledge Incrementally

## Purpose

This is a repository-independent executable instruction specification for keeping `.ai/project-knowledge/` current after a project grows or changes.

The command is incremental: do not regenerate every knowledge file blindly. First determine what changed, then update only the knowledge areas affected by those changes, while still checking for contradictions with the source.

## Execution contract

1. Treat this file as instructions to execute, not documentation to edit.
2. Discover the repository and existing `.ai/project-knowledge/` yourself.
3. Never ask the user to manually provide source files.
4. Source code and authoritative configuration are the source of truth.
5. Do not invent information. Mark unresolved points `Uncertain`.
6. Do not modify this command file.

## Determine the change set

Inspect, in this order:

1. `git status` and the current branch when available.
2. The diff against the appropriate baseline branch/commit when available.
3. Recent commits when they help explain the change.
4. Existing project-knowledge files.
5. Source files affected by the change and the important callers/dependencies they influence.
6. Related tests, configuration, routes, migrations and documentation.

If a clean git diff is unavailable, compare the current source against the existing knowledge base and inspect the areas that appear stale.

## Dependency-aware impact analysis

Do not assume that a changed file affects only one knowledge file. Trace its references and classify impact across:

- technology/dependencies
- project architecture and dependency direction
- conventions
- domain models/rules
- workflows
- authentication/authorization
- persistence/schema
- testing/E2E behavior
- architectural decisions

For example, a new service may require changes to `architecture.md`, `workflows.md`, `conventions.md`, `domain.md` and `decisions.md`, depending on its actual responsibilities.

## Update rules

- Preserve accurate existing knowledge.
- Update only claims affected by the current change unless an existing claim is contradicted.
- Remove stale claims that are no longer true.
- Remove duplicated information when touching related sections.
- Add concrete source paths for new important claims.
- Record exact dependency versions from authoritative files.
- Record real call paths rather than generic descriptions.
- If a rationale is not documented or directly observable, label it `Uncertain` instead of presenting it as fact.
- Never copy secrets, passwords, tokens or connection strings.

## Consistency checks

After updating the affected files, check for contradictions across the complete knowledge base, especially:

- `architecture.md` versus project references and DI registration;
- `tech-stack.md` versus project/package manifests;
- `domain.md` versus actual models and service enforcement;
- `workflows.md` versus actual routes/components/services;
- `conventions.md` versus recurring source patterns;
- `decisions.md` versus actual implementation.

If the change makes an existing statement uncertain, correct it rather than preserving an obsolete assumption.

## Completion requirements

Before finishing:

1. Verify the changed knowledge files against source code.
2. Verify no stale or duplicated statements remain in affected areas.
3. Verify all important new behavior has an evidence-backed entry.
4. Verify no secrets were added.
5. Report which knowledge files changed, why they changed, the source areas inspected, and any remaining uncertainties.

## Source-of-truth rule

Project knowledge is not authoritative. When it conflicts with source code, source code wins. The next coding agent must inspect source when the knowledge base does not contain enough evidence.
