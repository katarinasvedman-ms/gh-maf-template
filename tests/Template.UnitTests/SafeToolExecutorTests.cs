using Template.Tools;

namespace Template.UnitTests;

public sealed class SafeToolExecutorTests
{
    [Fact]
    public void ToolCommandParser_ParsesQuotedArguments()
    {
        var parser = new ToolCommandParser();

        var result = parser.Parse("/tool mcp.search_docs query=\"retry policies\" source='internal docs'");

        Assert.True(result.IsToolCommand);
        Assert.True(result.Success);
        Assert.Equal("mcp.search_docs", result.ToolName);
        Assert.Equal("retry policies", result.Arguments["query"]);
        Assert.Equal("internal docs", result.Arguments["source"]);
    }

    [Fact]
    public void ToolCommandParser_RejectsMalformedArguments()
    {
        var parser = new ToolCommandParser();

        var result = parser.Parse("/tool echo invalidArg");

        Assert.True(result.IsToolCommand);
        Assert.False(result.Success);
        Assert.Contains("key=value", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUnknownTool_WhenToolNotRegistered()
    {
        var executor = CreateExecutor(new InMemoryToolRegistry(), allowedTools: new[] { "echo" });
        var request = new ToolCallRequest("corr", "missing", new Dictionary<string, string>(), DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.UnknownTool, result.Failure?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_DeniesTool_WhenPolicyBlocksTool()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(CreateTool("echo", ToolSensitivity.Low, _ => ToolCallResult.Ok("echo", "ok", 1, TimeSpan.Zero)));

        var executor = CreateExecutor(registry, allowedTools: new[] { "different" });
        var request = new ToolCallRequest("corr", "echo", new Dictionary<string, string> { ["text"] = "hello" }, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.DeniedByPolicy, result.Failure?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_FailsValidation_WhenRequiredArgumentMissing()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(CreateTool("echo", ToolSensitivity.Low, _ => ToolCallResult.Ok("echo", "ok", 1, TimeSpan.Zero)));

        var executor = CreateExecutor(registry, allowedTools: new[] { "echo" });
        var request = new ToolCallRequest("corr", "echo", new Dictionary<string, string>(), DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.InvalidArguments, result.Failure?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesTransientFailure_ThenSucceeds()
    {
        var attempts = 0;
        var registry = new InMemoryToolRegistry();
        registry.Register(CreateTool("echo", ToolSensitivity.Low, _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                return ToolCallResult.Failed("echo", ToolErrorCode.TransientFailure, "flaky", 1, TimeSpan.Zero);
            }

            return ToolCallResult.Ok("echo", "ok", 1, TimeSpan.Zero);
        }));

        var executor = CreateExecutor(registry, allowedTools: new[] { "echo" });
        var request = new ToolCallRequest("corr", "echo", new Dictionary<string, string> { ["text"] = "hello" }, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, result.AttemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTimeout_WhenToolExceedsTimeout()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(new LocalFunctionTool(
            new ToolDefinition("slow", "slow op", new[] { "text" }, ToolSensitivity.Low, TimeSpan.FromMilliseconds(10)),
            static async (request, cancellationToken) =>
            {
                await Task.Delay(200, cancellationToken);
                return ToolCallResult.Ok(request.ToolName, "done", 1, TimeSpan.Zero);
            }));

        var executor = CreateExecutor(registry, allowedTools: new[] { "slow" });
        var request = new ToolCallRequest("corr", "slow", new Dictionary<string, string> { ["text"] = "hello" }, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.Timeout, result.Failure?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresApproval_ForHighSensitivityTool()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(CreateTool("delete_file", ToolSensitivity.High, _ => ToolCallResult.Ok("delete_file", "done", 1, TimeSpan.Zero), requiredArg: "path"));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "delete_file" }),
            new AutoApproveGate(approveHighSensitivity: false),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));

        var request = new ToolCallRequest("corr", "delete_file", new Dictionary<string, string> { ["path"] = "README.md" }, DateTimeOffset.UtcNow);
        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.ApprovalDenied, result.Failure?.Code);
        Assert.True(result.ApprovalRequired);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsApprovedHighSensitivityTool()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(CreateTool("delete_file", ToolSensitivity.High, _ => ToolCallResult.Ok("delete_file", "done", 1, TimeSpan.Zero), requiredArg: "path"));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "delete_file" }),
            new AutoApproveGate(approveHighSensitivity: true),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));

        var request = new ToolCallRequest("corr", "delete_file", new Dictionary<string, string> { ["path"] = "README.md" }, DateTimeOffset.UtcNow);
        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal("delete_file", result.ToolName);
    }

    [Fact]
    public async Task ParseThenExecute_TranslatorLookupToolCommand_UsesApprovalAndRetry()
    {
        var parser = new ToolCommandParser();
        var parse = parser.Parse("/tool mcp.lookup_official_translator language=\"French\"");

        Assert.True(parse.IsToolCommand);
        Assert.True(parse.Success);
        Assert.Equal("mcp.lookup_official_translator", parse.ToolName);

        var shouldFailTransiently = true;
        var registry = new InMemoryToolRegistry();
        registry.Register(new LocalFunctionTool(
            new ToolDefinition(
                "mcp.lookup_official_translator",
                "translator lookup",
                new[] { "language" },
                ToolSensitivity.High,
                TimeSpan.FromSeconds(1)),
            request =>
            {
                if (shouldFailTransiently)
                {
                    shouldFailTransiently = false;
                    return Task.FromResult(ToolCallResult.Failed(
                        request.ToolName,
                        ToolErrorCode.TransientFailure,
                        "flaky",
                        1,
                        TimeSpan.Zero));
                }

                return Task.FromResult(ToolCallResult.Ok(request.ToolName, "Claire Dubois", 1, TimeSpan.Zero));
            }));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "mcp.lookup_official_translator" }),
            new AutoApproveGate(approveHighSensitivity: true),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));

        var request = new ToolCallRequest(
            "corr",
            parse.ToolName!,
            parse.Arguments,
            DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(result.AttemptCount >= 2);
        Assert.Equal("mcp.lookup_official_translator", result.ToolName);
        Assert.Equal("Claire Dubois", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_DeniesTool_WhenContextModeNotAllowed()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(new LocalFunctionTool(
            new ToolDefinition(
                "mcp.lookup_official_translator",
                "translator lookup",
                new[] { "language" },
                ToolSensitivity.Low,
                TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Looks up translator identity.",
                    UseWhen: ["Language translation context is active."],
                    DoNotUseWhen: ["Admin operations are being executed."],
                    InputDescriptions: new Dictionary<string, string> { ["language"] = "Target language." },
                    OutputDescription: "Translator display name.",
                    Constraints: ["Template mock response only."],
                    SideEffects: ["No side effects."],
                    AllowedModes: [ToolExecutionMode.Translation],
                    MaxRiskLevel: ToolRiskLevel.Elevated)),
            request => Task.FromResult(ToolCallResult.Ok(request.ToolName, "Claire Dubois", 1, TimeSpan.Zero))));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "mcp.lookup_official_translator" }),
            new AutoApproveGate(approveHighSensitivity: true));

        var request = new ToolCallRequest(
            "corr",
            "mcp.lookup_official_translator",
            new Dictionary<string, string> { ["language"] = "French" },
            DateTimeOffset.UtcNow,
            Context: new ToolExecutionContext(ToolExecutionMode.Admin, "admin-path", ToolRiskLevel.High));

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.DeniedByPolicy, result.Failure?.Code);
    }

    [Fact]
    public void Register_Throws_WhenContractMissingRequiredInputDescription()
    {
        var registry = new InMemoryToolRegistry();
        var tool = new LocalFunctionTool(
            new ToolDefinition(
                "mcp.lookup_official_translator",
                "translator lookup",
                new[] { "language" },
                ToolSensitivity.Low,
                TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Looks up translator identity.",
                    UseWhen: ["Language translation context is active."],
                    DoNotUseWhen: ["Admin operations are being executed."],
                    InputDescriptions: new Dictionary<string, string>(),
                    OutputDescription: "Translator display name.",
                    Constraints: ["Template mock response only."],
                    SideEffects: ["No side effects."],
                    AllowedModes: [ToolExecutionMode.Translation],
                    MaxRiskLevel: ToolRiskLevel.Elevated)),
            request => Task.FromResult(ToolCallResult.Ok(request.ToolName, "Claire Dubois", 1, TimeSpan.Zero)));

        Assert.Throws<InvalidOperationException>(() => registry.Register(tool));
    }

    private static SafeToolExecutor CreateExecutor(InMemoryToolRegistry registry, IEnumerable<string> allowedTools)
    {
        return new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(allowedTools),
            new AutoApproveGate(approveHighSensitivity: true),
            retryPolicy: new RetryPolicy(2, TimeSpan.FromMilliseconds(20)));
    }

    private static ITool CreateTool(string toolName, ToolSensitivity sensitivity, Func<ToolCallRequest, ToolCallResult> handler, string requiredArg = "text")
    {
        return new LocalFunctionTool(
            new ToolDefinition(toolName, "test", new[] { requiredArg }, sensitivity, TimeSpan.FromSeconds(1)),
            request => Task.FromResult(handler(request)));
    }

    [Fact]
    public void EffectiveRiskLevel_EscalatesAfterTwoConsecutiveFailures()
    {
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.High, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_DoesNotEscalateWithFewerThanTwoFailures()
    {
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.Elevated, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_DoesNotExceedCritical()
    {
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Critical, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.Critical, context.EffectiveRiskLevel);
    }

    [Fact]
    public async Task ExecuteAsync_DeniesTool_WhenEffectiveRiskLevelExceedsContractMaxRiskLevel()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(new LocalFunctionTool(
            new ToolDefinition(
                "test-tool", "test", new[] { "input" }, ToolSensitivity.Low, TimeSpan.FromSeconds(1),
                Contract: new ToolContract(
                    WhatItDoes: "Test.",
                    UseWhen: ["Always."],
                    DoNotUseWhen: ["Never."],
                    InputDescriptions: new Dictionary<string, string> { ["input"] = "Input." },
                    OutputDescription: "Output.",
                    Constraints: ["None."],
                    SideEffects: ["None."],
                    AllowedModes: [ToolExecutionMode.Unspecified],
                    MaxRiskLevel: ToolRiskLevel.Elevated)),
            request => Task.FromResult(ToolCallResult.Ok(request.ToolName, "ok", 1, TimeSpan.Zero))));

        var executor = new SafeToolExecutor(
            registry,
            new AllowListToolPolicy(new[] { "test-tool" }),
            new AutoApproveGate(approveHighSensitivity: true));

        // Base RiskLevel is Elevated (which equals MaxRiskLevel), but prior failures escalate it to High
        var priorCalls = new[]
        {
            ToolCallResult.Failed("other", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Failed("other", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero)
        };

        var request = new ToolCallRequest(
            "corr", "test-tool",
            new Dictionary<string, string> { ["input"] = "hello" },
            DateTimeOffset.UtcNow,
            Context: new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls));

        var result = await executor.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ToolErrorCode.DeniedByPolicy, result.Failure?.Code);
    }

    [Fact]
    public void EffectiveRiskLevel_DoesNotEscalate_WhenFailuresAreNotConsecutiveAtEnd()
    {
        // [fail, success, fail] → only 1 trailing failure, no escalation
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Ok("tool", "ok", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.Elevated, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_Escalates_WhenTwoTrailingFailuresFollowSuccess()
    {
        // [fail, success, fail, fail] → 2 trailing failures, escalates
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Ok("tool", "ok", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail3", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.High, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_Escalates_WhenSuccessFollowedByTwoFailures()
    {
        // [success, fail, fail] → 2 trailing failures, escalates
        var priorCalls = new[]
        {
            ToolCallResult.Ok("tool", "ok", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.High, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_DoesNotEscalate_WhenTrailingCallIsSuccess()
    {
        // [fail, fail, success] → 0 trailing failures, no escalation
        var priorCalls = new[]
        {
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail1", 1, TimeSpan.Zero),
            ToolCallResult.Failed("tool", ToolErrorCode.ExecutionFailed, "fail2", 1, TimeSpan.Zero),
            ToolCallResult.Ok("tool", "ok", 1, TimeSpan.Zero)
        };

        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: priorCalls);

        Assert.Equal(ToolRiskLevel.Elevated, context.EffectiveRiskLevel);
    }

    [Fact]
    public void EffectiveRiskLevel_DoesNotEscalate_WhenPriorCallsEmpty()
    {
        var context = new ToolExecutionContext(ToolExecutionMode.Unspecified, "test", ToolRiskLevel.Elevated, PriorCallsInTurn: Array.Empty<ToolCallResult>());

        Assert.Equal(ToolRiskLevel.Elevated, context.EffectiveRiskLevel);
    }
}
