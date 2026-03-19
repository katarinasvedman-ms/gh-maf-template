# Evaluation Workflow

Use this skill to change evaluation scenarios, scoring logic, or evaluation artifacts.

## Typical user asks (plain language)

- "Add an evaluation scenario for this failure mode and update tests/docs."
- "Adjust evaluation thresholds and ensure CI still validates artifacts."
- "Update evaluation reporting for this new runtime behavior."

Users do not need to mention this workflow ID explicitly.

## Use for

- Editing `src/Template.Evaluation/*`.
- Updating evaluation thresholds or scenario logic.
- Adjusting CI checks that depend on evaluation artifacts.

## Do not use for

- Runtime behavior changes without corresponding runtime tests.
- Silent changes to evaluation policy without decision documentation.

## Required inputs

- Evaluation goal (what quality/safety/reliability behavior must be measured).
- Success criteria and thresholds.
- Expected artifact impact.

## Advanced invocation (optional)

- `evaluation-workflow`

## Execution flow

1. Review current evaluation contracts and artifact expectations in:
   - `src/Template.Evaluation/*`
   - `.github/workflows/evals.yml`
   - `.github/workflows/template-validation.yml`
2. Apply minimal changes to scenario evaluator and any supporting contracts.
3. Update tests that assert evaluation outcomes.
4. Update docs:
   - `README.md` for user-visible eval behavior
   - `DECISIONS.md` and `docs/architecture/decision-log.md` for policy-level changes
5. Run validation:
   - `dotnet build <SolutionName>.sln`
   - `dotnet test <SolutionName>.sln`
   - `dotnet run --project src/<ProjectName>.Host/<ProjectName>.Host.csproj`
6. Confirm artifacts are produced and shaped as expected:
   - `artifacts/evaluation-report.json`
   - `artifacts/evaluation-library-report.json`

## Completion checks

- Scenario and threshold changes are explicit and tested.
- Artifact schema/fields remain consumable by CI.
- Decision docs updated for policy-level changes.

## Common failure modes

- Updating evaluation code without workflow/docs updates.
- Changing artifact contract without corresponding CI alignment.
- Treating flaky runtime behavior as acceptable evaluation output.
