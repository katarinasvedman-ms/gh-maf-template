---
applyTo: "docs/**/*,**/*.md"
---

## Documentation rules

- Update architecture docs when boundaries or contracts change.
- Update usage docs when command behavior or setup steps change.
- Prefer concrete, operational language over aspirational statements.

## Required doc updates by runtime area

- `src/Template.Agents/*` (routing/flow):
	- `docs/architecture/system-overview.md`
	- `docs/architecture/coding-standards.md` if invariants change

- `src/Template.Tools/*` (parser/safety contracts):
	- `docs/architecture/system-overview.md`
	- `docs/architecture/coding-standards.md`
	- `USAGE.md` when command syntax/behavior changes

- `src/Template.Mcp/*` (integration contracts):
	- `docs/architecture/system-overview.md`
	- `README.md` or `USAGE.md` when operator-facing behavior changes

- `src/Template.Evaluation/*` (metrics/artifacts/thresholds):
	- `README.md`
	- `DECISIONS.md` and `docs/architecture/decision-log.md` for policy-level evaluation changes

- `.github/workflows/*` or `scripts/*`:
	- `SETUP.md`
	- `README.md` when CI/runtime expectations visible to users change

## Examples

Acceptable:
- Change timeout semantics in `SafeToolExecutor` and update `system-overview.md`, `coding-standards.md`, and `USAGE.md` where behavior is described.

Disallowed:
- Modify parser behavior and only update tests, leaving docs/examples stale.
