$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$hostCsproj = Join-Path $root 'src/Template.Host/Template.Host.csproj'
$reportPath = Join-Path $root 'artifacts/evaluation-report.json'

function Ensure {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$hostText = Get-Content -Path $hostCsproj -Raw -Encoding UTF8
$requiredRefs = @(
    '..\Template.Agents\Template.Agents.csproj',
    '..\Template.Evaluation\Template.Evaluation.csproj',
    '..\Template.Observability\Template.Observability.csproj',
    '..\Template.Scenarios\Template.Scenarios.csproj',
    '..\Template.Tools\Template.Tools.csproj'
)

foreach ($reference in $requiredRefs) {
    Ensure -Condition $hostText.Contains($reference) -Message "Missing required host project reference: $reference"
}

Ensure -Condition (Test-Path -Path $reportPath) -Message "Missing evaluation report: $reportPath"
$report = Get-Content -Path $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json

$requiredReportFields = @(
    'Scenario',
    'TranslatorToolCalls',
    'AgentWorkflowContext',
    'Observability',
    'Dataset',
    'ToolCallsSucceeded'
)

foreach ($field in $requiredReportFields) {
    Ensure -Condition ($null -ne $report.PSObject.Properties[$field]) -Message "Missing required report field: $field"
}

Ensure -Condition ([bool]$report.Observability.Enabled) -Message 'Observability.Enabled must be true'
Ensure -Condition ($null -ne $report.AgentWorkflowContext.PSObject.Properties['Workers']) -Message 'AgentWorkflowContext.Workers is required'
Ensure -Condition ($null -ne $report.Dataset.PSObject.Properties['Loaded']) -Message 'Dataset.Loaded is required'

Write-Host 'Runtime contract validation passed.'
