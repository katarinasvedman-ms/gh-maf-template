param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectName,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($ProjectName -notmatch '^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$') {
    throw "ProjectName must be a valid dotted identifier (example: Contoso.Agent)."
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

$markerTokens = @(
    "Template.Host",
    "src/Template.Host/Template.Host.csproj",
    "gh-maf-template.sln"
)

$containsTemplateMarkers = $false
foreach ($token in $markerTokens) {
    if (Get-ChildItem -Path $root -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|artifacts)\\' } |
        Select-String -SimpleMatch -Pattern $token -Quiet) {
        $containsTemplateMarkers = $true
        break
    }
}

if (-not $containsTemplateMarkers -and -not $Force) {
    throw "Template markers were not found. If this repo was already renamed, rerun with -Force to continue."
}

$runtimeProjects = @("Host", "Agents", "Tools", "Mcp", "Observability", "Evaluation")
$testProjects = @("UnitTests", "IntegrationTests")

$replacements = [ordered]@{}

foreach ($name in $runtimeProjects) {
    $old = "Template.$name"
    $new = "$ProjectName.$name"
    $replacements[$old] = $new
    $replacements["src/$old/$old.csproj"] = "src/$new/$new.csproj"
    $replacements["src\\$old\\$old.csproj"] = "src\\$new\\$new.csproj"
    $replacements["src/$old"] = "src/$new"
    $replacements["src\\$old"] = "src\\$new"
}

foreach ($name in $testProjects) {
    $old = "Template.$name"
    $new = "$ProjectName.$name"
    $replacements[$old] = $new
    $replacements["tests/$old/$old.csproj"] = "tests/$new/$new.csproj"
    $replacements["tests\\$old\\$old.csproj"] = "tests\\$new\\$new.csproj"
    $replacements["tests/$old"] = "tests/$new"
    $replacements["tests\\$old"] = "tests\\$new"
}

$replacements["gh-maf-template.sln"] = "$ProjectName.sln"

$allowedExtensions = @(
    ".cs", ".csproj", ".sln", ".md", ".yml", ".yaml", ".json", ".ps1", ".props", ".targets"
)

$files = Get-ChildItem -Path $root -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj|\.git|artifacts)\\' -and
        $allowedExtensions -contains $_.Extension.ToLowerInvariant()
    }

$changedFiles = 0
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $updated = $content

    foreach ($entry in $replacements.GetEnumerator()) {
        $updated = $updated.Replace($entry.Key, $entry.Value)
    }

    if ($updated -ne $content) {
        Set-Content -Path $file.FullName -Value $updated -Encoding UTF8
        $changedFiles++
    }
}

foreach ($name in $runtimeProjects) {
    $oldDir = Join-Path $root "src/Template.$name"
    $newDir = Join-Path $root "src/$ProjectName.$name"
    if (Test-Path -LiteralPath $oldDir) {
        Move-Item -LiteralPath $oldDir -Destination $newDir
    }

    $oldProjectFile = Join-Path $newDir "Template.$name.csproj"
    $newProjectFile = Join-Path $newDir "$ProjectName.$name.csproj"
    if (Test-Path -LiteralPath $oldProjectFile) {
        Move-Item -LiteralPath $oldProjectFile -Destination $newProjectFile
    }
}

foreach ($name in $testProjects) {
    $oldDir = Join-Path $root "tests/Template.$name"
    $newDir = Join-Path $root "tests/$ProjectName.$name"
    if (Test-Path -LiteralPath $oldDir) {
        Move-Item -LiteralPath $oldDir -Destination $newDir
    }

    $oldProjectFile = Join-Path $newDir "Template.$name.csproj"
    $newProjectFile = Join-Path $newDir "$ProjectName.$name.csproj"
    if (Test-Path -LiteralPath $oldProjectFile) {
        Move-Item -LiteralPath $oldProjectFile -Destination $newProjectFile
    }
}

$oldSolutionPath = Join-Path $root "gh-maf-template.sln"
$newSolutionPath = Join-Path $root "$ProjectName.sln"
if (Test-Path -LiteralPath $oldSolutionPath) {
    if (Test-Path -LiteralPath $newSolutionPath) {
        throw "Cannot rename solution: $ProjectName.sln already exists."
    }

    Move-Item -LiteralPath $oldSolutionPath -Destination $newSolutionPath
}

Write-Host "Bootstrap rename complete."
Write-Host "Updated files: $changedFiles"
Write-Host "New solution: $ProjectName.sln"
Write-Host "Next steps:"
Write-Host "  dotnet restore $ProjectName.sln"
Write-Host "  dotnet build $ProjectName.sln"
