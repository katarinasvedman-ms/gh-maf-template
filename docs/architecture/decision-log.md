# Decision Log

## ADR-001: Two-layer template design

- Date: 2026-03-19
- Decision: Separate GitHub guidance layer from product reference layer.
- Rationale: Keeps AI-assistance standards reusable across product implementations.

## ADR-002: Instructions-first, no custom-agent profiles

- Date: 2026-03-19
- Decision: Standardize on `.github/copilot-instructions.md` plus `.github/instructions/*.instructions.md` as the only guidance mechanism.
- Rationale: Removes mixed signaling, keeps one source of truth, and improves enforceability via workflows.

## ADR-003: Policy-first tool execution

- Date: 2026-03-19
- Decision: Keep policy and approval gates as non-optional runtime boundaries.
- Rationale: Prevents unsafe execution behavior and preserves auditability.

## ADR-004: Structured failure codes

- Date: 2026-03-19
- Decision: Preserve `ToolErrorCode` taxonomy for all tool failures.
- Rationale: Supports robust handling, diagnostics, and quality evaluation.

## ADR-005: Evaluation artifacts in CI

- Date: 2026-03-19
- Decision: Require evaluation artifacts for runtime validation flows.
- Rationale: Keeps reliability and safety checks measurable in automation.
