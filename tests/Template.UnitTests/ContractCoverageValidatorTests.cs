using Template.Evaluation;
using Template.Tools;

namespace Template.UnitTests;

public sealed class ContractCoverageValidatorTests
{
    [Fact]
    public void Validate_ReturnsNoGaps_WhenAllConditionsHaveMatchingScenarios()
    {
        var tools = new[]
        {
            new ToolDefinition(
                "test-tool", "Test tool", new[] { "input" }, ToolSensitivity.Low, TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Does a thing.",
                    UseWhen: ["The task explicitly requires this tool capability."],
                    DoNotUseWhen: ["A safer or narrower-scope tool can fulfill the same request."],
                    InputDescriptions: new Dictionary<string, string> { ["input"] = "Input value." },
                    OutputDescription: "Result.",
                    Constraints: ["None."],
                    SideEffects: ["None."],
                    AllowedModes: [ToolExecutionMode.Unspecified],
                    MaxRiskLevel: ToolRiskLevel.High))
        };

        var scenarios = new[]
        {
            new EvaluationScenario(
                "normal-test", "Do the thing", "result",
                Category: EvaluationScenarioCategory.Normal,
                ExpectedTool: "test-tool",
                LinkedContractRule: "UseWhen: The task explicitly requires this tool capability."),
            new EvaluationScenario(
                "adversarial-test", "Bypass safety", "denied",
                Category: EvaluationScenarioCategory.Adversarial,
                ExpectedTool: "test-tool",
                LinkedContractRule: "DoNotUseWhen: A safer or narrower-scope tool can fulfill the same request.")
        };

        var report = ContractCoverageValidator.Validate(tools, scenarios);

        Assert.False(report.HasGaps);
        Assert.Empty(report.UncoveredUseWhenConditions);
        Assert.Empty(report.UncoveredDoNotUseWhenConditions);
    }

    [Fact]
    public void Validate_DetectsMissingNormalScenario_ForUseWhenCondition()
    {
        var tools = new[]
        {
            new ToolDefinition(
                "test-tool", "Test tool", new[] { "input" }, ToolSensitivity.Low, TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Does a thing.",
                    UseWhen: ["The task explicitly requires this tool capability."],
                    DoNotUseWhen: ["A safer or narrower-scope tool can fulfill the same request."],
                    InputDescriptions: new Dictionary<string, string> { ["input"] = "Input value." },
                    OutputDescription: "Result.",
                    Constraints: ["None."],
                    SideEffects: ["None."],
                    AllowedModes: [ToolExecutionMode.Unspecified],
                    MaxRiskLevel: ToolRiskLevel.High))
        };

        var scenarios = new[]
        {
            new EvaluationScenario(
                "adversarial-test", "Bypass safety", "denied",
                Category: EvaluationScenarioCategory.Adversarial,
                ExpectedTool: "test-tool",
                LinkedContractRule: "DoNotUseWhen: A safer or narrower-scope tool can fulfill the same request.")
        };

        var report = ContractCoverageValidator.Validate(tools, scenarios);

        Assert.True(report.HasGaps);
        Assert.Single(report.UncoveredUseWhenConditions);
        Assert.Contains("The task explicitly requires this tool capability.", report.UncoveredUseWhenConditions[0]);
    }

    [Fact]
    public void Validate_DetectsMissingAdversarialScenario_ForDoNotUseWhenCondition()
    {
        var tools = new[]
        {
            new ToolDefinition(
                "test-tool", "Test tool", new[] { "input" }, ToolSensitivity.Low, TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Does a thing.",
                    UseWhen: ["The task explicitly requires this tool capability."],
                    DoNotUseWhen: ["A safer or narrower-scope tool can fulfill the same request."],
                    InputDescriptions: new Dictionary<string, string> { ["input"] = "Input value." },
                    OutputDescription: "Result.",
                    Constraints: ["None."],
                    SideEffects: ["None."],
                    AllowedModes: [ToolExecutionMode.Unspecified],
                    MaxRiskLevel: ToolRiskLevel.High))
        };

        var scenarios = new[]
        {
            new EvaluationScenario(
                "normal-test", "Do the thing", "result",
                Category: EvaluationScenarioCategory.Normal,
                ExpectedTool: "test-tool",
                LinkedContractRule: "UseWhen: The task explicitly requires this tool capability.")
        };

        var report = ContractCoverageValidator.Validate(tools, scenarios);

        Assert.True(report.HasGaps);
        Assert.Single(report.UncoveredDoNotUseWhenConditions);
        Assert.Contains("A safer or narrower-scope tool can fulfill the same request.", report.UncoveredDoNotUseWhenConditions[0]);
    }

    [Fact]
    public void HasGaps_IsFalseWhenCoverageComplete_TrueWhenNot()
    {
        var tools = new[]
        {
            new ToolDefinition(
                "test-tool", "Test tool", new[] { "input" }, ToolSensitivity.Low, TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Does a thing.",
                    UseWhen: ["Condition A"],
                    DoNotUseWhen: ["Condition B"],
                    InputDescriptions: new Dictionary<string, string> { ["input"] = "Input value." },
                    OutputDescription: "Result.",
                    Constraints: ["None."],
                    SideEffects: ["None."],
                    AllowedModes: [ToolExecutionMode.Unspecified],
                    MaxRiskLevel: ToolRiskLevel.High))
        };

        // No scenarios at all — should have gaps
        var reportWithGaps = ContractCoverageValidator.Validate(tools, Array.Empty<EvaluationScenario>());
        Assert.True(reportWithGaps.HasGaps);

        // Full coverage
        var fullScenarios = new[]
        {
            new EvaluationScenario(
                "normal", "Do A", "result",
                Category: EvaluationScenarioCategory.Normal,
                ExpectedTool: "test-tool",
                LinkedContractRule: "UseWhen: Condition A"),
            new EvaluationScenario(
                "adversarial", "Try B", "denied",
                Category: EvaluationScenarioCategory.Adversarial,
                ExpectedTool: "test-tool",
                LinkedContractRule: "DoNotUseWhen: Condition B")
        };

        var reportComplete = ContractCoverageValidator.Validate(tools, fullScenarios);
        Assert.False(reportComplete.HasGaps);
    }
}
