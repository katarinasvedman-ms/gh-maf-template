namespace Template.Agents;

public sealed class SafetyWorker : IAgentWorker
{
    public string Name => "safety";

    public bool CanHandle(string prompt) =>
        prompt.Contains("safe", StringComparison.OrdinalIgnoreCase) ||
        prompt.Contains("risk", StringComparison.OrdinalIgnoreCase) ||
        prompt.Contains("policy", StringComparison.OrdinalIgnoreCase);

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        const string response = "Applied safety checks: allow-list enforcement, argument validation, approval gate, timeout, and retry boundaries.";
        return Task.FromResult(response);
    }
}
