using Template.Evaluation;

namespace Template.UnitTests;

public sealed class EvaluationDatasetLoaderTests
{
    [Fact]
    public async Task LoadJsonlAsync_LoadsNormalEdgeAndAdversarialCategories()
    {
        var datasetPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "evaluation-datasets", "translator-scenarios.jsonl"));

        var scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(datasetPath, CancellationToken.None);

        Assert.NotEmpty(scenarios);
        Assert.Contains(scenarios, scenario => scenario.Category == EvaluationScenarioCategory.Normal);
        Assert.Contains(scenarios, scenario => scenario.Category == EvaluationScenarioCategory.Edge);
        Assert.Contains(scenarios, scenario => scenario.Category == EvaluationScenarioCategory.Adversarial);
    }
}
