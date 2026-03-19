using System.Collections.Concurrent;
using System.Reflection;

namespace Template.Agents;

internal static class InstructionTemplateLoader
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string LoadTemplate(string fileName)
    {
        return Cache.GetOrAdd(fileName, LoadFromAssembly);
    }

    private static string LoadFromAssembly(string fileName)
    {
        var assembly = typeof(InstructionTemplateLoader).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded instruction resource not found: {fileName}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open embedded instruction resource: {resourceName}");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
