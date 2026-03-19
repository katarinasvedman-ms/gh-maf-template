---
applyTo: "src/**/*.cs,tests/**/*.cs"
---

## C# implementation rules

### Symbols and contracts that must be preserved

- `Template.Host/Program.cs` run path must integrate `Template.Agents`, `Template.Scenarios`, `Template.Tools`, `Template.Evaluation`, and `Template.Observability`.
- `ToolCommandParser`: command grammar and argument parsing behavior, including quoted/escaped values.
- `SafeToolExecutor`: policy -> approval -> validation -> execution -> timeout/retry ordering.
- `IToolPolicy`, `IToolApprovalGate`, `IToolExecutor`: do not bypass or inline around these abstractions.
- `ToolErrorCode`: keep taxonomy and mapping (`UnknownTool`, `DeniedByPolicy`, `InvalidArguments`, `ApprovalDenied`, `Timeout`, `ExecutionFailed`, `TransientFailure`).

### Forbidden refactors

- Do not move safety logic out of `SafeToolExecutor`.
- Do not move parser semantics into ad-hoc string handling in host.
- Do not execute tool invocations in host without going through `ToolCommandParser` + `IToolExecutor`.
- Do not collapse structured failures into generic exceptions or string-only errors.

### If you change X, also update Y

- Parser grammar in `ToolCommandParser` -> update parser-focused unit tests and usage docs examples.
- Host tool-command routing in `Template.Host/Program.cs` -> update host runtime docs and relevant integration/unit tests.
- Policy/approval/timeout/retry behavior in `SafeToolExecutor` -> update executor tests and `docs/architecture/coding-standards.md` safety rules.
- Tool failure contract (`ToolErrorCode`, metadata) -> update evaluation assertions in `src/Template.Evaluation/*` and related tests.

### Examples

Allowed:
- Add support for escaped quote handling in `ToolCommandParser` and add/adjust parser tests.
- Add bounded retry jitter in `SafeToolExecutor` while preserving terminal-failure no-retry behavior and tests.

Disallowed:
- Executing tools directly from `Template.Host/Program.cs` without going through `IToolExecutor`.
- Replacing `ToolErrorCode`-based outcomes with a single "failed" status.

## Validation requirement

- Behavior changes must include corresponding unit or integration tests.
- Host runtime changes must keep `scripts/validate_runtime_contract.ps1` passing.
