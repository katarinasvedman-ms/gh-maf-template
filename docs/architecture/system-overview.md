# System Overview

## Architecture boundaries

- `Template.Host`: composition root only; wires dependencies and runtime entrypoints.
- `Template.Agents`: reusable agent contracts, standalone agent specs, and worker role examples.
- `Template.Tools`: parser plus safe execution pipeline (policy, approval, validation, timeout, retry).
- `Template.Mcp`: transport/adaptation boundary that normalizes MCP tools into common contracts.
- `Template.Observability`: telemetry abstractions and provider bootstrap points.
- `Template.Scenarios`: runtime orchestration scenarios under `Scenarios/<ScenarioName>/`.
- `Template.Evaluation`: dataset loading, deterministic scoring, and report/artifact generation.

## Runtime flow

1. Tools are registered with structured contracts and validated at registration.
2. For each target language, `Template.Host` derives runtime context (mode/intent/risk) and filters available tools before selection.
3. Host builds and parses an MCP-style `/tool ...` translator lookup command.
4. Parsed tool requests execute through `SafeToolExecutor` (policy -> approval -> validation -> execution -> timeout/retry).
5. `Template.Agents` builds standalone translation agent specs from `Agents/<AgentName>/` code + instruction files, with runtime translator identities and workflow guidance injected into templates.
6. `Template.Host` dispatches one runtime scenario (for example `TranslateText`) from `Template.Scenarios`.
7. The selected scenario composes `Template.Evaluation` (`EvaluationDatasetLoader` + `AgentScenarioEvaluator`) to produce machine-readable artifacts for CI.

## Boundary invariants

- Tool calls must flow through `ToolCommandParser` and `IToolExecutor`.
- Tool selection must be context-filtered before execution.
- Tool contracts must be structured and runtime-discoverable through `IToolRegistry.List()`.
- No bypass of policy/approval gates.
- Structured failures remain explicit and machine-readable.
- `EffectiveRiskLevel` reflects execution history via `PriorCallsInTurn` — trailing consecutive failures (the unbroken run of failures at the end of the sequence) escalate the risk level used for policy checks. A success resets the count.
- `IAgent` carries an `AgentContract` that mirrors `ToolContract`, enabling task adherence evaluation and Foundry signal interpretation.
