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

    [Fact]
    public async Task LoadJsonlAsync_DeserializesOriginAndLinkedContractRule()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"name\":\"origin-test\",\"prompt\":\"test prompt\",\"expectedSubstring\":\"test\",\"origin\":\"DerivedFromContractCondition\",\"linkedContractRule\":\"DoNotUseWhen: A safer tool can fulfill the request.\"}");

            var scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(path, CancellationToken.None);

            Assert.Single(scenarios);
            Assert.Equal(ScenarioOrigin.DerivedFromContractCondition, scenarios[0].Origin);
            Assert.Equal("DoNotUseWhen: A safer tool can fulfill the request.", scenarios[0].LinkedContractRule);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadJsonlAsync_DefaultsOriginToManual_WhenFieldAbsent()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"name\":\"minimal\",\"prompt\":\"hello\",\"expectedSubstring\":\"hi\"}");

            var scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(path, CancellationToken.None);

            Assert.Single(scenarios);
            Assert.Equal(ScenarioOrigin.Manual, scenarios[0].Origin);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadJsonlAsync_Throws_WhenRequiredFieldMissing()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"name\":\"missing-expected\",\"prompt\":\"hello\"}");

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                EvaluationDatasetLoader.LoadJsonlAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadJsonlAsync_SkipsBlankLines_WithoutThrowing()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path,
                "{\"name\":\"first\",\"prompt\":\"p1\",\"expectedSubstring\":\"s1\"}\n\n{\"name\":\"second\",\"prompt\":\"p2\",\"expectedSubstring\":\"s2\"}");

            var scenarios = await EvaluationDatasetLoader.LoadJsonlAsync(path, CancellationToken.None);

            Assert.Equal(2, scenarios.Count);
            Assert.Equal("first", scenarios[0].Name);
            Assert.Equal("second", scenarios[1].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
