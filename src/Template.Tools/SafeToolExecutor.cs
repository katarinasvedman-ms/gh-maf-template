using System.Diagnostics;

namespace Template.Tools;

public sealed class SafeToolExecutor : IToolExecutor
{
    private static readonly IToolExecutionObserver NoOpObserver = new NoOpToolExecutionObserver();

    private readonly IToolRegistry _registry;
    private readonly IToolPolicy _policy;
    private readonly IToolApprovalGate _approvalGate;
    private readonly IToolExecutionObserver _observer;
    private readonly RetryPolicy _retryPolicy;

    public SafeToolExecutor(
        IToolRegistry registry,
        IToolPolicy policy,
        IToolApprovalGate approvalGate,
        IToolExecutionObserver? observer = null,
        RetryPolicy? retryPolicy = null)
    {
        _registry = registry;
        _policy = policy;
        _approvalGate = approvalGate;
        _observer = observer ?? NoOpObserver;
        _retryPolicy = retryPolicy ?? RetryPolicy.Conservative;
    }

    public async Task<ToolCallResult> ExecuteAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();

        if (!_registry.TryGet(request.ToolName, out var tool) || tool is null)
        {
            return ToolCallResult.Failed(request.ToolName, ToolErrorCode.UnknownTool, "Tool not registered.", 0, started.Elapsed);
        }

        if (!_policy.TryAuthorize(tool.Definition, request, out var denialReason))
        {
            return ToolCallResult.Failed(request.ToolName, ToolErrorCode.DeniedByPolicy, denialReason, 0, started.Elapsed);
        }

        var validationFailure = ValidateRequiredArguments(tool.Definition, request);
        if (validationFailure is not null)
        {
            return ToolCallResult.Failed(request.ToolName, ToolErrorCode.InvalidArguments, validationFailure, 0, started.Elapsed);
        }

        if (tool.Definition.Sensitivity == ToolSensitivity.High)
        {
            var approved = await _approvalGate.ApproveAsync(tool.Definition, request, cancellationToken).ConfigureAwait(false);
            if (!approved)
            {
                return ToolCallResult.Failed(
                    request.ToolName,
                    ToolErrorCode.ApprovalDenied,
                    "High-sensitivity tool invocation was not approved.",
                    0,
                    started.Elapsed,
                    approvalRequired: true);
            }
        }

        var attempt = 0;
        while (attempt < _retryPolicy.MaxAttempts)
        {
            attempt++;
            _observer.OnAttemptStarted(request, attempt);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(tool.Definition.Timeout);

            ToolCallResult result;
            try
            {
                result = await tool.InvokeAsync(request, timeoutCts.Token).ConfigureAwait(false);

                if (result.Success)
                {
                    var successful = result with
                    {
                        AttemptCount = attempt,
                        Duration = started.Elapsed,
                        Provider = ResolveProvider(request.ToolName),
                        CorrelationId = request.CorrelationId
                    };
                    _observer.OnAttemptCompleted(request, successful);
                    return successful;
                }

                var normalized = result with
                {
                    AttemptCount = attempt,
                    Duration = started.Elapsed,
                    Provider = ResolveProvider(request.ToolName),
                    CorrelationId = request.CorrelationId
                };
                _observer.OnAttemptCompleted(request, normalized);
                if (normalized.Failure?.Code != ToolErrorCode.TransientFailure || attempt >= _retryPolicy.MaxAttempts)
                {
                    return normalized;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = ToolCallResult.Failed(
                    request.ToolName,
                    ToolErrorCode.Timeout,
                    "Tool execution timed out.",
                    attempt,
                    started.Elapsed,
                    exceptionType: nameof(OperationCanceledException)) with
                {
                    Provider = ResolveProvider(request.ToolName),
                    CorrelationId = request.CorrelationId
                };
                _observer.OnAttemptCompleted(request, result);
                return result;
            }
            catch (Exception ex)
            {
                var isTransient = IsTransient(ex);
                var code = isTransient ? ToolErrorCode.TransientFailure : ToolErrorCode.ExecutionFailed;
                result = ToolCallResult.Failed(request.ToolName, code, ex.Message, attempt, started.Elapsed, exceptionType: ex.GetType().Name) with
                {
                    Provider = ResolveProvider(request.ToolName),
                    CorrelationId = request.CorrelationId
                };
                _observer.OnAttemptCompleted(request, result);

                if (!isTransient || attempt >= _retryPolicy.MaxAttempts)
                {
                    return result;
                }
            }

            var delay = TimeSpan.FromMilliseconds(_retryPolicy.BaseDelay.TotalMilliseconds * attempt);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return ToolCallResult.Failed(request.ToolName, ToolErrorCode.ExecutionFailed, "Execution exited retry loop unexpectedly.", attempt, started.Elapsed);
    }

    private static string? ValidateRequiredArguments(ToolDefinition definition, ToolCallRequest request)
    {
        foreach (var required in definition.RequiredArguments)
        {
            if (!request.Arguments.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return $"Missing required argument '{required}'.";
            }
        }

        return null;
    }

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException ||
        ex is HttpRequestException ||
        ex is TaskCanceledException;

    private static string ResolveProvider(string toolName) =>
        toolName.StartsWith("mcp.", StringComparison.OrdinalIgnoreCase) ? "mcp" : "local";

    private sealed class NoOpToolExecutionObserver : IToolExecutionObserver
    {
        public void OnAttemptStarted(ToolCallRequest request, int attempt)
        {
        }

        public void OnAttemptCompleted(ToolCallRequest request, ToolCallResult result)
        {
        }
    }
}
