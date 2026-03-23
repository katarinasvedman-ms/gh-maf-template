# Decision Log

## ADR-001: Two-layer template design

- Date: 2026-03-19
- Decision: Separate GitHub guidance layer from product reference layer.
- Rationale: Keeps AI-assistance standards reusable across product implementations.

## ADR-002: Instructions-first, no custom-agent profiles

- Date: 2026-03-19
- Decision: Standardize on `.github/copilot-instructions.md` plus `.github/instructions/*.instructions.md` as the only guidance mechanism. Custom Copilot agent profiles (runtime prompt personas injected via copilot-instructions.md) are not supported. Development-time agents (Planner, Implementer, Reviewer, Verifier) under `.github/agents/` are instruction files, not custom agent profiles, and are a supported part of the template.
- Rationale: Removes mixed signaling, keeps one source of truth, and improves enforceability via workflows. The distinction between unsupported custom agent profiles and supported development-time instruction files prevents ambiguity introduced by the later addition of `.github/agents/`.

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

## ADR-006: Eval dataset as coverage artifact

- Date: 2026-03-23
- Decision: Eval dataset is a coverage artifact linked to tool contracts via ScenarioOrigin and LinkedContractRule.
- Rationale: Makes scenario provenance traceable to the tool contract conditions each scenario exercises, enabling gap detection.

## ADR-007: AgentContract mirrors ToolContract

- Date: 2026-03-23
- Decision: AgentContract mirrors ToolContract for agents, enabling task adherence evaluation and Foundry signal interpretation.
- Rationale: Provides structured metadata about agent capabilities, out-of-scope actions, and required tools, making agent behavior evaluable.

## ADR-008: Foundry eval payload serialization

- Date: 2026-03-23
- Decision: AgentTurnOutput serializes to Foundry eval payload format for ToolCallAccuracyEvaluator compatibility.
- Rationale: Enables the same execution output to feed both local evaluation and Foundry-hosted evaluation without format translation.

## ADR-009: Governance verification in PR checks

- Date: 2026-03-23
- Decision: PR checks include skill file reference validation and escalation surface documentation warnings.
- Rationale: Catches broken skill references early and reminds contributors to update DECISIONS.md when touching high-risk surfaces.

## ADR-010: Development-time agent layer

- Date: 2026-03-23
- Decision: Development-time agents (planner, implementer, reviewer, verifier) are Copilot instruction files under .github/agents/, not runtime C# objects. Workflow sequencing is defined in .github/skills/development-agent-workflow/SKILL.md.
- Rationale: Development agents are guidance-plane artifacts, not application runtime. Keeping them as markdown instruction files aligns with the repo's instructions-first model and avoids conflating development tooling with product code.

## ADR-011: Template validation permits .github/agents/*.md

- Date: 2026-03-23
- Decision: template-validation.yml accepts .github/agents/*.md files as first-class governance artifacts. Only non-.md files under .github/agents/ are rejected.
- Rationale: The previous validation step predated ADR-010 and blanket-rejected all files under .github/agents/, directly contradicting DECISIONS.md item 13. The replacement check preserves the safety intent (no runtime artifacts in the guidance layer) while permitting the legitimate markdown instruction files.

## ADR-012: EffectiveRiskLevel uses trailing consecutive failure count

- Date: 2026-03-23
- Decision: EffectiveRiskLevel counts only the unbroken run of failures at the end of PriorCallsInTurn. A success anywhere in the sequence resets the count. Escalation triggers when the trailing count reaches 2 or more.
- Rationale: The previous implementation used Count(r => !r.Success) which counted all failures regardless of position, violating the documented invariant ("consecutive failures within a turn escalate the risk level"). A sequence like [fail, success, fail] incorrectly escalated despite only 1 consecutive failure at the end. The fix aligns the implementation with the architectural invariant.

## ADR-013: Evaluation cleanup — planner dataset removal, coverage wiring, doc fixes

- Date: 2026-03-23
- Decision: (1) Removed evaluation-datasets/planner-scenarios.jsonl because the planner is a guidance-layer instruction file under .github/agents/, not a runtime IAgent — the evaluation layer is for runtime agents only. (2) Wired ContractCoverageValidator.Validate() into Program.cs so coverage gaps appear in artifacts/evaluation-report.json as an advisory CoverageReport section. (3) Updated README.md, PROJECT_STRUCTURE.md, and USAGE.md to reference .github/agents/ and the development-agent-workflow skill.
- Rationale: The planner dataset referenced repo.read_file which is not a registered runtime tool and had no execution path through AgentScenarioEvaluator. ContractCoverageValidator was implemented and tested but never called, making coverage gaps invisible to CI. Documentation did not reflect the .github/agents/ layer added in ADR-010.
