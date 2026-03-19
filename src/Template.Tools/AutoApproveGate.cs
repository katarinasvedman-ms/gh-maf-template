namespace Template.Tools;

public sealed class AutoApproveGate : IToolApprovalGate
{
    private readonly bool _approveHighSensitivity;

    public AutoApproveGate(bool approveHighSensitivity)
    {
        _approveHighSensitivity = approveHighSensitivity;
    }

    public Task<bool> ApproveAsync(ToolDefinition definition, ToolCallRequest request, CancellationToken cancellationToken)
    {
        if (definition.Sensitivity == ToolSensitivity.Low)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(_approveHighSensitivity);
    }
}
