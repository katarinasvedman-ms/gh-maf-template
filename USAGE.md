# Usage

## Concurrent workflow model
- The host creates two translator agents (French and Spanish).
- Before each agent is created, the host calls an MCP-style tool (`mcp.lookup_official_translator`) via `/tool ...` parsing and `SafeToolExecutor`.
- Tool registration requires a structured contract (what/when/not-when/input/output/constraints/side-effects).
- Tool availability is filtered by runtime context (mode + risk level) before selection.
- `Template.Agents` workers generate workflow guidance that is injected into translation agent instructions and user prompt context.
- Standalone translation agent definitions are sourced from `src/Template.Agents/Agents/<AgentName>/` folders (C# + `.instructions.md`) through `StandaloneTranslationAgentRegistry`.
- The tool result (translator name) is injected into each agent's runtime instruction.
- `AgentWorkflowBuilder.BuildConcurrent(...)` runs both agents in parallel for the same user message.
- The runtime streams per-agent updates and then emits aggregated final output.
- The host also runs `Template.Agents` workers to demonstrate `IAgentWorker` contract-based handling in the same execution.

### Examples
- User input: `Hello, world!`
- Concurrent outputs: translated responses from both agents.

## Runtime modes
- Cloud mode: set `AZURE_OPENAI_ENDPOINT` (and optional `AZURE_OPENAI_DEPLOYMENT_NAME`) to execute against Azure OpenAI.
- Local fallback mode: without endpoint, host returns deterministic sample translations to keep local/CI flows stable.

## Evaluation flow
`Template.Host` runs the concurrent workflow sample and writes artifacts.
It writes:
- `artifacts/evaluation-report.json` with:
- summary fields consumed by CI threshold checks, including per-language tool lookup execution details
- `AgentWorkflowContext` entries from `Template.Agents` runtime execution integrated into workflow guidance
- dataset summary fields loaded from `evaluation-datasets/translator-scenarios.jsonl` (`Normal`/`Edge`/`Adversarial` counts)
- `artifacts/evaluation-library-report.json` with aggregated workflow message output.

## Dataset-driven evaluation
- Dataset files are JSONL under `evaluation-datasets/`.
- Runtime orchestration scenarios live under `src/Template.Scenarios/Scenarios/`.
- The default translation flow is `src/Template.Scenarios/Scenarios/TranslateText/`, where `TranslateTextScenarioRunner` loads datasets through `EvaluationDatasetLoader` and executes checks through `AgentScenarioEvaluator`.
- `Template.Evaluation` remains the evaluation layer (dataset parsing, deterministic scoring, and report generation).
- Supported scenario categories: `Normal`, `Edge`, `Adversarial`.

CI reads this artifact and fails when thresholds are violated.
