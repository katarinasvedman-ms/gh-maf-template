---
applyTo: "tests/**/*"
---

## Test rules

- Every behavior change in `src/` requires test updates or explicit rationale.
- Prefer deterministic tests with no external network dependency.
- Name tests by behavior and expected outcome.

## Required coverage by change type

- Parser changes (`src/Template.Tools/ToolCommandParser.cs`):
	- malformed `/tool` syntax
	- missing, duplicate, quoted, and escaped arguments
	- parse error code and message shape consistency

- Policy/approval changes (`IToolPolicy`, `IToolApprovalGate`, `SafeToolExecutor`):
	- policy denial path
	- high-sensitivity approval denied path
	- no execution when denied

- Retry/timeout changes (`SafeToolExecutor`):
	- timeout path is deterministic
	- transient retries remain bounded
	- terminal failures are not retried

- MCP/evaluation changes (`src/Template.Mcp/*`, `src/Template.Evaluation/*`):
	- MCP unavailable/registration failure diagnostics
	- evaluation report generation and expected artifact assertions

## Completion blockers

- Unacceptable: runtime behavior changed, tests unchanged, and no explicit rationale in PR/docs.
- Acceptable: parser behavior changed with focused parser test updates plus any impacted integration tests.
