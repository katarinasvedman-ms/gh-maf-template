namespace Template.Agents;

public sealed class SpanishTranslatorAgent : IStandaloneTranslationAgent
{
    private const string TemplateFileName = "SpanishTranslator.instructions.md";

    public string AgentName => "spanish-translator";

    public string TargetLanguage => "Spanish";

    public StandaloneTranslationAgentSpec CreateSpec(string translatorName, string workflowGuidance)
    {
        var template = InstructionTemplateLoader.LoadTemplate(TemplateFileName);
        var instructions = template
            .Replace("{{TranslatorName}}", translatorName, StringComparison.Ordinal)
            .Replace("{{TargetLanguage}}", TargetLanguage, StringComparison.Ordinal)
            .Replace("{{WorkflowGuidance}}", workflowGuidance, StringComparison.Ordinal);

        return new StandaloneTranslationAgentSpec(AgentName, TargetLanguage, instructions);
    }
}
