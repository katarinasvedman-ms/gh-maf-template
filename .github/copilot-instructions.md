# Copilot Instructions

This repository uses one guidance plane only:
- Repo-wide contract in this file.
- Path-specific constraints in `.github/instructions/*.instructions.md`.

No custom agent profiles are part of the supported model.

## Operating contract

- Use the smallest safe diff.
- Do not introduce packages without documented need.
- Do not change public contracts without rationale and tests.
- Do not edit generated/runtime outputs (`bin/`, `obj/`, `artifacts/*.json`) directly.
- Preserve architecture boundaries documented in `docs/architecture/system-overview.md`.

## Required review mindset

When the user asks for a "review", prioritize:
1. correctness and behavioral regressions,
2. safety/policy bypass risk,
3. missing tests,
4. missing docs updates.

Findings come first, ordered by severity, with file/line evidence.

## Completion criteria

A change is complete only when all apply:
1. required files are updated (code/tests/docs/workflows as impacted),
2. `dotnet build gh-maf-template.sln` passes for code changes,
3. `dotnet test gh-maf-template.sln` passes for behavior/test changes,
4. for host/runtime changes, `dotnet run --project src/Template.Host/Template.Host.csproj` produces `artifacts/evaluation-report.json`,
5. no stale references remain to removed guidance mechanisms.

## Documentation update rules

- Runtime behavior change: update `README.md` and `USAGE.md`.
- Boundary/contract change: update `docs/architecture/system-overview.md` and related architecture docs.
- Setup/workflow change: update `SETUP.md` and affected workflow/docs references.
- Decision-level change: update `DECISIONS.md` and `docs/architecture/decision-log.md`.

## Escalation-required surfaces

When changes touch any of the following, also update architecture docs and targeted tests:
- `src/Template.Host/Program.cs`
- `src/Template.Tools/ToolCommandParser.cs`
- `src/Template.Tools/SafeToolExecutor.cs`
- `src/Template.Evaluation/*`
- `.github/workflows/*`

Treat these as high-risk surfaces: no drive-by refactors, no silent behavior shifts, and no completion without validation evidence.
