using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Configurator.Extensions;
using Configurator.ViewModels;
using Configurator.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Configurator;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = new ServiceCollection().AddConfiguratorServices().BuildServiceProvider();
        _services.MigrateDatabase();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // CommunityToolkit validation is used instead of Avalonia's DataAnnotations plugin
            BindingPlugins.DataValidators.RemoveAt(0);
            desktop.MainWindow = new MainWindow { DataContext = _services.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
