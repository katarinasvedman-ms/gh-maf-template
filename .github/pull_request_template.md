## Summary

- What changed and why?

## Scope

- [ ] Runtime code (`src/`)
- [ ] Tests (`tests/`)
- [ ] Workflows (`.github/workflows/`)
- [ ] Documentation (`docs/`, root markdown files)

## Validation

- [ ] `dotnet restore gh-maf-template.sln`
- [ ] `dotnet build gh-maf-template.sln`
- [ ] `dotnet test gh-maf-template.sln` (if behavior changed)
- [ ] `dotnet run --project src/Template.Host/Template.Host.csproj` (if runtime changed)
- [ ] `artifacts/evaluation-report.json` generated when required

## Safety and reliability checklist

- [ ] No bypass of policy or approval gates
- [ ] Structured error behavior preserved
- [ ] Retry and timeout behavior remains bounded
- [ ] Correlation ID propagation preserved

## Docs

- [ ] Docs updated for behavior/architecture/workflow changes
- [ ] Not needed (explain why)
