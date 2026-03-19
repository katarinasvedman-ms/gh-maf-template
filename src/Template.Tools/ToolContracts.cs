namespace Template.Tools;

public enum ToolSensitivity
{
    Low,
    High
}

public enum ToolErrorCode
{
    UnknownTool,
    DeniedByPolicy,
    InvalidArguments,
    ApprovalDenied,
    Timeout,
    ExecutionFailed,
    TransientFailure
}

public enum ToolExecutionMode
{
    Unspecified,
    Translation,
    Evaluation,
    Operations,
    Admin
}

public enum ToolRiskLevel
{
    Low = 0,
    Elevated = 1,
    High = 2,
    Critical = 3
}

public sealed record ToolExecutionContext(
    ToolExecutionMode Mode,
    string Intent,
    ToolRiskLevel RiskLevel,
    IReadOnlyDictionary<string, string>? Tags = null)
{
    public static ToolExecutionContext Default { get; } = new(ToolExecutionMode.Unspecified, "unspecified", ToolRiskLevel.Low);
}

public sealed record ToolContract(
    string WhatItDoes,
    IReadOnlyList<string> UseWhen,
    IReadOnlyList<string> DoNotUseWhen,
    IReadOnlyDictionary<string, string> InputDescriptions,
    string OutputDescription,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> SideEffects,
    IReadOnlyCollection<ToolExecutionMode> AllowedModes,
    ToolRiskLevel MaxRiskLevel)
{
    public static ToolContract CreateDefault(string description, IReadOnlyCollection<string> requiredArguments)
    {
        var inputDescriptions = requiredArguments.ToDictionary(
            keySelector: value => value,
            elementSelector: _ => "Required input.",
            comparer: StringComparer.OrdinalIgnoreCase);

        return new ToolContract(
            WhatItDoes: description,
            UseWhen: ["The task explicitly requires this tool capability."],
            DoNotUseWhen: ["A safer or narrower-scope tool can fulfill the same request."],
            InputDescriptions: inputDescriptions,
            OutputDescription: "Tool-specific response payload.",
            Constraints: ["Input validation and policy checks are required before execution."],
            SideEffects: ["No declared external side effects."],
            AllowedModes: [ToolExecutionMode.Unspecified],
            MaxRiskLevel: ToolRiskLevel.High);
    }
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyCollection<string> RequiredArguments,
    ToolSensitivity Sensitivity,
    TimeSpan Timeout,
    bool Idempotent = false,
    ToolContract? Contract = null);

public sealed record ToolCallRequest(
    string CorrelationId,
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    DateTimeOffset RequestedAtUtc,
    ToolExecutionContext? Context = null)
{
    public ToolExecutionContext EffectiveContext => Context ?? ToolExecutionContext.Default;
}

public sealed record ToolExecutionFailure(ToolErrorCode Code, string Message, string? ExceptionType = null);

public sealed record ToolCallResult(
    string ToolName,
    bool Success,
    string Output,
    ToolExecutionFailure? Failure,
    int AttemptCount,
    TimeSpan Duration,
    bool ApprovalRequired,
    string? Provider = null,
    string? CorrelationId = null)
{
    public static ToolCallResult Ok(string toolName, string output, int attemptCount, TimeSpan duration) =>
        new(toolName, true, output, null, attemptCount, duration, false);

    public static ToolCallResult Failed(
        string toolName,
        ToolErrorCode code,
        string message,
        int attemptCount,
        TimeSpan duration,
        bool approvalRequired = false,
        string? exceptionType = null) =>
        new(toolName, false, string.Empty, new ToolExecutionFailure(code, message, exceptionType), attemptCount, duration, approvalRequired);
}

public sealed record ToolCommandParseResult(
    bool IsToolCommand,
    bool Success,
    string? ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    string? Error);

public interface IToolCommandParser
{
    ToolCommandParseResult Parse(string input);
}

public sealed record RetryPolicy(int MaxAttempts, TimeSpan BaseDelay)
{
    public static RetryPolicy Conservative => new(2, TimeSpan.FromMilliseconds(75));
}

public interface ITool
{
    ToolDefinition Definition { get; }

    Task<ToolCallResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken);
}

public interface IToolRegistry
{
    void Register(ITool tool);

    bool TryGet(string toolName, out ITool? tool);

    IReadOnlyCollection<ToolDefinition> List();
}

public interface IToolPolicy
{
    bool TryAuthorize(ToolDefinition definition, ToolCallRequest request, out string reason);
}

public interface IToolApprovalGate
{
    Task<bool> ApproveAsync(ToolDefinition definition, ToolCallRequest request, CancellationToken cancellationToken);
}

public interface IToolExecutionObserver
{
    void OnAttemptStarted(ToolCallRequest request, int attempt);

    void OnAttemptCompleted(ToolCallRequest request, ToolCallResult result);
}

public interface IToolExecutor
{
    Task<ToolCallResult> ExecuteAsync(ToolCallRequest request, CancellationToken cancellationToken = default);
}

public static class ToolContractValidator
{
    public static void ValidateOrThrow(ToolDefinition definition)
    {
        var contract = definition.Contract ?? ToolContract.CreateDefault(definition.Description, definition.RequiredArguments);

        if (string.IsNullOrWhiteSpace(contract.WhatItDoes))
        {
            throw new InvalidOperationException($"Tool '{definition.Name}' must declare what it does.");
        }

        if (contract.UseWhen.Count == 0)
        {
            throw new InvalidOperationException($"Tool '{definition.Name}' must define at least one use case.");
        }

        if (contract.DoNotUseWhen.Count == 0)
        {
            throw new InvalidOperationException($"Tool '{definition.Name}' must define at least one do-not-use condition.");
        }

        if (string.IsNullOrWhiteSpace(contract.OutputDescription))
        {
            throw new InvalidOperationException($"Tool '{definition.Name}' must declare output description.");
        }

        if (contract.AllowedModes.Count == 0)
        {
            throw new InvalidOperationException($"Tool '{definition.Name}' must declare at least one allowed execution mode.");
        }

        foreach (var requiredArgument in definition.RequiredArguments)
        {
            if (!contract.InputDescriptions.TryGetValue(requiredArgument, out var description) || string.IsNullOrWhiteSpace(description))
            {
                throw new InvalidOperationException($"Tool '{definition.Name}' is missing input contract for required argument '{requiredArgument}'.");
            }
        }
    }
}
