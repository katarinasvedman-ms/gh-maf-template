dotnet restore gh-maf-template.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build gh-maf-template.sln
exit $LASTEXITCODE
