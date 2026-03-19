namespace Template.Agents;

public sealed class PlanningWorker : IAgentWorker
{
    public string Name => "planning";

    public bool CanHandle(string prompt) =>
        prompt.Contains("plan", StringComparison.OrdinalIgnoreCase) ||
        prompt.Contains("design", StringComparison.OrdinalIgnoreCase) ||
        prompt.Contains("architecture", StringComparison.OrdinalIgnoreCase);

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        const string response = "Generated a modular plan with orchestrator, worker agents, tool policy gates, and evaluation checks.";
        return Task.FromResult(response);
    }
}
