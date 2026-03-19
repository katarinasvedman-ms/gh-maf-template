namespace Template.Agents;

public sealed class GeneralWorker : IAgentWorker
{
    public string Name => "general";

    public bool CanHandle(string prompt) => true;

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled prompt with general reasoning: {prompt}");
    }
}
