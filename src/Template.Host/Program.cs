using Template.Agents;
using System.Text.Json;
using Template.Evaluation;
using Template.Scenarios.TranslateText;
using Template.Observability;
using Template.Tools;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var scenarioName = Environment.GetEnvironmentVariable("SCENARIO") ?? "TranslateText";
var standaloneAgentRegistry = new StandaloneTranslationAgentRegistry();
var datasetPath = TranslateTextScenarioRunner.GetDefaultDatasetPath();
using var openTelemetryRuntime = OpenTelemetryRuntime.CreateDefault("Template.Host");

var parser = new ToolCommandParser();
var runtime = BuildSafeToolRuntime(openTelemetryRuntime.Observer);
if (!string.Equals(scenarioName, "TranslateText", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"Unsupported scenario '{scenarioName}'. Supported scenarios: TranslateText");
}

var scenarioAgent = new TranslateTextScenarioAgent(endpoint, deploymentName, standaloneAgentRegistry, parser, runtime);
var scenarioRunner = new TranslateTextScenarioRunner();

var scenarioRunResult = await scenarioRunner.RunAsync(scenarioAgent, datasetPath, CancellationToken.None);

Console.WriteLine("===== TranslateText Scenario Results =====");
foreach (var scenario in scenarioRunResult.Report.Scenarios)
{
    Console.WriteLine($"scenario={scenario.ScenarioName}, category={scenario.Category}, passed={scenario.Passed}, latencyMs={scenario.LatencyMs:F2}");
    Console.WriteLine($"response={scenario.Response}");
}

foreach (var lookup in scenarioAgent.TranslatorToolCalls)
{
    Console.WriteLine($"translator profile lookup: {JsonSerializer.Serialize(lookup)}");
}

var agentWorkflowContext = scenarioAgent.LatestWorkflowContext ?? new AgentWorkflowContextResult(string.Empty, []);
Console.WriteLine("===== Template.Agents Workflow Context =====");
foreach (var entry in agentWorkflowContext.Workers)
{
    Console.WriteLine($"worker={entry.Worker}, prompt={entry.Prompt}, response={entry.Response}");
}

Directory.CreateDirectory("artifacts");

var evaluationScenarios = await EvaluationDatasetLoader.LoadJsonlAsync(datasetPath, CancellationToken.None);
var coverageReport = ContractCoverageValidator.Validate(runtime.Registry.List(), evaluationScenarios);

if (coverageReport.HasGaps)
{
    Console.WriteLine("WARNING: Contract coverage gaps detected. See CoverageReport in artifacts/evaluation-report.json for details.");
}

var translatorToolCalls = scenarioAgent.TranslatorToolCalls;
var toolCallsSucceeded = translatorToolCalls.Count > 0 && translatorToolCalls.All(call => call.ExecutionSuccess);
var failedToolCalls = translatorToolCalls.Count(call => !call.ExecutionSuccess);
var report = scenarioRunResult.Report;

var evaluationSummary = new
{
    Scenario = scenarioRunResult.Scenario,
    Total = report.Total,
    Passed = report.Passed,
    PassRate = report.PassRate,
    AverageLatencyMs = report.AverageLatencyMs,
    ToolUsageAccuracy = report.ToolUsageAccuracy,
    SafetyViolationCount = failedToolCalls,
    ReliabilityScore = report.ReliabilityScore,
    ToolCallsSucceeded = toolCallsSucceeded,
    TranslatorToolCalls = translatorToolCalls,
    AgentWorkflowContext = agentWorkflowContext,
    Observability = new
    {
        Enabled = true,
        Observer = runtime.Observer.GetType().Name
    },
    Dataset = scenarioRunResult.Dataset,
    CoverageReport = new
    {
        HasGaps = coverageReport.HasGaps,
        UncoveredUseWhenConditions = coverageReport.UncoveredUseWhenConditions,
        UncoveredDoNotUseWhenConditions = coverageReport.UncoveredDoNotUseWhenConditions
    },
    Categories = report.Categories,
    Scenarios = report.Scenarios
};

await File.WriteAllTextAsync(
    Path.Combine("artifacts", "evaluation-report.json"),
    JsonSerializer.Serialize(evaluationSummary, new JsonSerializerOptions { WriteIndented = true }),
    CancellationToken.None);

static ToolRuntime BuildSafeToolRuntime(OpenTelemetryRuntimeObserver observer)
{
    var registry = new InMemoryToolRegistry();
    var shouldFailTransiently = true;

    registry.Register(new LocalFunctionTool(
        new ToolDefinition(
            Name: "mcp.lookup_official_translator",
            Description: "Looks up an official translator profile for a target language.",
            RequiredArguments: ["language"],
            Sensitivity: ToolSensitivity.High,
            Timeout: TimeSpan.FromSeconds(2),
            Contract: new ToolContract(
                WhatItDoes: "Resolves the representative translator profile name for a language.",
                UseWhen: ["An agent needs language-specific translator identity metadata for prompt construction."],
                DoNotUseWhen: ["The task does not involve translation or language profile enrichment."],
                InputDescriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["language"] = "Target natural language name, for example French or Spanish."
                },
                OutputDescription: "Single translator display name associated with the language.",
                Constraints: ["Returns a hardcoded mock value in template mode."],
                SideEffects: ["No external side effects in template mode."],
                AllowedModes: [ToolExecutionMode.Translation, ToolExecutionMode.Evaluation],
                MaxRiskLevel: ToolRiskLevel.Elevated)),
        request =>
        {
            if (shouldFailTransiently)
            {
                shouldFailTransiently = false;
                return Task.FromResult(ToolCallResult.Failed(
                    request.ToolName,
                    ToolErrorCode.TransientFailure,
                    "Simulated transient MCP directory warm-up delay.",
                    1,
                    TimeSpan.Zero));
            }

            request.Arguments.TryGetValue("language", out var language);
            var output = GetDefaultTranslatorName(language ?? string.Empty);

            return Task.FromResult(ToolCallResult.Ok(request.ToolName, output, 1, TimeSpan.Zero));
        }));

    var policy = new AllowListToolPolicy(["mcp.lookup_official_translator"]);
    var executor = new SafeToolExecutor(
        registry,
        policy,
        new AutoApproveGate(approveHighSensitivity: true),
        observer: observer,
        retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(30)));

    return new ToolRuntime(registry, policy, executor, observer);
}

static string GetDefaultTranslatorName(string language) => language.ToLowerInvariant() switch
{
    "french" => "Claire Dubois",
    "spanish" => "Alejandro Garcia",
    _ => "Jordan Lee"
};

