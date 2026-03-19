namespace Template.Tools;

public sealed class AllowListToolPolicy : IToolPolicy
{
    private readonly HashSet<string> _allowedTools;

    public AllowListToolPolicy(IEnumerable<string> allowedTools)
    {
        _allowedTools = new HashSet<string>(allowedTools, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryAuthorize(ToolDefinition definition, ToolCallRequest request, out string reason)
    {
        if (!_allowedTools.Contains(definition.Name))
        {
            reason = $"Tool '{definition.Name}' is not in the allow list.";
            return false;
        }

        var contract = definition.Contract ?? ToolContract.CreateDefault(definition.Description, definition.RequiredArguments);
        var context = request.EffectiveContext;

        if (!contract.AllowedModes.Contains(context.Mode))
        {
            reason = $"Tool '{definition.Name}' is not available in execution mode '{context.Mode}'.";
            return false;
        }

        if (context.RiskLevel > contract.MaxRiskLevel)
        {
            reason = $"Tool '{definition.Name}' is blocked for risk level '{context.RiskLevel}' (max allowed '{contract.MaxRiskLevel}').";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
