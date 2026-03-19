# Safe Runtime Modification

Use this skill for high-risk runtime changes in parser and executor surfaces.

## Typical user asks (plain language)

- "Change parser escaping behavior and keep safety checks intact."
- "Update tool timeout/retry behavior and add the right tests."
- "Modify executor behavior without bypassing policy or approval boundaries."

Users do not need to mention this workflow ID explicitly.

## Use for

- `src/Template.Tools/ToolCommandParser.cs` changes.
- `src/Template.Tools/SafeToolExecutor.cs` changes.
- Tool-policy and approval-boundary changes.

## Do not use for

- Refactors that blur ownership across Host/Agents/Tools boundaries.
- Any change that bypasses structured failures or approval boundaries.

## Required inputs

- Exact behavior change request.
- Failure-mode expectations (parse error, policy denied, approval denied, timeout, retry).
- Test cases to prove correctness.

## Advanced invocation (optional)

- `safe-runtime-modification`

## Execution flow

1. Confirm invariants in:
   - `./.github/instructions/csharp.instructions.md`
   - `./.github/instructions/security.instructions.md`
   - `docs/architecture/system-overview.md`
   - `docs/architecture/coding-standards.md`
2. Implement minimal code change in parser/executor only.
3. Add focused tests for changed behavior:
   - parser syntax/escaping/validation paths
   - policy/approval-denied non-execution paths
   - timeout and bounded retry behavior
4. Update docs when behavior contracts change:
   - `USAGE.md` (command syntax / operator behavior)
   - `docs/architecture/system-overview.md` and `docs/architecture/coding-standards.md`
5. Validate end-to-end:
   - `dotnet build <SolutionName>.sln`
   - `dotnet test <SolutionName>.sln`

## Completion checks

- No direct tool execution path outside executor.
- `ToolErrorCode` mapping and structured failures preserved.
- Sensitive payload logging remains redacted.
- Tests cover changed risk path.

## Common failure modes

- Parser grammar changes without usage doc updates.
- Retry behavior changes without deterministic timeout tests.
- Approval/policy denial paths that still execute side effects.
