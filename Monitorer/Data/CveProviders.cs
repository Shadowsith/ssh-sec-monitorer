namespace Monitorer.Data;

/// <summary>Sources a CVE check can search.</summary>
public static class CveProviders
{
    /// <summary>BSI CERT-Bund WID: RSS feed + CSAF advisories.</summary>
    public const string Bsi = "Bsi";
    /// <summary>tldrcve.com RSS feed (editorial, few items).</summary>
    public const string TldrCve = "TldrCve";
    /// <summary>app.opencve.io search page.</summary>
    public const string OpenCve = "OpenCve";
    /// <summary>GitHub opencve/opencve-kb: changed CVE files since the last commit seen.</summary>
    public const string OpenCveKb = "OpenCveKb";

    public static readonly string[] All = [Bsi, TldrCve, OpenCve, OpenCveKb];

    public static bool IsValid(string value) => All.Contains(value);

    public static string DisplayName(string provider) => provider switch
    {
        Bsi => "BSI CERT-Bund",
        TldrCve => "TL;DR CVE",
        OpenCve => "OpenCVE",
        OpenCveKb => "OpenCVE KB (GitHub)",
        _ => provider
    };

    /// <summary>Translation key of the short description in the check editor.</summary>
    public static string HintKey(string provider) => "cveChecks.provider." + char.ToLowerInvariant(provider[0]) + provider[1..];

    /// <summary>"Bsi,OpenCve" → valid, distinct keys in the order of All.</summary>
    public static List<string> Parse(string? value)
    {
        var parts = (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return All.Where(p => parts.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
    }
}
