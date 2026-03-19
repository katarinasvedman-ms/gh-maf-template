# Coding Standards

## Design boundaries

- Keep host composition in `Template.Host`; avoid domain logic in `Program.cs`.
- Keep reusable agent contracts and worker role implementations in `Template.Agents`.
- Keep runtime orchestration scenarios in `Template.Scenarios` under `Scenarios/<ScenarioName>/`.
- Keep parsing and safety execution logic in `Template.Tools`.
- Keep MCP transport adaptation isolated to `Template.Mcp`.
- Keep dataset loading/scoring/reporting logic in `Template.Evaluation`.

## Code-level rules

- Use explicit outcomes and typed contracts over stringly-typed status handling.
- Keep `ToolContract` metadata complete and machine-readable for each tool.
- Keep `ToolErrorCode` mappings stable and test-covered.
- Keep cancellation and timeout behavior deterministic and explicit.
- Keep retries bounded; no infinite loops or unbounded backoff.
- Preserve correlation IDs in logs/telemetry across host -> parser -> executor paths.

## Safety and logging

- Never bypass `IToolPolicy` or `IToolApprovalGate`.
- Apply context-based tool filtering (mode/intent/risk) before tool selection.
- Do not log raw secrets or full sensitive tool payloads.
- Prefer structured logs with code, tool name, and correlation ID.

## Testing discipline

- Parser behavior changes require parser-focused unit tests.
- Executor policy/approval/timeout/retry changes require targeted unit tests and affected integration coverage.
- MCP or evaluation changes require integration/evaluation assertions.

## Change discipline

- Prefer the smallest safe diff.
- Avoid adding dependencies unless justified.
- Update docs whenever runtime behavior, boundaries, or workflows change.
