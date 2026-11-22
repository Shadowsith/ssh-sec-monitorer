using Avalonia.Markup.Xaml;

namespace Configurator.Localization;

/// <summary>XAML: Text="{i18n:T toolbar.save}" with xmlns:i18n="using:Configurator.Localization".</summary>
public class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Texts.Get(Key);
}
