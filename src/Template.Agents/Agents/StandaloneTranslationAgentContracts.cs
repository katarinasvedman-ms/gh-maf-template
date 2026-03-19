namespace Template.Agents;

public sealed record StandaloneTranslationAgentSpec(string AgentName, string TargetLanguage, string Instructions);

public interface IStandaloneTranslationAgent
{
    string AgentName { get; }

    string TargetLanguage { get; }

    StandaloneTranslationAgentSpec CreateSpec(string translatorName, string workflowGuidance);
}
