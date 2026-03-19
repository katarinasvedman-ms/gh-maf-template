# AI Engineering Guidelines

## Guidance model

This template is instructions-first and does not use custom-agent profile files.

- Repo-wide operating contract: `.github/copilot-instructions.md`
- Path-specific constraints: `.github/instructions/*.instructions.md`
- Workflow playbooks: `.github/skills/*`

The expected behavior is that implementation, review, and validation decisions are driven by instructions plus architecture docs. Skills are execution accelerators and do not override instruction policy.

## Workflow model

- Plan first for non-trivial changes.
- Implement with minimal diff and explicit boundary preservation.
- Review for correctness/regression risk first, then safety, tests, and docs coverage.
- Verify with command evidence before completion.

## Validation expectations

- Build for every code change.
- Test for behavior changes.
- Host run for runtime/artifact-affecting changes.
- Keep outputs reproducible and deterministic where possible.

## Examples

Acceptable:
- Parser contract change with parser tests, usage doc update, and architecture-flow update.

Disallowed:
- Direct tool invocation path added in orchestrator that bypasses executor policy/approval.
