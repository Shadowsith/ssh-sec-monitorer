using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → host</summary>
public partial class HostSection : ObservableObject
{
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _procPath = "/host/proc";
    [ObservableProperty] private string _rootfsPath = "/hostfs";
    [ObservableProperty] private int _interval = 10;
    [ObservableProperty] private int _historyRetention = 3600;
    [ObservableProperty] private bool _cpu = true;
    [ObservableProperty] private bool _memory = true;
    [ObservableProperty] private bool _load = true;

    public DiskSection Disks { get; set; } = new();
    public NetworkSection Network { get; set; } = new();
}
