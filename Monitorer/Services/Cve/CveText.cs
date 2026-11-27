using System.Net;
using System.Text.RegularExpressions;

namespace Monitorer.Services.Cve;

/// <summary>Keyword matching and CVE ID extraction shared by the CVE sources.</summary>
public static partial class CveText
{
    [GeneratedRegex(@"CVE-\d{4}-\d{4,}", RegexOptions.IgnoreCase)]
    public static partial Regex CveIdPattern();

    [GeneratedRegex(@"^CVE-\d{4}-\d{4,}$")]
    private static partial Regex ExactCveId();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTag();

    public static bool IsCveId(string? value) => value is not null && ExactCveId().IsMatch(value);

    /// <summary>"cve-2026-1234" → "CVE-2026-1234".</summary>
    public static string Normalize(string cveId) => cveId.Trim().ToUpperInvariant();

    /// <summary>
    /// First keyword found as a whole word (case-insensitive). "Whole word" = no letter / digit right before or after,
    /// so "c++", ".net" or "linux kernel" work, and "go" does not match "google".
    /// Underscores count as spaces ("linux_kernel" matches "linux kernel").
    /// </summary>
    public static string? FirstMatch(string? text, IEnumerable<string> keywords)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        var haystack = text.Replace('_', ' ');
        foreach (var keyword in keywords)
        {
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(keyword.Replace('_', ' '))}(?![\p{{L}}\p{{N}}])";
            if (Regex.IsMatch(haystack, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                return keyword;
        }
        return null;
    }

    /// <summary>HTML of a feed description → plain text.</summary>
    public static string StripHtml(string? html) =>
        string.IsNullOrEmpty(html) ? "" : WebUtility.HtmlDecode(HtmlTag().Replace(html, " "));
}
