# Project Structure

## Template layers

- Guidance layer (single guidance plane):
	- `.github/copilot-instructions.md`
	- `.github/instructions/*`
	- `.github/agents/*`
	- `.github/skills/*`
	- `.github/workflows/*`
	- `.github/pull_request_template.md`
	- `docs/architecture/*`
	- `scripts/*`
- C# reference implementation layer:
	- `src/*`
	- `tests/*`
	- `artifacts/*`

## Layering
- `Template.Host` depends on all runtime libraries and owns framework-backed runtime composition.
- `Template.Agents` depends on `Template.Tools`, `Template.Mcp`, and `Template.Observability`.
- `Template.Mcp` depends on `Template.Tools` for normalized tool contracts.
- `Template.Evaluation` depends on `Template.Agents` for scenario execution.
- Tests depend on runtime projects only.

## Design rules
- Keep domain logic in class libraries, not in the host entrypoint.
- Keep tool safety checks centralized in `Template.Tools`.
- Keep MCP transport details isolated to `Template.Mcp` with multi-server registration handled by catalogs.
- Keep telemetry APIs vendor-neutral in `Template.Observability` with provider bootstrap at the host edge.
- Keep evaluation deterministic, scenario-oriented, and CI-artifact-friendly.
