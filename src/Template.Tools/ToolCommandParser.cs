using System.Text;

namespace Template.Tools;

public sealed class ToolCommandParser : IToolCommandParser
{
    public ToolCommandParseResult Parse(string input)
    {
        if (!input.StartsWith("/tool", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolCommandParseResult(false, true, null, new Dictionary<string, string>(), null);
        }

        var tokenizeResult = Tokenize(input);
        if (!tokenizeResult.Success)
        {
            return new ToolCommandParseResult(true, false, null, new Dictionary<string, string>(), tokenizeResult.Error);
        }

        var tokens = tokenizeResult.Tokens;
        if (tokens.Count < 2)
        {
            return new ToolCommandParseResult(true, false, null, new Dictionary<string, string>(), "Tool command is missing the tool name.");
        }

        if (!string.Equals(tokens[0], "/tool", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolCommandParseResult(true, false, null, new Dictionary<string, string>(), "Command must start with '/tool'.");
        }

        var toolName = tokens[1];
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return new ToolCommandParseResult(true, false, null, new Dictionary<string, string>(), "Tool name cannot be empty.");
        }

        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 2; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                return new ToolCommandParseResult(true, false, toolName, arguments, $"Invalid argument token '{token}'. Expected key=value format.");
            }

            var key = token[..separator].Trim();
            var value = token[(separator + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                return new ToolCommandParseResult(true, false, toolName, arguments, "Argument key cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return new ToolCommandParseResult(true, false, toolName, arguments, $"Argument '{key}' cannot be empty.");
            }

            if (!arguments.TryAdd(key, value))
            {
                return new ToolCommandParseResult(true, false, toolName, arguments, $"Duplicate argument '{key}' was provided.");
            }
        }

        return new ToolCommandParseResult(true, true, toolName, arguments, null);
    }

    private static TokenizeResult Tokenize(string input)
    {
        var tokens = new List<string>();
        var buffer = new StringBuilder();
        var inQuotes = false;
        var quoteCharacter = '\0';
        var escaping = false;

        foreach (var character in input)
        {
            if (escaping)
            {
                buffer.Append(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (inQuotes)
            {
                if (character == quoteCharacter)
                {
                    inQuotes = false;
                    continue;
                }

                buffer.Append(character);
                continue;
            }

            if (character == '"' || character == '\'')
            {
                inQuotes = true;
                quoteCharacter = character;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                FlushToken(tokens, buffer);
                continue;
            }

            buffer.Append(character);
        }

        if (escaping)
        {
            return new TokenizeResult(false, tokens, "Command ends with an unfinished escape sequence.");
        }

        if (inQuotes)
        {
            return new TokenizeResult(false, tokens, "Command has an unterminated quoted value.");
        }

        FlushToken(tokens, buffer);
        return new TokenizeResult(true, tokens, null);
    }

    private static void FlushToken(List<string> tokens, StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        tokens.Add(buffer.ToString());
        buffer.Clear();
    }

    private sealed record TokenizeResult(bool Success, IReadOnlyList<string> Tokens, string? Error);
}
