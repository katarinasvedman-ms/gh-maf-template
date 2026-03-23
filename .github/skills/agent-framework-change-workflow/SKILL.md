# Agent Framework Change Workflow

Use this skill to implement or modify Agent Framework-backed runtime behavior safely.

## Typical user asks (plain language)

- "Add an agent that connects to MCP server X and answers user questions."
- "Wire a new runtime agent into the host and add required tests."
- "Extend agent routing for this new capability without changing safety boundaries."

Users do not need to mention this workflow ID explicitly.

## Use for

- Adding or changing worker behavior in `src/Template.Agents/*`.
- Updating host composition for framework-backed runtime in `src/Template.Host/*`.
- Extending tool routing interactions that remain orchestrator -> parser -> executor.

## Do not use for

- Skipping required tests/docs updates.
- Bypassing `SafeToolExecutor`, `IToolPolicy`, or `IToolApprovalGate`.
- Infrastructure deployment workflows.

## Invocation context

This skill is invoked by the Implementer agent during a development-agent-workflow
run, or directly for straightforward runtime changes that do not require
multi-agent orchestration.

## Required inputs

- The intended behavior change.
- Expected runtime impact (routing, execution, observability, evaluation).
- Acceptance criteria.

## Advanced invocation (optional)

- `agent-framework-change-workflow`

## Execution flow

1. Read `./.github/copilot-instructions.md` and relevant `./.github/instructions/*.instructions.md` files.
2. Locate impacted boundaries in:
   - `src/Template.Agents/*`
   - `src/Template.Host/*`
   - `src/Template.Tools/*` when routing or invocation semantics are involved
3. Implement minimal diff while preserving invariants from architecture docs.
4. Add/update tests in:
   - `tests/Template.UnitTests/*`
   - `tests/Template.IntegrationTests/*`
5. Update architecture and usage docs when behavior or boundaries change.
6. Validate with:
   - `dotnet build <SolutionName>.sln`
   - `dotnet test <SolutionName>.sln`
   - `dotnet run --project src/<ProjectName>.Host/<ProjectName>.Host.csproj` for runtime-affecting changes

## Completion checks

- No policy/approval bypass paths introduced.
- Test coverage updated for changed behavior.
- Docs updated for boundary or user-visible changes.
- Validation commands pass.

## Common failure modes

- Changed orchestrator behavior without parser/executor tests.
- Moving logic into host composition instead of runtime libraries.
- Updating runtime behavior without architecture doc changes.
