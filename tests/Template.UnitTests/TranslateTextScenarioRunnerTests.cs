using Template.Agents;
using Template.Scenarios.TranslateText;

namespace Template.UnitTests;

public sealed class TranslateTextScenarioRunnerTests
{
    [Fact]
    public async Task RunAsync_LoadsDatasetAndEvaluatesScenarios()
    {
        var datasetPath = Path.GetTempFileName();

        try
        {
            var lines = new[]
            {
                "{\"name\":\"translate-normal\",\"prompt\":\"Hello\",\"expectedSubstring\":\"Bonjour\",\"category\":\"Normal\"}",
                "{\"name\":\"translate-edge\",\"prompt\":\"Hi\",\"expectedSubstring\":\"Bonjour\",\"category\":\"Edge\"}"
            };

            await File.WriteAllLinesAsync(datasetPath, lines);

            var runner = new TranslateTextScenarioRunner();
            var result = await runner.RunAsync(new StaticSuccessAgent("Bonjour"), datasetPath, CancellationToken.None);

            Assert.Equal("TranslateText", result.Scenario);
            Assert.True(result.Dataset.Loaded);
            Assert.Equal(2, result.Dataset.Total);
            Assert.Equal(1, result.Dataset.Normal);
            Assert.Equal(1, result.Dataset.Edge);
            Assert.Equal(0, result.Dataset.Adversarial);
            Assert.Equal(2, result.Report.Total);
            Assert.Equal(2, result.Report.Passed);
        }
        finally
        {
            if (File.Exists(datasetPath))
            {
                File.Delete(datasetPath);
            }
        }
    }

    [Fact]
    public async Task RunAsync_UsesDefaultScenarioWhenDatasetMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.jsonl");

        var runner = new TranslateTextScenarioRunner();
        var result = await runner.RunAsync(new StaticSuccessAgent("Bonjour"), missingPath, CancellationToken.None);

        Assert.False(result.Dataset.Loaded);
        Assert.Equal(1, result.Dataset.Total);
        Assert.Contains("not found", result.Dataset.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Report.Total);
        Assert.Equal(1, result.Report.Passed);
    }

    private sealed class StaticSuccessAgent : IAgent
    {
        private readonly string _response;

        public StaticSuccessAgent(string response)
        {
            _response = response;
        }

        public string Name => "static-success";

        public AgentContract Contract { get; } = new(
            Purpose: "Static test agent.",
            Capabilities: ["Return static response"],
            OutOfScope: [],
            RequiredTools: []);

        public Task<AgentTurnOutput> RunAsync(AgentTurnInput input, CancellationToken cancellationToken)
        {
            var output = new AgentTurnOutput(
                Success: true,
                ResponseText: _response,
                InvokedTools: [],
                ToolResults: [],
                Duration: TimeSpan.FromMilliseconds(5));

            return Task.FromResult(output);
        }
    }
}
