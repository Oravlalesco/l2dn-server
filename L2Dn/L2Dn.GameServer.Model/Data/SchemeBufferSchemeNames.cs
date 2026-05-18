using System.Text.RegularExpressions;
using L2Dn.Extensions;

namespace L2Dn.GameServer.Data;

public static partial class SchemeBufferSchemeNames
{
    public const int MaxNameLength = 14;

    private static readonly Regex ValidNamePattern = ValidNameRegex();

    public static string? Normalize(string? name)
    {
        if (name == null)
            return null;

        string trimmed = name.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static bool IsValidFormat(string? name, out string? error)
    {
        error = null;
        string? normalized = Normalize(name);
        if (normalized == null)
        {
            error = "empty";
            return false;
        }

        if (normalized.Length > MaxNameLength)
        {
            error = "length";
            return false;
        }

        if (normalized.Contains(';') || normalized.Contains('"') || normalized.Contains('<'))
        {
            error = "forbidden";
            return false;
        }

        string alphanumericCheck = normalized.Replace(" ", "").Replace(".", "").Replace(",", "").Replace("-", "")
            .Replace("+", "").Replace("!", "").Replace("?", "");
        if (!alphanumericCheck.ContainsAlphaNumericOnly())
        {
            error = "alphanumeric";
            return false;
        }

        if (!ValidNamePattern.IsMatch(normalized))
        {
            error = "pattern";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9 _.,+\-!?]{1,14}$")]
    private static partial Regex ValidNameRegex();
}
