# Development Agent Workflow

Orchestrates the four development-time agents (planner, implementer, reviewer, verifier) for structured change execution.

## Domain skills

The Implementer must select the appropriate domain skill based on the plan:

| If the plan touches...                          | Implementer uses...                    |
|-------------------------------------------------|----------------------------------------|
| src/Template.Agents/*, src/Template.Host/*      | agent-framework-change-workflow        |
| src/Template.Tools/SafeToolExecutor.cs or       |                                        |
| src/Template.Tools/ToolCommandParser.cs         | safe-runtime-modification              |
| src/Template.Evaluation/*, evaluation-datasets/ | evaluation-workflow                    |
| .github/agents/*, .github/skills/*              | no domain skill — follow               |
|                                                 | copilot-instructions.md directly       |

The Planner identifies which domain skill applies and includes it in the plan.
The Implementer reads the domain skill before writing any code.
The Reviewer checks that the domain skill's completion checks are satisfied.

## When to use

- Multi-step changes that touch escalation-required surfaces.
- Changes that span multiple architecture boundaries.
- Any change where the user requests a structured development workflow.

Users can invoke this by asking for a "planned change", "structured implementation", or referencing the development agent workflow.

## Agent sequence

The four agents execute in strict order. Each agent's output is the next agent's input.

```
Planner → Implementer → Reviewer → Verifier
                              ↑          |
                              └──────────┘ (on failure)
```

### 1. Planner (`.github/agents/planner.md`)

- Reads architecture docs and the change request.
- Produces a structured, dependency-ordered plan with file references.
- Identifies escalation surfaces and required doc updates.
- **Does not** write code or make architectural decisions.

### 2. Implementer (`.github/agents/implementer.md`)

- Executes each plan step with the smallest safe diff.
- Follows the plan exactly — no reordering, no skipped steps.
- **Does not** plan, review, or verify.

### 3. Reviewer (`.github/agents/reviewer.md`)

- Reviews the implementation for correctness, safety, missing tests, and missing docs.
- Findings are severity-ordered with file/line evidence.
- **Does not** write code or run commands.

### 4. Verifier (`.github/agents/verifier.md`)

- Runs the completion criteria checklist: build, test, host run, stale reference check.
- Reports pass/fail per criterion.
- On failure, hands back to Implementer with evidence.
- **Does not** modify files.

## Handoff rules

- Each agent reads its own `.github/agents/<name>.md` for operating constraints.
- Each agent reads `.github/copilot-instructions.md` and `docs/architecture/system-overview.md` before acting.
- The Verifier's checklist items map 1:1 to the completion criteria in `.github/copilot-instructions.md`.
- If the Verifier reports a failure, the loop restarts at the Implementer (not the Planner) unless the failure requires re-planning.

## Do not use for

- Single-line fixes with no escalation surface impact.
- Documentation-only changes.
- Changes that do not touch `src/` or `tests/`.
