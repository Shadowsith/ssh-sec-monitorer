using System.Text.RegularExpressions;

namespace Monitorer.Extensions;

/// <summary>String helpers.</summary>
public static class StringExtensions
{
    /// <summary>
    /// Wildcard match (* and ?), case-insensitive. Several patterns can be separated by ";" or ",".
    /// An empty pattern matches everything.
    /// </summary>
    public static bool MatchesWildcard(this string value, string? patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
            return true;
        return patterns
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(p => Regex.IsMatch(value,
                "^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
    }

    public static List<string> SplitList(this string? value) =>
        (value ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
