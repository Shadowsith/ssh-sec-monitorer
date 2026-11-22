using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Monitorer.Services;

/// <summary>
/// Translations from the embedded files i18n/&lt;language&gt;.json - one flat object "key": "text" per language,
/// placeholders are string.Format style ({0}). The language is CultureInfo.CurrentUICulture, set per request
/// by the request localization middleware (cookie, Accept-Language) and to the default language for background work.
/// A missing key falls back to the default language, then to the key itself.
/// </summary>
public sealed class JsonStringLocalizer : IStringLocalizer
{
    /// <summary>Keys with these prefixes are handed to the browser scripts (see _Layout and wwwroot/js/i18n.js).</summary>
    public static readonly string[] ScriptPrefixes = ["js.", "status."];

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _languages;
    private readonly FrozenDictionary<string, string> _default;

    public string DefaultLanguage { get; }
    public IReadOnlyList<string> Languages { get; }

    private JsonStringLocalizer(Dictionary<string, FrozenDictionary<string, string>> languages, string defaultLanguage)
    {
        if (!languages.TryGetValue(defaultLanguage, out var fallback))
            throw new InvalidOperationException(
                $"Default language '{defaultLanguage}' has no i18n file (available: {string.Join(", ", languages.Keys)}).");
        _languages = languages.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _default = fallback;
        DefaultLanguage = defaultLanguage;
        Languages = languages.Keys.Order().ToList();
    }

    /// <summary>Reads every embedded i18n/*.json of this assembly.</summary>
    public static JsonStringLocalizer Load(string defaultLanguage)
    {
        var assembly = typeof(JsonStringLocalizer).Assembly;
        const string prefix = "Monitorer.i18n.";
        var languages = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".json")))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                          ?? throw new InvalidOperationException($"Empty translation file {name}.");
            languages[name[prefix.Length..^".json".Length]] = entries.ToFrozenDictionary();
        }
        return new JsonStringLocalizer(languages, defaultLanguage);
    }

    /// <summary>Keys that are missing in a language compared to the default language (for the startup check).</summary>
    public IEnumerable<(string Language, string Key)> MissingKeys() =>
        from language in _languages
        from key in _default.Keys.Except(language.Value.Keys)
        select (language.Key, key);

    public LocalizedString this[string name]
    {
        get
        {
            var found = TryGet(name, out var value);
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = TryGet(name, out var value);
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, value, arguments), !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Current().Select(e => new LocalizedString(e.Key, e.Value));

    /// <summary>All texts of the current language whose key starts with one of the prefixes (default language filled in).</summary>
    public Dictionary<string, string> GetAll(IEnumerable<string> prefixes) =>
        _default.Keys.Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)))
            .ToDictionary(k => k, k => { TryGet(k, out var v); return v; });

    /// <summary>The language that is used for the current culture.</summary>
    public string CurrentLanguage
    {
        get
        {
            for (var culture = CultureInfo.CurrentUICulture; culture.Name.Length > 0; culture = culture.Parent)
                if (_languages.ContainsKey(culture.Name))
                    return culture.Name;
            return DefaultLanguage;
        }
    }

    private FrozenDictionary<string, string> Current() => _languages[CurrentLanguage];

    private bool TryGet(string name, out string value)
    {
        if (Current().TryGetValue(name, out value!) || _default.TryGetValue(name, out value!))
            return true;
        value = name;
        return false;
    }
}
