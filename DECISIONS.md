# Decisions

1. Template architecture is two-layer: guidance-first plus product reference implementation.
2. Guidance standard is repo-wide plus path-specific instructions. Custom Copilot agent profiles (runtime prompt personas injected via copilot-instructions.md) are not part of the supported model. Development-time agents (Planner, Implementer, Reviewer, Verifier) under .github/agents/ are instruction files, not custom agent profiles, and are a supported part of the template.
3. Runtime target is local-first with .NET 10.0 baseline.
4. Tool execution is policy-first with explicit allow-listing.
5. High-sensitivity tool calls require approval by default.
6. MCP tools are normalized into common tool contracts.
7. Observability uses OpenTelemetry-compatible primitives without vendor lock-in.
8. Evaluation is built into the template as a first-class concern.
9. Eval dataset is a coverage artifact linked to tool contracts via ScenarioOrigin and LinkedContractRule.
10. AgentContract mirrors ToolContract for agents, enabling task adherence evaluation and Foundry signal interpretation.
11. AgentTurnOutput serializes to Foundry eval payload format for ToolCallAccuracyEvaluator compatibility.
12. PR checks include governance verification: skill file reference validation and escalation surface documentation warnings.
13. Development-time agents (planner, implementer, reviewer, verifier) are Copilot instruction files under .github/agents/, not runtime C# objects. Workflow sequencing is defined in .github/skills/development-agent-workflow/SKILL.md.
14. template-validation.yml permits .github/agents/*.md files as first-class governance artifacts; only non-.md files under .github/agents/ are rejected.
15. EffectiveRiskLevel counts only trailing consecutive failures in PriorCallsInTurn (the unbroken run at the end); a success anywhere in the sequence resets the count. This matches the documented invariant in system-overview.md.
16. Evaluation datasets are for runtime agents only. planner-scenarios.jsonl was removed because the planner is a guidance-layer instruction file with no runtime IAgent. ContractCoverageValidator is wired into the host evaluation artifact. README.md, PROJECT_STRUCTURE.md, and USAGE.md now reference .github/agents/ correctly.
