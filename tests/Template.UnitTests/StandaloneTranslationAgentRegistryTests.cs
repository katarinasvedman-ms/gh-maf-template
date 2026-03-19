using Template.Agents;

namespace Template.UnitTests;

public sealed class StandaloneTranslationAgentRegistryTests
{
    [Fact]
    public void CreateSpecs_LoadsTemplatesAndReplacesTokens()
    {
        var registry = new StandaloneTranslationAgentRegistry();
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["French"] = "Claire Dubois",
            ["Spanish"] = "Alejandro Garcia"
        };

        var specs = registry.CreateSpecs(profiles, "planning and safety guidance");

        Assert.Equal(2, specs.Count);

        var french = specs.Single(spec => spec.TargetLanguage == "French");
        Assert.Contains("Claire Dubois", french.Instructions, StringComparison.Ordinal);
        Assert.Contains("French", french.Instructions, StringComparison.Ordinal);
        Assert.Contains("planning and safety guidance", french.Instructions, StringComparison.Ordinal);

        var spanish = specs.Single(spec => spec.TargetLanguage == "Spanish");
        Assert.Contains("Alejandro Garcia", spanish.Instructions, StringComparison.Ordinal);
        Assert.Contains("Spanish", spanish.Instructions, StringComparison.Ordinal);
        Assert.Contains("planning and safety guidance", spanish.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSpecs_ThrowsWhenLanguageProfileIsMissing()
    {
        var registry = new StandaloneTranslationAgentRegistry();
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["French"] = "Claire Dubois"
        };

        var error = Assert.Throws<InvalidOperationException>(() => registry.CreateSpecs(profiles, "guidance"));

        Assert.Contains("Missing translator profile", error.Message, StringComparison.Ordinal);
    }
}
