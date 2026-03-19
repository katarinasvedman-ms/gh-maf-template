using Template.Tools;

namespace Template.Agents;

public sealed record AgentTurnInput(string CorrelationId, string UserPrompt);

public sealed record AgentTurnOutput(
    bool Success,
    string ResponseText,
    IReadOnlyList<string> InvokedTools,
    IReadOnlyList<ToolCallResult> ToolResults,
    TimeSpan Duration,
    string? ErrorCode = null);

public interface IAgent
{
    string Name { get; }

    Task<AgentTurnOutput> RunAsync(AgentTurnInput input, CancellationToken cancellationToken);
}

public interface IAgentWorker
{
    string Name { get; }

    bool CanHandle(string prompt);

    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
