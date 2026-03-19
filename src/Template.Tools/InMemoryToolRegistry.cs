namespace Template.Tools;

public sealed class InMemoryToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ITool tool)
    {
        var normalizedDefinition = EnsureContract(tool.Definition);
        ToolContractValidator.ValidateOrThrow(normalizedDefinition);
        _tools[normalizedDefinition.Name] = tool is LocalFunctionTool
            ? new LocalFunctionTool(normalizedDefinition, tool.InvokeAsync)
            : new ContractNormalizedTool(tool, normalizedDefinition);
    }

    public bool TryGet(string toolName, out ITool? tool)
    {
        var found = _tools.TryGetValue(toolName, out var registered);
        tool = registered;
        return found;
    }

    public IReadOnlyCollection<ToolDefinition> List() => _tools.Values.Select(value => value.Definition).ToArray();

    private static ToolDefinition EnsureContract(ToolDefinition definition) =>
        definition.Contract is null
            ? definition with { Contract = ToolContract.CreateDefault(definition.Description, definition.RequiredArguments) }
            : definition;

    private sealed class ContractNormalizedTool : ITool
    {
        private readonly ITool _inner;

        public ContractNormalizedTool(ITool inner, ToolDefinition definition)
        {
            _inner = inner;
            Definition = definition;
        }

        public ToolDefinition Definition { get; }

        public Task<ToolCallResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken) =>
            _inner.InvokeAsync(request, cancellationToken);
    }
}
