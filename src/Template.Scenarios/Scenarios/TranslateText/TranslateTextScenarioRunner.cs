using Template.Agents;
using Template.Evaluation;

namespace Template.Scenarios.TranslateText;

public sealed record TranslateTextDatasetSummary(
    string Path,
    bool Loaded,
    int Total,
    int Normal,
    int Edge,
    int Adversarial,
    string? Reason = null);

public sealed record TranslateTextScenarioRunResult(
    string Scenario,
    EvaluationReport Report,
    TranslateTextDatasetSummary Dataset);

public sealed class TranslateTextScenarioRunner
{
    private readonly AgentScenarioEvaluator _evaluator;

    public TranslateTextScenarioRunner(AgentScenarioEvaluator? evaluator = null)
    {
        _evaluator = evaluator ?? new AgentScenarioEvaluator();
    }

    public static string GetDefaultDatasetPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "evaluation-datasets",
            "translator-scenarios.jsonl"));
    }

    public async Task<TranslateTextScenarioRunResult> RunAsync(
        IAgent agent,
        string? datasetPath,
        CancellationToken cancellationToken)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(datasetPath)
            ? GetDefaultDatasetPath()
            : datasetPath;

        IReadOnlyList<EvaluationScenario> scenarios;
        TranslateTextDatasetSummary dataset;

        if (File.Exists(resolvedPath))
        {
            scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            dataset = new TranslateTextDatasetSummary(
                Path: resolvedPath,
                Loaded: true,
                Total: scenarios.Count,
                Normal: scenarios.Count(value => value.Category == EvaluationScenarioCategory.Normal),
                Edge: scenarios.Count(value => value.Category == EvaluationScenarioCategory.Edge),
                Adversarial: scenarios.Count(value => value.Category == EvaluationScenarioCategory.Adversarial));
        }
        else
        {
            scenarios =
            [
                new EvaluationScenario(
                    Name: "TranslateText-default",
                    Prompt: "Hello, world!",
                    ExpectedSubstring: "Bonjour")
            ];

            dataset = new TranslateTextDatasetSummary(
                Path: resolvedPath,
                Loaded: false,
                Total: scenarios.Count,
                Normal: 1,
                Edge: 0,
                Adversarial: 0,
                Reason: "Dataset file not found. Ran default in-memory scenario.");
        }

        var report = await _evaluator.EvaluateAsync(agent, scenarios, cancellationToken).ConfigureAwait(false);
        return new TranslateTextScenarioRunResult("TranslateText", report, dataset);
    }
}
