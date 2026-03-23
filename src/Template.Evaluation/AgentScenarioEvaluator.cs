using Template.Agents;
using Template.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Json;
using System.Text.Json;

namespace Template.Evaluation;

public enum EvaluationScenarioCategory
{
    Normal,
    Edge,
    Adversarial
}

public enum ScenarioOrigin
{
    Manual,
    DerivedFromProductionTrace,
    DerivedFromContractCondition
}

public sealed record EvaluationScenario(
    string Name,
    string Prompt,
    string ExpectedSubstring,
    EvaluationScenarioCategory Category = EvaluationScenarioCategory.Normal,
    string? ExpectedTool = null,
    bool? ExpectedSuccess = null,
    string? ExpectedErrorCode = null,
    bool? ExpectApprovalRequired = null,
    double? MaxLatencyMs = null,
    ScenarioOrigin Origin = ScenarioOrigin.Manual,
    string? LinkedContractRule = null);

public sealed record ScenarioResult(
    string ScenarioName,
    EvaluationScenarioCategory Category,
    bool Passed,
    double LatencyMs,
    string Response,
    string? FailureReason,
    IReadOnlyList<string> UsedTools,
    IReadOnlyList<string> ErrorCodes,
    bool ApprovalObserved,
    int MaxAttemptCount);

public sealed record CategorySummary(
    EvaluationScenarioCategory Category,
    int Total,
    int Passed,
    double PassRate,
    int SafetyViolationCount,
    double ToolUsageAccuracy);

public sealed record EvaluationReport(
    int Total,
    int Passed,
    double PassRate,
    double AverageLatencyMs,
    double ToolUsageAccuracy,
    int SafetyViolationCount,
    double ReliabilityScore,
    IReadOnlyList<CategorySummary> Categories,
    IReadOnlyList<ScenarioResult> Scenarios)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class AgentScenarioEvaluator
{
    private const string ContractPassMetric = "contract_pass";
    private const string ToolCorrectMetric = "tool_correct";
    private const string SafetyViolationMetric = "safety_violation";
    private const string LatencyMsMetric = "latency_ms";
    private const string RetryWithinBudgetMetric = "retry_within_budget";
    private const string FailureReasonMetric = "failure_reason";
    private const string DeterministicContextName = "deterministic.contract_context";

    private static readonly IEvaluator DeterministicEvaluator = new CompositeEvaluator(new DeterministicContractEvaluator());

    public async Task<EvaluationReport> EvaluateAsync(
        IAgent agent,
        IEnumerable<EvaluationScenario> scenarios,
        CancellationToken cancellationToken)
    {
        var scenarioList = scenarios.ToList();
        var all = new List<ScenarioResult>();
        var scenarioRunResults = new List<ScenarioRunResult>();
        var executionName = $"local-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        foreach (var scenario in scenarioList)
        {
            var correlationId = $"eval-{Guid.NewGuid():N}";
            var output = await agent.RunAsync(new AgentTurnInput(correlationId, scenario.Prompt), cancellationToken).ConfigureAwait(false);
            var approvalObserved = output.ToolResults.Any(result => result.ApprovalRequired);

            var errorCodes = output.ToolResults
                .Where(result => result.Failure is not null)
                .Select(result => result.Failure!.Code.ToString())
                .ToArray();

            var maxAttemptCount = output.ToolResults.Count == 0 ? 0 : output.ToolResults.Max(result => result.AttemptCount);

            var payload = new DeterministicMetricPayload(
                scenario.ExpectedSubstring,
                scenario.ExpectedTool,
                scenario.ExpectedSuccess,
                scenario.ExpectedErrorCode,
                scenario.ExpectApprovalRequired,
                scenario.MaxLatencyMs,
                output.ResponseText,
                output.InvokedTools,
                output.Success,
                output.ErrorCode,
                approvalObserved,
                output.Duration.TotalMilliseconds,
                maxAttemptCount);

            var evaluationContext = new DeterministicEvaluationContext(payload);

            var messages = new[] { new ChatMessage(ChatRole.User, scenario.Prompt) };
            var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, output.ResponseText));

            var evaluationResult = await DeterministicEvaluator.EvaluateAsync(
                messages,
                modelResponse,
                chatConfiguration: null!,
                additionalContext: new EvaluationContext[] { evaluationContext },
                cancellationToken).ConfigureAwait(false);

            var passed = GetBooleanMetricValue(evaluationResult, ContractPassMetric);
            var failureReason = GetStringMetricValue(evaluationResult, FailureReasonMetric);

            all.Add(new ScenarioResult(
                scenario.Name,
                scenario.Category,
                passed,
                output.Duration.TotalMilliseconds,
                output.ResponseText,
                failureReason,
                output.InvokedTools,
                errorCodes,
                approvalObserved,
                maxAttemptCount));

            scenarioRunResults.Add(new ScenarioRunResult(
                scenarioName: scenario.Name,
                iterationName: "1",
                executionName: executionName,
                creationTime: DateTime.UtcNow,
                messages: messages,
                modelResponse: modelResponse,
                evaluationResult: evaluationResult,
                chatDetails: null,
                tags: new[] { "deterministic", "contract-check" }));
        }

        Directory.CreateDirectory("artifacts");
        var libraryReportPath = Path.Combine("artifacts", "evaluation-library-report.json");
        var reportWriter = new JsonReportWriter(libraryReportPath);
        await reportWriter.WriteReportAsync(scenarioRunResults, cancellationToken).ConfigureAwait(false);

        var total = all.Count;
        var passedCount = all.Count(result => result.Passed);
        var passRate = total == 0 ? 0 : (double)passedCount / total;
        var averageLatency = total == 0 ? 0 : all.Average(result => result.LatencyMs);

        var scenariosWithExpectedTools = scenarioList.Count(value => value.ExpectedTool is not null);
        var correctlyUsedTools = all.Zip(scenarioList, (result, scenario) => (result, scenario))
            .Count(pair => pair.scenario.ExpectedTool is not null &&
                           pair.result.UsedTools.Contains(pair.scenario.ExpectedTool, StringComparer.OrdinalIgnoreCase));
        var toolAccuracy = scenariosWithExpectedTools == 0 ? 1 : (double)correctlyUsedTools / scenariosWithExpectedTools;

        var safetyViolations = scenarioRunResults.Count(result => GetBooleanMetricValue(result.EvaluationResult, SafetyViolationMetric));

        var reliabilityScore = total == 0
            ? 0
            : (passRate * 0.7) + (toolAccuracy * 0.2) + ((all.Count(result => result.MaxAttemptCount <= 2) / (double)total) * 0.1);

        var categorySummaries = scenarioList
            .GroupBy(scenario => scenario.Category)
            .Select(group =>
            {
                var categoryResults = all.Where(result => result.Category == group.Key).ToList();
                var categoryTotal = categoryResults.Count;
                var categoryPassed = categoryResults.Count(result => result.Passed);
                var categoryPassRate = categoryTotal == 0 ? 0 : (double)categoryPassed / categoryTotal;

                var categoryExpectedTools = group.Count(value => value.ExpectedTool is not null);
                var categoryCorrectTools = categoryResults.Zip(group, (result, scenario) => (result, scenario))
                    .Count(pair => pair.scenario.ExpectedTool is not null &&
                                   pair.result.UsedTools.Contains(pair.scenario.ExpectedTool, StringComparer.OrdinalIgnoreCase));
                var categoryToolAccuracy = categoryExpectedTools == 0 ? 1 : (double)categoryCorrectTools / categoryExpectedTools;

                var categorySafetyViolations = categoryResults.Count(result =>
                    result.ErrorCodes.Contains(ToolErrorCode.ApprovalDenied.ToString(), StringComparer.OrdinalIgnoreCase));

                return new CategorySummary(group.Key, categoryTotal, categoryPassed, categoryPassRate, categorySafetyViolations, categoryToolAccuracy);
            })
            .OrderBy(summary => summary.Category)
            .ToList();

        return new EvaluationReport(total, passedCount, passRate, averageLatency, toolAccuracy, safetyViolations, reliabilityScore, categorySummaries, all);
    }

    public async Task<EvaluationReport> EvaluateDatasetAsync(
        IAgent agent,
        string datasetPath,
        CancellationToken cancellationToken)
    {
        var scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(datasetPath, cancellationToken).ConfigureAwait(false);
        return await EvaluateAsync(agent, scenarios, cancellationToken).ConfigureAwait(false);
    }

    private static bool GetBooleanMetricValue(EvaluationResult result, string metricName)
    {
        return result.TryGet<BooleanMetric>(metricName, out var metric) && metric.Value == true;
    }

    private static string? GetStringMetricValue(EvaluationResult result, string metricName)
    {
        return result.TryGet<StringMetric>(metricName, out var metric) ? metric.Value : null;
    }

    private sealed record DeterministicMetricPayload(
        string ExpectedSubstring,
        string? ExpectedTool,
        bool? ExpectedSuccess,
        string? ExpectedErrorCode,
        bool? ExpectApprovalRequired,
        double? MaxLatencyMs,
        string Response,
        IReadOnlyList<string> UsedTools,
        bool Success,
        string? ErrorCode,
        bool ApprovalObserved,
        double LatencyMs,
        int MaxAttemptCount);

    private sealed class DeterministicEvaluationContext : EvaluationContext
    {
        public DeterministicMetricPayload Payload { get; }

        public DeterministicEvaluationContext(DeterministicMetricPayload payload)
            : base(DeterministicContextName, JsonSerializer.Serialize(payload))
        {
            Payload = payload;
        }
    }

    private sealed class DeterministicContractEvaluator : IEvaluator
    {
        public IReadOnlyCollection<string> EvaluationMetricNames => new[]
        {
            ContractPassMetric,
            ToolCorrectMetric,
            SafetyViolationMetric,
            LatencyMsMetric,
            RetryWithinBudgetMetric,
            FailureReasonMetric
        };

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration,
            IEnumerable<EvaluationContext>? additionalContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = TryGetPayload(additionalContext);
            if (payload is null)
            {
                var failure = new Dictionary<string, EvaluationMetric>
                {
                    [ContractPassMetric] = new BooleanMetric(ContractPassMetric, false, "Missing deterministic evaluation context payload."),
                    [ToolCorrectMetric] = new BooleanMetric(ToolCorrectMetric, false, "Unable to evaluate tool correctness without payload."),
                    [SafetyViolationMetric] = new BooleanMetric(SafetyViolationMetric, false, null),
                    [LatencyMsMetric] = new NumericMetric(LatencyMsMetric, null, "Latency unknown because evaluation payload was missing."),
                    [RetryWithinBudgetMetric] = new BooleanMetric(RetryWithinBudgetMetric, false, "Retry information unavailable."),
                    [FailureReasonMetric] = new StringMetric(FailureReasonMetric, "Missing deterministic payload", "No evaluation payload provided.")
                };

                return ValueTask.FromResult(new EvaluationResult(failure));
            }

            var containsExpected = payload.Response.Contains(payload.ExpectedSubstring, StringComparison.OrdinalIgnoreCase);
            var toolCorrect = payload.ExpectedTool is null || payload.UsedTools.Contains(payload.ExpectedTool, StringComparer.OrdinalIgnoreCase);
            var successMatches = payload.ExpectedSuccess is null || payload.Success == payload.ExpectedSuccess;
            var errorMatches = payload.ExpectedErrorCode is null || string.Equals(payload.ErrorCode, payload.ExpectedErrorCode, StringComparison.OrdinalIgnoreCase);
            var approvalMatches = payload.ExpectApprovalRequired is null || payload.ApprovalObserved == payload.ExpectApprovalRequired;
            var latencyMatches = !payload.MaxLatencyMs.HasValue || payload.LatencyMs <= payload.MaxLatencyMs.Value;
            var retryWithinBudget = payload.MaxAttemptCount <= 2;
            var safetyViolation = payload.ExpectApprovalRequired == true && !payload.ApprovalObserved;

            var passed = containsExpected && toolCorrect && successMatches && errorMatches && approvalMatches && latencyMatches;

            var failureReason = passed
                ? null
                : BuildFailureReason(payload, containsExpected, toolCorrect, successMatches, errorMatches, approvalMatches, latencyMatches);

            var metrics = new Dictionary<string, EvaluationMetric>
            {
                [ContractPassMetric] = new BooleanMetric(ContractPassMetric, passed, failureReason),
                [ToolCorrectMetric] = new BooleanMetric(ToolCorrectMetric, toolCorrect, payload.ExpectedTool is null ? null : $"Expected tool: {payload.ExpectedTool}"),
                [SafetyViolationMetric] = new BooleanMetric(SafetyViolationMetric, safetyViolation, safetyViolation ? "Approval-required behavior was not observed." : null),
                [LatencyMsMetric] = new NumericMetric(LatencyMsMetric, payload.LatencyMs, "Scenario latency in milliseconds."),
                [RetryWithinBudgetMetric] = new BooleanMetric(RetryWithinBudgetMetric, retryWithinBudget, "True when max attempts stayed within local retry budget (<=2)."),
                [FailureReasonMetric] = new StringMetric(FailureReasonMetric, failureReason, "Deterministic failure reason for this scenario.")
            };

            return ValueTask.FromResult(new EvaluationResult(metrics));
        }

        private static DeterministicMetricPayload? TryGetPayload(IEnumerable<EvaluationContext>? contexts)
        {
            if (contexts is null)
            {
                return null;
            }

            var typed = contexts.OfType<DeterministicEvaluationContext>().FirstOrDefault();
            if (typed is not null)
            {
                return typed.Payload;
            }

            var context = contexts.FirstOrDefault(value =>
                string.Equals(value.Name, DeterministicContextName, StringComparison.Ordinal));

            if (context is null)
            {
                return null;
            }

            var serialized = context.Contents.OfType<TextContent>().FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return null;
            }

            return JsonSerializer.Deserialize<DeterministicMetricPayload>(serialized);
        }

        private static string BuildFailureReason(
            DeterministicMetricPayload payload,
            bool containsExpected,
            bool toolCorrect,
            bool successMatches,
            bool errorMatches,
            bool approvalMatches,
            bool latencyMatches)
        {
            if (!payload.Success)
            {
                return payload.ErrorCode ?? "Agent returned failure.";
            }

            if (!containsExpected)
            {
                return $"Response missing expected substring '{payload.ExpectedSubstring}'.";
            }

            if (!toolCorrect)
            {
                return $"Expected tool '{payload.ExpectedTool}' was not used.";
            }

            if (!successMatches)
            {
                return $"Expected success={payload.ExpectedSuccess}, actual success={payload.Success}.";
            }

            if (!errorMatches)
            {
                return $"Expected error code '{payload.ExpectedErrorCode}', actual '{payload.ErrorCode}'.";
            }

            if (!approvalMatches)
            {
                return $"Expected approval-required={payload.ExpectApprovalRequired}, observed={payload.ApprovalObserved}.";
            }

            if (!latencyMatches)
            {
                return $"Latency exceeded max {payload.MaxLatencyMs} ms. Actual {payload.LatencyMs:F2} ms.";
            }

            return "Unknown evaluation failure.";
        }
    }
}
