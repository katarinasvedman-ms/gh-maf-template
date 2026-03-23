# Setup

This template is instructions-first: `.github/copilot-instructions.md` and `.github/instructions/*.instructions.md` are the supported guidance mechanism, and the C# runtime is the reference implementation.

## Prerequisites
- .NET SDK 10.0+
- GitHub Actions enabled for CI checks

## Optional Azure OpenAI runtime variables
Set these when you want the host to run the live Agent Framework concurrent workflow against Azure OpenAI:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT_NAME` (optional, defaults to `gpt-4o-mini`)

If `AZURE_OPENAI_ENDPOINT` is not set, the host runs a deterministic local fallback output.

## Local bootstrap
```powershell
dotnet restore
dotnet build
```

## Rename template identity (recommended first step)
```powershell
./scripts/bootstrap-rename.ps1 -ProjectName Contoso.Agent
dotnet restore Contoso.Agent.sln
dotnet build Contoso.Agent.sln
```

This converts `Template.*` project and namespace identities to your chosen name across `src/`, `tests/`, solution references, and docs.

Or use helper scripts:
```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/evals.ps1
```

## Use repository skills in Copilot Chat
This template includes starter skills under `.github/skills/*` for repeatable workflows.

Recommended usage pattern:
1. Describe the outcome in plain language.
2. Include key constraints (for example: keep safety boundaries, add tests, update docs).
3. Ask for validation evidence (`dotnet build`, `dotnet test`, and host run when runtime behavior changes).
4. Verify docs/tests updates before completion.

Example prompts (first-time user friendly):
- "I need to add an agent that connects to MCP server X and answers user questions. Keep existing safety boundaries, add tests, and update docs."
- "Add a new evaluation scenario for tool timeout handling and update whatever tests/docs are required."
- "Change parser escaping behavior without bypassing executor policy/approval checks, then run build and tests."

Advanced (optional):
- You can explicitly ask for workflow IDs when needed:
  - `agent-framework-change-workflow`
  - `evaluation-workflow`
  - `safe-runtime-modification`
- This is not required for normal usage.

## Run host demo
```powershell
dotnet run --project src/Template.Host/Template.Host.csproj
```

## Run tests
```powershell
dotnet test
```

## PR check steps
The `.github/workflows/pr-checks.yml` workflow includes two governance verification steps:
- **Validate skill file references**: Checks all `SKILL.md` files under `.github/skills/` for broken path references to `src/`, `tests/`, or `.github/` files. Fails the check if any referenced path does not exist.
- **Check escalation surface documentation**: Warns (advisory, non-blocking) when escalation-required surfaces (`Program.cs`, `ToolCommandParser.cs`, `SafeToolExecutor.cs`, `Template.Evaluation/`, `.github/workflows/`) are modified without a corresponding `DECISIONS.md` update.

## Extending beyond the sample
1. Add more specialized agents by following the pattern in `src/Template.Host/Program.cs`.
2. Switch from two translators to additional agents by expanding the concurrent workflow inputs.
3. Integrate your own tools and safety boundaries using `Template.Tools` and `Template.Mcp` projects when needed.

## Observability defaults to keep
- Keep OpenTelemetry provider bootstrap enabled.
- Keep correlation IDs on tool results and spans.
- Control console exporter with `OTEL_CONSOLE_EXPORTER_ENABLED` (`true` by default).
