using System.Text.Json;

namespace Template.Evaluation;

public static class EvaluationDatasetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<EvaluationScenario>> LoadJsonlAsync(string datasetPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            throw new ArgumentException("Dataset path is required.", nameof(datasetPath));
        }

        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"Dataset file not found: {datasetPath}", datasetPath);
        }

        var scenarios = new List<EvaluationScenario>();
        var lineNumber = 0;

        await foreach (var line in File.ReadLinesAsync(datasetPath, cancellationToken).ConfigureAwait(false))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var dto = JsonSerializer.Deserialize<EvaluationScenarioJsonl>(line, JsonOptions);
            if (dto is null)
            {
                throw new InvalidDataException($"Invalid JSONL record at line {lineNumber}.");
            }

            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Prompt) || string.IsNullOrWhiteSpace(dto.ExpectedSubstring))
            {
                throw new InvalidDataException($"Dataset line {lineNumber} is missing required fields 'name', 'prompt', or 'expectedSubstring'.");
            }

            var category = ParseCategory(dto.Category);

            var origin = ParseOrigin(dto.Origin);

            scenarios.Add(new EvaluationScenario(
                Name: dto.Name,
                Prompt: dto.Prompt,
                ExpectedSubstring: dto.ExpectedSubstring,
                Category: category,
                ExpectedTool: dto.ExpectedTool,
                ExpectedSuccess: dto.ExpectedSuccess,
                ExpectedErrorCode: dto.ExpectedErrorCode,
                ExpectApprovalRequired: dto.ExpectApprovalRequired,
                MaxLatencyMs: dto.MaxLatencyMs,
                Origin: origin,
                LinkedContractRule: dto.LinkedContractRule));
        }

        return scenarios;
    }

    private static EvaluationScenarioCategory ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EvaluationScenarioCategory.Normal;
        }

        return Enum.TryParse<EvaluationScenarioCategory>(value, ignoreCase: true, out var category)
            ? category
            : EvaluationScenarioCategory.Normal;
    }

    private static ScenarioOrigin ParseOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ScenarioOrigin.Manual;
        }

        return Enum.TryParse<ScenarioOrigin>(value, ignoreCase: true, out var origin)
            ? origin
            : ScenarioOrigin.Manual;
    }

    private sealed record EvaluationScenarioJsonl(
        string Name,
        string Prompt,
        string ExpectedSubstring,
        string? Category,
        string? ExpectedTool,
        bool? ExpectedSuccess,
        string? ExpectedErrorCode,
        bool? ExpectApprovalRequired,
        double? MaxLatencyMs,
        string? Origin = null,
        string? LinkedContractRule = null);
}
