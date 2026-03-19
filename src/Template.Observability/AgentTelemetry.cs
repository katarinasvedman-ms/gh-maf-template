using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Template.Tools;

namespace Template.Observability;

public interface IAgentExecutionObserver
{
    IDisposable BeginAgentTurn(string agentName, string correlationId, string prompt);

    void OnAgentCompleted(string agentName, string correlationId, bool success, TimeSpan duration, int toolCallCount);
}

public sealed class OpenTelemetryRuntimeObserver : IToolExecutionObserver, IAgentExecutionObserver, IDisposable
{
    public const string ActivitySourceName = "Template.AgentRuntime";
    public const string MeterName = "Template.AgentRuntime";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _toolCallCounter;
    private readonly Counter<long> _toolFailureCounter;
    private readonly Histogram<double> _toolDurationHistogram;
    private readonly Counter<long> _agentTurnCounter;
    private readonly ConcurrentDictionary<string, Activity> _activeTurns = new(StringComparer.Ordinal);

    public OpenTelemetryRuntimeObserver()
    {
        _toolCallCounter = _meter.CreateCounter<long>("tool.calls");
        _toolFailureCounter = _meter.CreateCounter<long>("tool.failures");
        _toolDurationHistogram = _meter.CreateHistogram<double>("tool.duration.ms");
        _agentTurnCounter = _meter.CreateCounter<long>("agent.turns");
    }

    public IDisposable BeginAgentTurn(string agentName, string correlationId, string prompt)
    {
        var activity = _activitySource.StartActivity("agent.turn", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("agent.name", agentName);
            activity.SetTag("agent.correlation_id", correlationId);
            activity.SetTag("agent.prompt", prompt);
            _activeTurns[correlationId] = activity;
        }

        _agentTurnCounter.Add(1, KeyValuePair.Create<string, object?>("agent.name", agentName));
        return new Scope(() => EndTurn(correlationId));
    }

    public void OnAgentCompleted(string agentName, string correlationId, bool success, TimeSpan duration, int toolCallCount)
    {
        if (_activeTurns.TryGetValue(correlationId, out var activity))
        {
            activity.SetTag("agent.success", success);
            activity.SetTag("agent.duration_ms", duration.TotalMilliseconds);
            activity.SetTag("agent.tool_calls", toolCallCount);
        }
    }

    public void OnAttemptStarted(ToolCallRequest request, int attempt)
    {
        _toolCallCounter.Add(1,
            KeyValuePair.Create<string, object?>("tool.name", request.ToolName),
            KeyValuePair.Create<string, object?>("tool.attempt", attempt));
    }

    public void OnAttemptCompleted(ToolCallRequest request, ToolCallResult result)
    {
        _toolDurationHistogram.Record(result.Duration.TotalMilliseconds,
            KeyValuePair.Create<string, object?>("tool.name", request.ToolName));

        if (!result.Success)
        {
            _toolFailureCounter.Add(1,
                KeyValuePair.Create<string, object?>("tool.name", request.ToolName),
                KeyValuePair.Create<string, object?>("error.code", result.Failure?.Code.ToString()));
        }
    }

    public void Dispose()
    {
        foreach (var correlationId in _activeTurns.Keys)
        {
            EndTurn(correlationId);
        }

        _activitySource.Dispose();
        _meter.Dispose();
    }

    private void EndTurn(string correlationId)
    {
        if (_activeTurns.TryRemove(correlationId, out var activity))
        {
            activity.Stop();
            activity.Dispose();
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;

        public Scope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDispose();
            }
        }
    }
}
