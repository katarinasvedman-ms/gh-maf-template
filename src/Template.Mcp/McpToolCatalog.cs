using Template.Tools;

namespace Template.Mcp;

public sealed class McpToolCatalog
{
    private readonly IMcpServerClient _client;

    public McpToolCatalog(IMcpServerClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyCollection<ITool>> CreateAdaptersAsync(string serverId, CancellationToken cancellationToken)
    {
        var descriptors = await _client.ListToolsAsync(serverId, cancellationToken).ConfigureAwait(false);
        return descriptors.Select(descriptor => (ITool)new McpToolAdapter(_client, descriptor)).ToArray();
    }

    public async Task<McpRegistrationResult> CreateAdaptersAsync(
        IReadOnlyCollection<McpServerDefinition> servers,
        CancellationToken cancellationToken)
    {
        var tools = new List<ITool>();
        var failures = new List<McpRegistrationFailure>();

        foreach (var server in servers)
        {
            try
            {
                var serverTools = await CreateAdaptersAsync(server.ServerId, cancellationToken).ConfigureAwait(false);
                tools.AddRange(serverTools);
            }
            catch (Exception ex)
            {
                failures.Add(new McpRegistrationFailure(server.ServerId, ex.Message, server.Required, Transient: false));
            }
        }

        return new McpRegistrationResult(tools, failures);
    }
}
