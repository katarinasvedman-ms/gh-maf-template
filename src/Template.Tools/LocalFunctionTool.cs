namespace Template.Tools;

public sealed class LocalFunctionTool : ITool
{
    private readonly Func<ToolCallRequest, CancellationToken, Task<ToolCallResult>> _handler;

    public LocalFunctionTool(ToolDefinition definition, Func<ToolCallRequest, CancellationToken, Task<ToolCallResult>> handler)
    {
        Definition = definition;
        _handler = handler;
    }

    public LocalFunctionTool(ToolDefinition definition, Func<ToolCallRequest, Task<ToolCallResult>> handler)
        : this(definition, (request, _) => handler(request))
    {
    }

    public ToolDefinition Definition { get; }

    public Task<ToolCallResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken) => _handler(request, cancellationToken);
}
