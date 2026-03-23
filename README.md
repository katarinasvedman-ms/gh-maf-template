## gh-maf-template

A structured engineering system for building agentic products—focused on control, evaluation, and observability, not just agent logic.

It helps teams move from prompt-driven experiments to systems that can be tested, constrained, and improved over time.

---

## What it provides

### Behavior layer
Define agents, workflows, and scenarios.

### Capability & control layer
Expose tools safely with contracts, policies, and context-based filtering.

### Evaluation layer
Test behavior using datasets and scenario-based checks.

### Observability layer
Trace decisions, tool usage, and failures to understand what happened.

### Governance layer
Repo instructions and workflows that standardize how changes are made.

---

## Why it matters

Agentic systems are not deterministic.  
Without structure, they become hard to trust, test, and operate.

This template adds the missing pieces:

- controlled capabilities instead of open-ended tools  
- measurable behavior instead of demos  
- traceable execution instead of guesswork  

---

## What this is not

- Not just an agent sample  
- Not just a prompt or tool wrapper  
- Not just a framework  

It’s the system around the agent that makes it possible to ship.
## Solution layout
- `src/Template.Host`: concurrent workflow sample (two translator agents) and artifact generation.
- `src/Template.Agents`: agent contracts and worker role examples.
- `src/Template.Tools`: safe tool execution pipeline, structured tool contracts, and context-based policy filtering.
- `src/Template.Mcp`: MCP adapters and multi-server catalog integration with graceful failure reporting.
- `src/Template.Observability`: runtime telemetry observers and OpenTelemetry provider bootstrap.
- `src/Template.Evaluation`: scenario evaluator, JSONL dataset loading, safety/reliability scoring, and machine-readable report output.
- `tests/Template.UnitTests`: deterministic safety and reliability unit tests.
- `tests/Template.IntegrationTests`: MCP-focused integration tests.

## GitHub guidance layout
- `.github/copilot-instructions.md`: repo-wide Copilot operating rules.
- `.github/instructions/*`: path-specific coding, testing, docs, and security constraints.
- `.github/agents/`: development-time Copilot instruction files for the Planner, Implementer, Reviewer and Verifier agents.
- `.github/skills/*`: reusable workflow playbooks for recurring engineering tasks.
- `.github/workflows/*`: CI, template checks, PR checks, and evaluation workflows.
- `.github/pull_request_template.md`: PR validation checklist.

## Starter skills
- `.github/skills/agent-framework-change-workflow/SKILL.md`
- `.github/skills/evaluation-workflow/SKILL.md`
- `.github/skills/safe-runtime-modification/SKILL.md`
- `.github/skills/development-agent-workflow/SKILL.md`: orchestrates the four development-time agents for structured change execution.

Skills define repeatable execution flow; instructions remain the mandatory policy layer.

## Prompting style for developers
- Preferred: ask for outcomes in plain English.
- Optional advanced mode: explicitly reference workflow IDs in `.github/skills/*`.

Good prompt examples:
- "Add an agent that can use MCP server X to answer user questions, with tests and docs updates."
- "Add an evaluation scenario for this failure mode and run build/test/host validation."
- "Update parser behavior while preserving safety boundaries and existing executor checks."

## Architecture docs
- `docs/architecture/system-overview.md`
- `docs/architecture/coding-standards.md`
- `docs/architecture/decision-log.md`
- `docs/architecture/ai-engineering-guidelines.md`

## Quick start
```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src/Template.Host/Template.Host.csproj
dotnet test -c Release
```

## Helper scripts
- `scripts/build.ps1`
- `scripts/test.ps1`
- `scripts/evals.ps1`
- `scripts/bootstrap-rename.ps1`

## Bootstrap renaming for new projects
Run the rename bootstrap immediately after creating a new repository from this template:
```powershell
./scripts/bootstrap-rename.ps1 -ProjectName Contoso.Agent
dotnet restore Contoso.Agent.sln
dotnet build Contoso.Agent.sln
```

This updates project names, namespaces, folder names, solution references, and docs/workflow references from `Template.*` to your chosen project identity.

## Concurrent workflow sample
The host sample follows the official Agent Framework concurrent orchestration pattern:
- two translator agents (French and Spanish)
- one concurrent workflow built by `AgentWorkflowBuilder.BuildConcurrent(...)`
- streaming events plus aggregated final output
- for each language, a parsed MCP-style `/tool ...` lookup executes through `ToolCommandParser` and `SafeToolExecutor` during execution
- lookup results seed each agent's translator identity (hardcoded mock values in template; real systems should use MCP servers)
- tool access is filtered by runtime context (mode/intent/risk) before selection
- `Template.Agents` worker implementations (`PlanningWorker`, `SafetyWorker`, `GeneralWorker`) build workflow guidance that is injected into the live translator workflow path.
- Standalone translator agents live under `src/Template.Agents/Agents/<AgentName>/` with a C# agent class and co-located `.instructions.md` file. Host loads these specs from `StandaloneTranslationAgentRegistry`.

## Runtime mode
- Cloud mode: set `AZURE_OPENAI_ENDPOINT` (and optionally `AZURE_OPENAI_DEPLOYMENT_NAME`) to run the live concurrent workflow.
- Local fallback mode: if no endpoint is set, host produces deterministic sample output so CI and local runs still succeed.

## OpenTelemetry visibility
- Host runtime bootstraps OpenTelemetry via `Template.Observability.OpenTelemetryRuntime.CreateDefault("Template.Host")`.
- Console exporter is enabled by default; view emitted spans/metrics directly in terminal output while running the host.
- Set `OTEL_CONSOLE_EXPORTER_ENABLED=false` to suppress console telemetry.
- `artifacts/evaluation-report.json` also records an `Observability` summary section for runtime verification.

## Evaluation artifacts
Host execution writes two artifacts:
- `artifacts/evaluation-report.json`: deterministic summary used by CI threshold checks.
- `artifacts/evaluation-library-report.json`: scenario-run report emitted via `Microsoft.Extensions.AI.Evaluation.Reporting`.

Dataset scenarios live under `evaluation-datasets/` as JSONL and include `Normal`, `Edge`, and `Adversarial` categories.
`Template.Scenarios/Scenarios/TranslateText/TranslateTextScenarioRunner` is the default runtime scenario entrypoint and composes `EvaluationDatasetLoader` with `AgentScenarioEvaluator`.

## Template extension points
- Add new worker roles in `src/Template.Agents`.
- Add local or remote tools in `src/Template.Tools` and register them in the host.
- Add MCP servers by implementing `IMcpServerClient` or wiring a real client.
- Add runtime scenarios in `src/Template.Scenarios/Scenarios/<ScenarioName>/` and keep scoring/report logic in `src/Template.Evaluation`.
