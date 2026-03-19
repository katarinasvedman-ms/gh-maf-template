namespace Template.Agents;

public sealed class StandaloneTranslationAgentRegistry
{
    private readonly IReadOnlyList<IStandaloneTranslationAgent> _agents;

    public StandaloneTranslationAgentRegistry()
        : this([
            new FrenchTranslatorAgent(),
            new SpanishTranslatorAgent()
        ])
    {
    }

    internal StandaloneTranslationAgentRegistry(IReadOnlyList<IStandaloneTranslationAgent> agents)
    {
        _agents = agents;
    }

    public IReadOnlyList<string> GetLanguages() => _agents.Select(agent => agent.TargetLanguage).ToArray();

    public IReadOnlyList<StandaloneTranslationAgentSpec> CreateSpecs(
        IReadOnlyDictionary<string, string> translatorProfiles,
        string workflowGuidance)
    {
        var specs = new List<StandaloneTranslationAgentSpec>(_agents.Count);

        foreach (var agent in _agents)
        {
            if (!translatorProfiles.TryGetValue(agent.TargetLanguage, out var translatorName) || string.IsNullOrWhiteSpace(translatorName))
            {
                throw new InvalidOperationException($"Missing translator profile for language: {agent.TargetLanguage}");
            }

            specs.Add(agent.CreateSpec(translatorName, workflowGuidance));
        }

        return specs;
    }
}
