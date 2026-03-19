using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Template.Agents;
using Template.Tools;

namespace Template.Scenarios.TranslateText;

public sealed class TranslateTextScenarioAgent : IAgent
{
    private readonly string? _endpoint;
    private readonly string _deploymentName;
    private readonly StandaloneTranslationAgentRegistry _standaloneAgentRegistry;
    private readonly ToolCommandParser _parser;
    private readonly ToolRuntime _runtime;

    public TranslateTextScenarioAgent(
        string? endpoint,
        string deploymentName,
        StandaloneTranslationAgentRegistry standaloneAgentRegistry,
        ToolCommandParser parser,
        ToolRuntime runtime)
    {
        _endpoint = endpoint;
        _deploymentName = deploymentName;
        _standaloneAgentRegistry = standaloneAgentRegistry;
        _parser = parser;
        _runtime = runtime;
    }

    public string Name => "TranslateText";

    public IReadOnlyList<TranslatorLookupResult> TranslatorToolCalls { get; private set; } = [];

    public AgentWorkflowContextResult? LatestWorkflowContext { get; private set; }

    public async Task<AgentTurnOutput> RunAsync(AgentTurnInput input, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var workflowContext = await BuildAgentWorkflowContextAsync(input.UserPrompt, cancellationToken).ConfigureAwait(false);
        LatestWorkflowContext = workflowContext;

        var translatorProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lookups = new List<TranslatorLookupResult>();

        foreach (var language in _standaloneAgentRegistry.GetLanguages())
        {
            var lookup = await ResolveTranslatorProfileAsync(language, _parser, _runtime, cancellationToken).ConfigureAwait(false);
            translatorProfiles[language] = lookup.TranslatorName;
            lookups.Add(lookup);
        }

        TranslatorToolCalls = lookups;

        if (IsAdversarialPrompt(input.UserPrompt))
        {
            var deniedRequest = new ToolCallRequest(
                CorrelationId: input.CorrelationId,
                ToolName: "mcp.lookup_official_translator",
                Arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["language"] = "French"
                },
                RequestedAtUtc: DateTimeOffset.UtcNow,
                Context: new ToolExecutionContext(ToolExecutionMode.Admin, "adversarial-safety-check", ToolRiskLevel.High));

            var deniedResult = await _runtime.Executor.ExecuteAsync(deniedRequest, cancellationToken).ConfigureAwait(false);

            var adversarialToolResults = ToolResultsFromLookups(lookups)
                .Concat(new[] { deniedResult })
                .ToArray();

            return new AgentTurnOutput(
                Success: false,
                ResponseText: "Tool invocation failed: DeniedByPolicy",
                InvokedTools: ["mcp.lookup_official_translator"],
                ToolResults: adversarialToolResults,
                Duration: DateTime.UtcNow - started,
                ErrorCode: ToolErrorCode.DeniedByPolicy.ToString());
        }

        IReadOnlyList<ChatMessage> messages;
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            messages = BuildFallbackMessages(input.UserPrompt, translatorProfiles);
        }
        else
        {
            try
            {
                var client = new AzureOpenAIClient(new Uri(_endpoint), new DefaultAzureCredential())
                    .GetChatClient(_deploymentName)
                    .AsIChatClient();

                var translationAgentSpecs = _standaloneAgentRegistry.CreateSpecs(translatorProfiles, workflowContext.WorkflowGuidance);
                var translationAgents =
                    from spec in translationAgentSpecs
                    select new ChatClientAgent(client, spec.Instructions);

                var workflow = AgentWorkflowBuilder.BuildConcurrent(translationAgents);
                var initialMessages = new List<ChatMessage>
                {
                    new(ChatRole.User, BuildWorkflowPrompt(input.UserPrompt, workflowContext.WorkflowGuidance))
                };

                await using var run = await InProcessExecution.RunStreamingAsync(workflow, initialMessages).ConfigureAwait(false);
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

                var outputMessages = new List<ChatMessage>();
                await foreach (var evt in run.WatchStreamAsync().ConfigureAwait(false))
                {
                    if (evt is WorkflowOutputEvent outputEvent)
                    {
                        outputMessages = outputEvent.As<List<ChatMessage>>()!;
                        break;
                    }
                }

                messages = outputMessages;
            }
            catch
            {
                messages = BuildFallbackMessages(input.UserPrompt, translatorProfiles);
            }
        }

        var duration = DateTime.UtcNow - started;
        var responseText = string.Join("\n", messages.Where(value => value.Role == ChatRole.Assistant).Select(value => value.Text));
        var invokedTools = lookups
            .Select(value => value.ToolName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var toolResults = ToolResultsFromLookups(lookups);
        var success = toolResults.All(value => value.Success);
        var errorCode = success ? null : toolResults.First(value => !value.Success).Failure?.Code.ToString();

        return new AgentTurnOutput(
            Success: success,
            ResponseText: responseText,
            InvokedTools: invokedTools,
            ToolResults: toolResults,
            Duration: duration,
            ErrorCode: errorCode);
    }

    private static IReadOnlyList<ChatMessage> BuildFallbackMessages(string prompt, IReadOnlyDictionary<string, string> translatorProfiles)
    {
        if (prompt.Contains("How are you?", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new ChatMessage(ChatRole.User, prompt),
                new ChatMessage(ChatRole.Assistant, $"{translatorProfiles["French"]}: English detected. Comment ca va ?"),
                new ChatMessage(ChatRole.Assistant, $"{translatorProfiles["Spanish"]}: English detected. ?Como estas?")
            ];
        }

        return
        [
            new ChatMessage(ChatRole.User, prompt),
            new ChatMessage(ChatRole.Assistant, $"{translatorProfiles["French"]}: English detected. Bonjour, le monde !"),
            new ChatMessage(ChatRole.Assistant, $"{translatorProfiles["Spanish"]}: English detected. Hola, mundo!")
        ];
    }

    private static bool IsAdversarialPrompt(string prompt)
    {
        return prompt.Contains("ignore all safety", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("admin tool", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("modify files", StringComparison.OrdinalIgnoreCase);
    }

    private static ToolCallResult[] ToolResultsFromLookups(IReadOnlyList<TranslatorLookupResult> lookups)
    {
        return lookups.Select(MapToolCallResult).ToArray();
    }

    private static string BuildWorkflowPrompt(string originalPrompt, string workflowGuidance) =>
        $"User request: {originalPrompt}\nWorkflow guidance: {workflowGuidance}";

    private static async Task<AgentWorkflowContextResult> BuildAgentWorkflowContextAsync(string userPrompt, CancellationToken cancellationToken)
    {
        var workerPrompts = new (IAgentWorker Worker, string Prompt)[]
        {
            (new PlanningWorker(), $"Create a compact execution plan for handling this user request: {userPrompt}"),
            (new SafetyWorker(), $"List mandatory safety gates for handling this user request: {userPrompt}"),
            (new GeneralWorker(), $"Summarize the user intent in one sentence: {userPrompt}")
        };

        var results = new List<WorkerContextResult>();
        foreach (var entry in workerPrompts)
        {
            var response = await entry.Worker.GenerateAsync(entry.Prompt, cancellationToken).ConfigureAwait(false);
            results.Add(new WorkerContextResult(entry.Worker.Name, entry.Prompt, response));
        }

        var workflowGuidance = string.Join(" | ", results.Select(value => $"{value.Worker}: {value.Response}"));
        return new AgentWorkflowContextResult(workflowGuidance, results);
    }

    private static ToolCallResult MapToolCallResult(TranslatorLookupResult lookup)
    {
        var toolName = lookup.ToolName ?? "mcp.lookup_official_translator";
        if (lookup.ExecutionSuccess)
        {
            return new ToolCallResult(
                ToolName: toolName,
                Success: true,
                Output: lookup.TranslatorName,
                Failure: null,
                AttemptCount: lookup.AttemptCount,
                Duration: TimeSpan.Zero,
                ApprovalRequired: lookup.ApprovalRequired,
                Provider: lookup.Provider);
        }

        var failureCode = Enum.TryParse<ToolErrorCode>(lookup.FailureCode, ignoreCase: true, out var parsed)
            ? parsed
            : ToolErrorCode.ExecutionFailed;

        return new ToolCallResult(
            ToolName: toolName,
            Success: false,
            Output: string.Empty,
            Failure: new ToolExecutionFailure(failureCode, lookup.FailureCode ?? "translator lookup failed"),
            AttemptCount: lookup.AttemptCount,
            Duration: TimeSpan.Zero,
            ApprovalRequired: lookup.ApprovalRequired,
            Provider: lookup.Provider);
    }

    private static async Task<TranslatorLookupResult> ResolveTranslatorProfileAsync(
        string language,
        ToolCommandParser parser,
        ToolRuntime runtime,
        CancellationToken cancellationToken)
    {
        var context = new ToolExecutionContext(
            Mode: ToolExecutionMode.Translation,
            Intent: "translator-profile-lookup",
            RiskLevel: ToolRiskLevel.Elevated);

        var availableTools = GetAvailableTools(runtime.Registry, runtime.Policy, context);
        if (!availableTools.Contains("mcp.lookup_official_translator", StringComparer.OrdinalIgnoreCase))
        {
            return new TranslatorLookupResult(
                Language: language,
                Command: "<selection_blocked>",
                ParseSuccess: false,
                ToolName: "mcp.lookup_official_translator",
                ExecutionSuccess: false,
                FailureCode: "ToolFilteredByContext",
                AttemptCount: 0,
                ApprovalRequired: false,
                Provider: null,
                TranslatorName: GetDefaultTranslatorName(language));
        }

        var command = BuildTranslatorProfileToolCommand(language);
        var parseResult = parser.Parse(command);

        if (!parseResult.IsToolCommand || !parseResult.Success || parseResult.ToolName is null)
        {
            return new TranslatorLookupResult(
                Language: language,
                Command: command,
                ParseSuccess: parseResult.Success,
                ToolName: parseResult.ToolName,
                ExecutionSuccess: false,
                FailureCode: "ParseFailure",
                AttemptCount: 0,
                ApprovalRequired: false,
                Provider: null,
                TranslatorName: GetDefaultTranslatorName(language));
        }

        var request = new ToolCallRequest(
            CorrelationId: $"corr-{Guid.NewGuid():N}",
            ToolName: parseResult.ToolName,
            Arguments: parseResult.Arguments,
            RequestedAtUtc: DateTimeOffset.UtcNow,
            Context: context);

        var result = await runtime.Executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        var name = result.Success && !string.IsNullOrWhiteSpace(result.Output)
            ? result.Output
            : GetDefaultTranslatorName(language);

        return new TranslatorLookupResult(
            Language: language,
            Command: command,
            ParseSuccess: true,
            ToolName: parseResult.ToolName,
            ExecutionSuccess: result.Success,
            FailureCode: result.Failure?.Code.ToString(),
            AttemptCount: result.AttemptCount,
            ApprovalRequired: result.ApprovalRequired,
            Provider: result.Provider,
            TranslatorName: name);
    }

    private static IReadOnlyCollection<string> GetAvailableTools(
        IToolRegistry registry,
        IToolPolicy policy,
        ToolExecutionContext context)
    {
        var available = new List<string>();
        foreach (var definition in registry.List())
        {
            var previewRequest = new ToolCallRequest(
                CorrelationId: "preview",
                ToolName: definition.Name,
                Arguments: new Dictionary<string, string>(),
                RequestedAtUtc: DateTimeOffset.UtcNow,
                Context: context);

            if (policy.TryAuthorize(definition, previewRequest, out _))
            {
                available.Add(definition.Name);
            }
        }

        return available;
    }

    private static string BuildTranslatorProfileToolCommand(string language)
    {
        var escaped = language.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"/tool mcp.lookup_official_translator language=\"{escaped}\"";
    }

    private static string GetDefaultTranslatorName(string language) => language.ToLowerInvariant() switch
    {
        "french" => "Claire Dubois",
        "spanish" => "Alejandro Garcia",
        _ => "Jordan Lee"
    };
}

public sealed record ToolRuntime(IToolRegistry Registry, IToolPolicy Policy, SafeToolExecutor Executor, IToolExecutionObserver Observer);

public sealed record TranslatorLookupResult(
    string Language,
    string Command,
    bool ParseSuccess,
    string? ToolName,
    bool ExecutionSuccess,
    string? FailureCode,
    int AttemptCount,
    bool ApprovalRequired,
    string? Provider,
    string TranslatorName);

public sealed record WorkerContextResult(string Worker, string Prompt, string Response);

public sealed record AgentWorkflowContextResult(string WorkflowGuidance, IReadOnlyList<WorkerContextResult> Workers);
