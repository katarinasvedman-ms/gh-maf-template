using System.Text.Json;
using Template.Agents;
using Template.Tools;

namespace Template.UnitTests;

public sealed class AgentTurnOutputTests
{
    [Fact]
    public void ToFoundryEvalPayload_IncludesToolCallEntries_MatchingInvokedTools()
    {
        var output = new AgentTurnOutput(
            Success: true,
            ResponseText: "Translation complete.",
            InvokedTools: ["mcp.lookup_official_translator", "mcp.translate"],
            ToolResults:
            [
                ToolCallResult.Ok("mcp.lookup_official_translator", "Claire Dubois", 1, TimeSpan.Zero) with { CorrelationId = "corr-1" },
                ToolCallResult.Ok("mcp.translate", "Bonjour", 1, TimeSpan.Zero) with { CorrelationId = "corr-2" }
            ],
            Duration: TimeSpan.FromMilliseconds(100));

        var json = output.ToFoundryEvalPayload("Translate hello to French");
        var doc = JsonDocument.Parse(json);

        var response = doc.RootElement.GetProperty("response");
        var toolCallMessage = response[0];
        Assert.Equal("assistant", toolCallMessage.GetProperty("role").GetString());

        var content = toolCallMessage.GetProperty("content");
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("mcp.lookup_official_translator", content[0].GetProperty("name").GetString());
        Assert.Equal("mcp.translate", content[1].GetProperty("name").GetString());
    }

    [Fact]
    public void ToFoundryEvalPayload_OmitsSystemMessage_WhenSystemPromptIsNull()
    {
        var output = new AgentTurnOutput(
            Success: true,
            ResponseText: "Done.",
            InvokedTools: [],
            ToolResults: [],
            Duration: TimeSpan.FromMilliseconds(10));

        var json = output.ToFoundryEvalPayload("Hello");
        var doc = JsonDocument.Parse(json);

        var query = doc.RootElement.GetProperty("query");
        Assert.Equal(1, query.GetArrayLength());
        Assert.Equal("user", query[0].GetProperty("role").GetString());
    }

    [Fact]
    public void ToFoundryEvalPayload_IncludesSystemMessage_WhenSystemPromptIsProvided()
    {
        var output = new AgentTurnOutput(
            Success: true,
            ResponseText: "Done.",
            InvokedTools: [],
            ToolResults: [],
            Duration: TimeSpan.FromMilliseconds(10));

        var json = output.ToFoundryEvalPayload("Hello", "You are a translator.");
        var doc = JsonDocument.Parse(json);

        var query = doc.RootElement.GetProperty("query");
        Assert.Equal(2, query.GetArrayLength());
        Assert.Equal("system", query[0].GetProperty("role").GetString());
        Assert.Equal("You are a translator.", query[0].GetProperty("content").GetString());
        Assert.Equal("user", query[1].GetProperty("role").GetString());
    }

    [Fact]
    public void ToFoundryEvalPayload_OutputIsValidJson()
    {
        var output = new AgentTurnOutput(
            Success: true,
            ResponseText: "Bonjour, le monde!",
            InvokedTools: ["mcp.lookup_official_translator"],
            ToolResults: [ToolCallResult.Ok("mcp.lookup_official_translator", "Claire Dubois", 1, TimeSpan.Zero)],
            Duration: TimeSpan.FromMilliseconds(50));

        var json = output.ToFoundryEvalPayload("Translate 'Hello, world!' to French", "System prompt");

        // Should not throw
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void ToFoundryEvalPayload_EmptyInvokedTools_ProducesOnlyFinalTextMessage()
    {
        var output = new AgentTurnOutput(
            Success: true,
            ResponseText: "No tools were needed.",
            InvokedTools: [],
            ToolResults: [],
            Duration: TimeSpan.FromMilliseconds(5));

        var json = output.ToFoundryEvalPayload("Simple question");
        var doc = JsonDocument.Parse(json);

        var response = doc.RootElement.GetProperty("response");
        Assert.Equal(1, response.GetArrayLength());
        Assert.Equal("assistant", response[0].GetProperty("role").GetString());
        Assert.Equal("No tools were needed.", response[0].GetProperty("content").GetString());
    }
}
