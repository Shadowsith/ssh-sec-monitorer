using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Configurator.ViewModels;

namespace Configurator.Views;

/// <summary>Code-behind only for clipboard / file dialogs (platform services); all logic is in the view model.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnCopyYaml(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Clipboard is null)
            return;
        await Clipboard.SetTextAsync(vm.YamlPreview);
        vm.StatusMessage = "config.yml in die Zwischenablage kopiert.";
    }

    private async void OnImportYaml(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Clipboard is null)
            return;
        vm.ImportYaml(await Clipboard.GetTextAsync() ?? "");
    }

    private async void OnExportYaml(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "config.yml speichern",
            SuggestedFileName = "config.yml",
            DefaultExtension = "yml"
        });
        if (file is null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(vm.YamlPreview.ReplaceLineEndings("\n"));
        vm.StatusMessage = $"Gespeichert: {file.Name}";
    }
}
