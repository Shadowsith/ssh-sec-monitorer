using System.Text.Json;

namespace Monitorer.Services.Cve;

/// <summary>The fields of an opencve-kb file (github.com/opencve/opencve-kb, &lt;year&gt;/CVE-….json) we use.</summary>
public record OpenCveKbDocument(string? Title, string? Description, decimal? Cvss, DateTime? PublishedAt, string Products)
{
    public static string RawUrl(string cveId) =>
        $"https://raw.githubusercontent.com/opencve/opencve-kb/main/{cveId.Split('-')[1]}/{cveId}.json";

    /// <summary>Text the keywords are matched against: title, description and the affected vendors / products.</summary>
    public string MatchText => $"{Title}\n{Description}\n{Products}";

    public static OpenCveKbDocument Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var title = Text(root, "opencve", "title", "data") ?? Text(root, "mitre", "title") ?? Text(root, "nvd", "title");
        var description = Text(root, "opencve", "description", "data") ?? Text(root, "mitre", "description")
                          ?? Text(root, "nvd", "description");
        var cvss = Score(root, "opencve", "metrics", "cvssV4_0", "data")
                   ?? Score(root, "opencve", "metrics", "cvssV3_1", "data")
                   ?? Score(root, "opencve", "metrics", "cvssV3_0", "data")
                   ?? Score(root, "nvd", "metrics", "cvssV3_1")
                   ?? Score(root, "vulnrichment", "metrics", "cvssV3_1");
        var created = Text(root, "mitre", "created") ?? Text(root, "nvd", "created");
        DateTime? published = DateTimeOffset.TryParse(created, out var parsed) ? parsed.UtcDateTime : null;
        return new OpenCveKbDocument(title, description, cvss, published, CollectProducts(root));
    }

    /// <summary>"vendor$PRODUCT$product" entries and the CNA's original names, as plain words.</summary>
    private static string CollectProducts(JsonElement root)
    {
        var names = new List<string>();
        foreach (var list in new[] { Find(root, "enrichment", "vendors"), Find(root, "opencve", "vendors", "data") })
        {
            if (list is { ValueKind: JsonValueKind.Array } array)
                names.AddRange(array.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString()!.Replace("$PRODUCT$", " ")));
        }
        if (Find(root, "enrichment", "affected") is { ValueKind: JsonValueKind.Array } affected)
        {
            foreach (var item in affected.EnumerateArray())
            {
                names.Add($"{Text(item, "vendor")} {Text(item, "product")}");
                names.Add($"{Text(item, "original", "vendor")} {Text(item, "original", "product")}");
            }
        }
        return string.Join("\n", names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());
    }

    private static JsonElement? Find(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                return null;
        }
        return current;
    }

    private static string? Text(JsonElement element, params string[] path) =>
        Find(element, path) is { ValueKind: JsonValueKind.String } value && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static decimal? Score(JsonElement element, params string[] path) =>
        Find(element, path) is { ValueKind: JsonValueKind.Object } metric
        && metric.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number
        && score.TryGetDecimal(out var value) && value > 0
            ? value
            : null;
}
