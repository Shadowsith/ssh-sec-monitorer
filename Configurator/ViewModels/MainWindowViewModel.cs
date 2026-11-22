using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Configurator.Data.Db;
using Configurator.Data.Model;
using Configurator.Services;
using YamlDotNet.Core;

namespace Configurator.ViewModels;

/// <summary>
/// Main window: list of saved configurations, editor for the selected one and a live config.yml preview.
/// List settings (mount points, patterns, ...) are edited as comma / line separated text.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AgentConfigService _configs;
    private readonly ConfigYamlService _yaml;
    private bool _loading;

    public MainWindowViewModel(AgentConfigService configs, ConfigYamlService yaml)
    {
        _configs = configs;
        _yaml = yaml;
        LoadSettings(new AgentSettings(), "", "");
        _ = ReloadList(null);
    }

    public ObservableCollection<AgentConfigDb> Configs { get; } = [];

    [ObservableProperty] private AgentConfigDb? _selectedConfig;
    [ObservableProperty] private AgentSettings _settings = new();
    [ObservableProperty] private string _configName = "";
    [ObservableProperty] private string _configDescription = "";
    [ObservableProperty] private string _yamlPreview = "";
    [ObservableProperty] private string _statusMessage = "Bereit.";
    [ObservableProperty] private string _validationMessage = "";

    // list settings as text
    [ObservableProperty] private string _diskMounts = "";
    [ObservableProperty] private string _networkInterfaces = "";
    [ObservableProperty] private string _networkExclude = "";
    [ObservableProperty] private string _dockerInclude = "";
    [ObservableProperty] private string _dockerExclude = "";

    public bool IsSaved => SelectedConfig is not null;

    // ------------------------------------------------------------------ commands

    [RelayCommand]
    private void New()
    {
        SelectedConfig = null;
        LoadSettings(new AgentSettings(), "", "");
        StatusMessage = "Neue Konfiguration – noch nicht gespeichert.";
    }

    [RelayCommand]
    private async Task Save()
    {
        var name = string.IsNullOrWhiteSpace(ConfigName) ? Settings.Agent.Name : ConfigName;
        var result = await _configs.Save(SelectedConfig?.Id, name, ConfigDescription, Settings);
        if (!result.IsSuccess)
        {
            StatusMessage = "Nicht gespeichert: " + result.Message;
            return;
        }
        await ReloadList(result.Value!.Id);
        StatusMessage = $"\"{result.Value.Name}\" gespeichert.";
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        if (SelectedConfig is null)
        {
            StatusMessage = "Bitte zuerst eine gespeicherte Konfiguration auswählen.";
            return;
        }
        var result = await _configs.Duplicate(SelectedConfig);
        if (result.IsSuccess)
            await ReloadList(result.Value!.Id);
        StatusMessage = result.IsSuccess ? $"Kopie \"{result.Value!.Name}\" angelegt." : result.Message!;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedConfig is null)
            return;
        var name = SelectedConfig.Name;
        await _configs.Delete(SelectedConfig.Id);
        await ReloadList(null);
        New();
        StatusMessage = $"\"{name}\" gelöscht.";
    }

    /// <summary>Loads a pasted config.yml into the editor (as a new, unsaved configuration).</summary>
    public void ImportYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            StatusMessage = "Die Zwischenablage enthält keinen Text.";
            return;
        }
        try
        {
            var settings = _yaml.FromYaml(yaml);
            SelectedConfig = null;
            LoadSettings(settings, settings.Agent.Name, "Importiert");
            StatusMessage = "config.yml importiert – zum Übernehmen speichern.";
        }
        catch (YamlException ex)
        {
            StatusMessage = "Import fehlgeschlagen: " + ex.Message;
        }
    }

    // ------------------------------------------------------------------ change handling

    partial void OnSelectedConfigChanged(AgentConfigDb? value)
    {
        OnPropertyChanged(nameof(IsSaved));
        if (value is not null && !_loading)
        {
            LoadSettings(_configs.ReadSettings(value), value.Name, value.Description);
            StatusMessage = $"\"{value.Name}\" geladen.";
        }
    }

    partial void OnSettingsChanged(AgentSettings? oldValue, AgentSettings newValue)
    {
        if (oldValue is not null)
            foreach (var section in Sections(oldValue))
                section.PropertyChanged -= OnSectionChanged;
        foreach (var section in Sections(newValue))
            section.PropertyChanged += OnSectionChanged;
    }

    partial void OnConfigDescriptionChanged(string value) => UpdatePreview();
    partial void OnDiskMountsChanged(string value) => ApplyList(value, list => Settings.Host.Disks.Mounts = list);
    partial void OnNetworkInterfacesChanged(string value) => ApplyList(value, list => Settings.Host.Network.Interfaces = list);
    partial void OnNetworkExcludeChanged(string value) => ApplyList(value, list => Settings.Host.Network.Exclude = list);
    partial void OnDockerIncludeChanged(string value) => ApplyList(value, list => Settings.Docker.Include = list);
    partial void OnDockerExcludeChanged(string value) => ApplyList(value, list => Settings.Docker.Exclude = list);

    private void OnSectionChanged(object? sender, PropertyChangedEventArgs e) => UpdatePreview();

    private void ApplyList(string text, Action<List<string>> apply)
    {
        if (_loading)
            return;
        apply(text.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList());
        UpdatePreview();
    }

    private void LoadSettings(AgentSettings settings, string name, string description)
    {
        _loading = true;
        Settings = settings;
        ConfigName = name;
        ConfigDescription = description;
        DiskMounts = string.Join(", ", settings.Host.Disks.Mounts);
        NetworkInterfaces = string.Join(", ", settings.Host.Network.Interfaces);
        NetworkExclude = string.Join(", ", settings.Host.Network.Exclude);
        DockerInclude = string.Join(", ", settings.Docker.Include);
        DockerExclude = string.Join(", ", settings.Docker.Exclude);
        _loading = false;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_loading)
            return;
        YamlPreview = _yaml.ToYaml(Settings, ConfigDescription);
        var errors = _yaml.Validate(Settings);
        ValidationMessage = errors.Count == 0 ? "" : "⚠ " + string.Join("  ", errors);
    }

    private async Task ReloadList(int? selectId)
    {
        _loading = true;
        Configs.Clear();
        foreach (var config in await _configs.GetAll())
            Configs.Add(config);
        SelectedConfig = selectId is null ? null : Configs.FirstOrDefault(c => c.Id == selectId);
        _loading = false;
        OnPropertyChanged(nameof(IsSaved));
        if (SelectedConfig is not null)
            LoadSettings(_configs.ReadSettings(SelectedConfig), SelectedConfig.Name, SelectedConfig.Description);
    }

    private static IEnumerable<INotifyPropertyChanged> Sections(AgentSettings s) =>
    [
        s.Agent, s.Host, s.Host.Disks, s.Host.Network, s.Docker, s.Docker.Ports, s.Docker.BlockIo, s.Docker.Logs
    ];
}
