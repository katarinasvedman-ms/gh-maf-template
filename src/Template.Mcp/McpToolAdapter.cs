using Template.Tools;

namespace Template.Mcp;

public sealed class McpToolAdapter : ITool
{
    private readonly IMcpServerClient _client;
    private readonly string _serverId;
    private readonly string _remoteToolName;

    public McpToolAdapter(IMcpServerClient client, McpToolDescriptor descriptor)
    {
        _client = client;
        _serverId = descriptor.ServerId;
        _remoteToolName = descriptor.Name;
        Definition = new ToolDefinition(
            Name: $"mcp.{descriptor.Name}",
            Description: descriptor.Description,
            RequiredArguments: descriptor.RequiredArguments,
            Sensitivity: descriptor.Sensitivity,
            Timeout: descriptor.Timeout,
            Idempotent: descriptor.Idempotent,
            Contract: new ToolContract(
                WhatItDoes: descriptor.Description,
                UseWhen: ["An MCP-backed capability is required for this task."],
                DoNotUseWhen: ["A local tool can satisfy the request with lower risk."],
                InputDescriptions: descriptor.RequiredArguments.ToDictionary(
                    keySelector: value => value,
                    elementSelector: _ => "Required MCP argument.",
                    comparer: StringComparer.OrdinalIgnoreCase),
                OutputDescription: "MCP tool response payload.",
                Constraints: ["MCP server availability is required."],
                SideEffects: ["Depends on remote MCP tool behavior."],
                AllowedModes: [ToolExecutionMode.Translation, ToolExecutionMode.Evaluation, ToolExecutionMode.Unspecified],
                MaxRiskLevel: ToolRiskLevel.High));
    }

    public ToolDefinition Definition { get; }

    public async Task<ToolCallResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken)
    {
        var response = await _client
            .InvokeToolAsync(_serverId, _remoteToolName, request.Arguments, cancellationToken)
            .ConfigureAwait(false);

        if (response.Success)
        {
            return ToolCallResult.Ok(request.ToolName, response.Output, 1, TimeSpan.Zero);
        }

        return ToolCallResult.Failed(
            request.ToolName,
            response.Transient ? ToolErrorCode.TransientFailure : ToolErrorCode.ExecutionFailed,
            response.Error ?? "Unknown MCP failure.",
            1,
            TimeSpan.Zero);
    }
}
