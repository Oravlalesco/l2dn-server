using System.Globalization;

namespace L2Dn.GameServer.Data;

public readonly struct SchemeBufferBypass
{
    public string Command { get; init; }
    public IReadOnlyList<string> Tokens { get; init; }

    public static bool TryParse(string commandValue, out SchemeBufferBypass bypass)
    {
        bypass = default;
        if (string.IsNullOrEmpty(commandValue))
            return false;

        string normalized = commandValue.Replace("createscheme ", "createscheme;", StringComparison.Ordinal);
        string[] tokens = normalized.Split(';', StringSplitOptions.None);
        if (tokens.Length == 0 || string.IsNullOrEmpty(tokens[0]))
            return false;

        bypass = new SchemeBufferBypass
        {
            Command = tokens[0],
            Tokens = tokens
        };
        return true;
    }

    public string TokenAt(int index) => index < Tokens.Count ? Tokens[index] : string.Empty;

    public bool TryParseInt(int index, out int value) =>
        int.TryParse(TokenAt(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
