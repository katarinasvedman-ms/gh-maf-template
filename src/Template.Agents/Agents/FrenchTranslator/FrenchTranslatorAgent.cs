namespace Template.Agents;

public sealed class FrenchTranslatorAgent : IStandaloneTranslationAgent
{
    private const string TemplateFileName = "FrenchTranslator.instructions.md";

    public string AgentName => "french-translator";

    public string TargetLanguage => "French";

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
