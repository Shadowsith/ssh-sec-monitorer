using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Configurator.Localization;

/// <summary>
/// UI texts from the embedded files i18n/&lt;language&gt;.json (flat "key": "text", placeholders {0}).
/// The language follows the operating system (CurrentUICulture); unsupported languages and missing keys
/// fall back to English, then to the key itself.
/// </summary>
public static class Texts
{
    private const string DefaultLanguage = "en";
    private const string ResourcePrefix = "Configurator.i18n.";

    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Languages = Load();
    private static readonly FrozenDictionary<string, string> Default = Languages[DefaultLanguage];
    private static readonly FrozenDictionary<string, string> Current = Languages[Pick(CultureInfo.CurrentUICulture)];

    public static string Get(string key) =>
        Current.TryGetValue(key, out var text) || Default.TryGetValue(key, out text) ? text : key;

    public static string Get(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static string Pick(CultureInfo culture)
    {
        for (; culture.Name.Length > 0; culture = culture.Parent)
            if (Languages.ContainsKey(culture.Name))
                return culture.Name;
        return DefaultLanguage;
    }

    private static FrozenDictionary<string, FrozenDictionary<string, string>> Load()
    {
        var assembly = typeof(Texts).Assembly;
        var languages = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix) && n.EndsWith(".json")))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            languages[name[ResourcePrefix.Length..^".json".Length]] = entries.ToFrozenDictionary();
        }
        return languages.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
