dotnet run --project src/Template.Host/Template.Host.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path "artifacts/evaluation-report.json")) { Write-Error "Missing artifacts/evaluation-report.json"; exit 1 }
if (-not (Test-Path "artifacts/evaluation-library-report.json")) { Write-Error "Missing artifacts/evaluation-library-report.json"; exit 1 }

Write-Host "Evaluation artifacts generated successfully."
