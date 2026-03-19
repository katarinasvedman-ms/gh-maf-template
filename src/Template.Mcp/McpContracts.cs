using Template.Tools;

namespace Template.Mcp;

public sealed record McpToolDescriptor(
    string ServerId,
    string Name,
    string Description,
    IReadOnlyCollection<string> RequiredArguments,
    ToolSensitivity Sensitivity,
    TimeSpan Timeout,
    bool Idempotent = false);

public sealed record McpInvocationResult(bool Success, string Output, string? Error, bool Transient);

public sealed record McpServerDefinition(string ServerId, bool Required = false);

public sealed record McpRegistrationFailure(string ServerId, string Error, bool Required, bool Transient);

public sealed record McpRegistrationResult(
    IReadOnlyCollection<ITool> Tools,
    IReadOnlyCollection<McpRegistrationFailure> Failures);

public interface IMcpServerClient
{
    Task<IReadOnlyCollection<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken);

    Task<McpInvocationResult> InvokeToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken);
}
