using Template.Mcp;
using Template.Tools;

namespace Template.IntegrationTests;

public sealed class McpIntegrationTests
{
    [Fact]
    public async Task McpToolAdapter_ExecutesThroughSafeExecutor()
    {
        var client = new DemoMcpServerClient();
        var catalog = new McpToolCatalog(client);
        var tools = await catalog.CreateAdaptersAsync("docs", CancellationToken.None);

        var registry = new InMemoryToolRegistry();
        foreach (var tool in tools)
        {
            registry.Register(tool);
        }

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "mcp.search_docs" }),
            new AutoApproveGate(approveHighSensitivity: true),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));

        var request = new ToolCallRequest(
            CorrelationId: "corr",
            ToolName: "mcp.search_docs",
            Arguments: new Dictionary<string, string> { ["query"] = "safety" },
            RequestedAtUtc: DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("retrieved", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task McpToolAdapter_ReturnsFailure_WhenServerToolMissing()
    {
        var missingClient = new MissingToolMcpClient();
        var descriptor = new McpToolDescriptor(
            "docs",
            "search_docs",
            "searches docs",
            new[] { "query" },
            ToolSensitivity.Low,
            TimeSpan.FromSeconds(1));

        var registry = new InMemoryToolRegistry();
        registry.Register(new McpToolAdapter(missingClient, descriptor));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "mcp.search_docs" }),
            new AutoApproveGate(approveHighSensitivity: true),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));

        var request = new ToolCallRequest("corr", "mcp.search_docs", new Dictionary<string, string> { ["query"] = "x" }, DateTimeOffset.UtcNow);
        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.ExecutionFailed, result.Failure?.Code);
    }

    [Fact]
    public async Task McpToolCatalog_RegistersMultipleServers_WithGracefulFailure()
    {
        var client = new DemoMcpServerClient();
        var catalog = new McpToolCatalog(client);

        var result = await catalog.CreateAdaptersAsync(
            new[]
            {
                new McpServerDefinition("docs", Required: true),
                new McpServerDefinition("ops", Required: false),
                new McpServerDefinition("unavailable", Required: false)
            },
            CancellationToken.None);

        Assert.True(result.Tools.Count >= 2);
        Assert.Single(result.Failures);
        Assert.Equal("unavailable", result.Failures.Single().ServerId);
    }

    private sealed class MissingToolMcpClient : IMcpServerClient
    {
        public Task<IReadOnlyCollection<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<McpToolDescriptor> value = [];
            return Task.FromResult(value);
        }

        public Task<McpInvocationResult> InvokeToolAsync(string serverId, string toolName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            return Task.FromResult(new McpInvocationResult(false, string.Empty, "Tool is unavailable.", Transient: false));
        }
    }

    private sealed class DemoMcpServerClient : IMcpServerClient
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<McpToolDescriptor>> Catalog
            = new Dictionary<string, IReadOnlyCollection<McpToolDescriptor>>(StringComparer.OrdinalIgnoreCase)
            {
                ["docs"] =
                [
                    new McpToolDescriptor(
                        "docs",
                        "search_docs",
                        "Searches documentation content.",
                        ["query"],
                        ToolSensitivity.Low,
                        TimeSpan.FromSeconds(2))
                ],
                ["ops"] =
                [
                    new McpToolDescriptor(
                        "ops",
                        "service_status",
                        "Returns service status details.",
                        ["service"],
                        ToolSensitivity.Low,
                        TimeSpan.FromSeconds(2))
                ]
            };

        public Task<IReadOnlyCollection<McpToolDescriptor>> ListToolsAsync(string serverId, CancellationToken cancellationToken)
        {
            if (serverId.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Server unavailable");
            }

            return Task.FromResult(Catalog.TryGetValue(serverId, out var descriptors)
                ? descriptors
                : Array.Empty<McpToolDescriptor>() as IReadOnlyCollection<McpToolDescriptor>);
        }

        public Task<McpInvocationResult> InvokeToolAsync(string serverId, string toolName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            if (serverId.Equals("docs", StringComparison.OrdinalIgnoreCase) && toolName.Equals("search_docs", StringComparison.OrdinalIgnoreCase))
            {
                arguments.TryGetValue("query", out var query);
                var response = $"Retrieved documentation snippets for query '{query}'.";
                return Task.FromResult(new McpInvocationResult(true, response, null, Transient: false));
            }

            if (serverId.Equals("ops", StringComparison.OrdinalIgnoreCase) && toolName.Equals("service_status", StringComparison.OrdinalIgnoreCase))
            {
                arguments.TryGetValue("service", out var service);
                var response = $"Service '{service}' is healthy.";
                return Task.FromResult(new McpInvocationResult(true, response, null, Transient: false));
            }

            return Task.FromResult(new McpInvocationResult(false, string.Empty, $"Unknown tool '{toolName}' on server '{serverId}'.", Transient: false));
        }
    }
}
