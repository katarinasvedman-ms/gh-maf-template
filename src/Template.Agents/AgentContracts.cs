using System.Text.Json;
using System.Text.Json.Serialization;
using Template.Tools;

namespace Template.Agents;

public sealed record AgentTurnInput(string CorrelationId, string UserPrompt);

public sealed record AgentTurnOutput(
    bool Success,
    string ResponseText,
    IReadOnlyList<string> InvokedTools,
    IReadOnlyList<ToolCallResult> ToolResults,
    TimeSpan Duration,
    string? ErrorCode = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string ToFoundryEvalPayload(string userPrompt, string? systemPrompt = null)
    {
        var queryMessages = new List<object>();
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            queryMessages.Add(new { role = "system", content = systemPrompt });
        }
        queryMessages.Add(new { role = "user", content = userPrompt });

        var responseMessages = new List<object>();

        if (InvokedTools.Count > 0)
        {
            var toolCallContents = InvokedTools.Select((toolName, index) => new
            {
                type = "tool_call",
                name = toolName,
                tool_call_id = ToolResults.Count > index && ToolResults[index].CorrelationId is not null
                    ? ToolResults[index].CorrelationId
                    : $"call_{index}",
                arguments = new object()
            }).ToArray<object>();

            responseMessages.Add(new { role = "assistant", content = toolCallContents });
        }

        responseMessages.Add(new { role = "assistant", content = ResponseText });

        var payload = new { query = queryMessages, response = responseMessages };
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}

public sealed record AgentContract(
    string Purpose,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> OutOfScope,
    IReadOnlyCollection<string> RequiredTools);

public interface IAgent
{
    string Name { get; }

    AgentContract Contract { get; }

    Task<AgentTurnOutput> RunAsync(AgentTurnInput input, CancellationToken cancellationToken);
}

public interface IAgentWorker
{
    string Name { get; }

    bool CanHandle(string prompt);

    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
