using Template.Tools;

namespace Template.Evaluation;

public sealed record ContractCoverageReport(
    IReadOnlyList<string> UncoveredUseWhenConditions,
    IReadOnlyList<string> UncoveredDoNotUseWhenConditions,
    IReadOnlyList<string> UncoveredModes,
    bool HasGaps);

public static class ContractCoverageValidator
{
    public static ContractCoverageReport Validate(
        IEnumerable<ToolDefinition> tools,
        IEnumerable<EvaluationScenario> scenarios)
    {
        var scenarioList = scenarios.ToList();
        var uncoveredUseWhen = new List<string>();
        var uncoveredDoNotUseWhen = new List<string>();
        var uncoveredModes = new List<string>();

        foreach (var tool in tools)
        {
            var contract = tool.Contract ?? ToolContract.CreateDefault(tool.Description, tool.RequiredArguments);

            foreach (var condition in contract.UseWhen)
            {
                var covered = scenarioList.Any(s =>
                    s.Category == EvaluationScenarioCategory.Normal &&
                    s.LinkedContractRule is not null &&
                    s.LinkedContractRule.Contains(condition, StringComparison.OrdinalIgnoreCase));

                if (!covered)
                {
                    uncoveredUseWhen.Add($"{tool.Name}: {condition}");
                }
            }

            foreach (var condition in contract.DoNotUseWhen)
            {
                var covered = scenarioList.Any(s =>
                    s.Category == EvaluationScenarioCategory.Adversarial &&
                    s.LinkedContractRule is not null &&
                    s.LinkedContractRule.Contains(condition, StringComparison.OrdinalIgnoreCase));

                if (!covered)
                {
                    uncoveredDoNotUseWhen.Add($"{tool.Name}: {condition}");
                }
            }

            foreach (var mode in contract.AllowedModes)
            {
                var covered = scenarioList.Any(s => s.ExpectedTool == tool.Name);

                if (!covered)
                {
                    uncoveredModes.Add($"{tool.Name}: {mode}");
                }
            }
        }

        var hasGaps = uncoveredUseWhen.Count > 0 || uncoveredDoNotUseWhen.Count > 0 || uncoveredModes.Count > 0;

        return new ContractCoverageReport(
            uncoveredUseWhen,
            uncoveredDoNotUseWhen,
            uncoveredModes,
            hasGaps);
    }
}
