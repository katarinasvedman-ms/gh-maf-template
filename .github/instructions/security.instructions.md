---
applyTo: "src/**/*"
---

## Security rules

- Never hardcode secrets, tokens, or keys.
- Do not add new external endpoints without rationale and documentation.
- Keep least-privilege defaults for tool execution.
- Do not log sensitive payload content.

## Safety contract constraints

- Never bypass `IToolPolicy`.
- Never bypass `IToolApprovalGate` for high-sensitivity tools.
- Keep structured failure metadata (`ToolErrorCode` + contextual metadata) for observability and auditability.
- Preserve failure-path determinism: denied/invalid/timeout paths must be explicit and testable.

## Approval and execution boundaries

- Approval must occur before execution for high-sensitivity tools.
- Denied policy/approval outcomes must not execute side effects.
- Do not introduce alternate execution paths that skip `SafeToolExecutor`.

## Sensitive logging rules

- Never log raw secrets, tokens, credentials, or full sensitive payloads.
- Prefer redacted summaries and stable identifiers (correlation IDs, tool names, failure codes).
- Keep logs structured; avoid free-form exception dumps that can leak inputs.

## Examples

Acceptable:
- Log `ToolErrorCode=ApprovalDenied` with tool name and correlation ID.

Disallowed:
- Log full tool arguments for denied high-sensitivity requests.
