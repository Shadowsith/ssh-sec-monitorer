using Avalonia;

namespace Configurator;

internal sealed class Program
{
    // Avalonia configuration - don't use any Avalonia, third-party APIs or SynchronizationContext before AppMain is called.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
