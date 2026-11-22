using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → docker</summary>
public partial class DockerSection : ObservableObject
{
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _baseUrl = "tcp://docker-proxy:2375";
    [ObservableProperty] private int _timeout = 10;
    [ObservableProperty] private int _interval = 30;
    [ObservableProperty] private bool _stats = true;
    [ObservableProperty] private bool _history = true;
    [ObservableProperty] private int _historyRetention = 3600;
    [ObservableProperty] private int _maxWorkers = 4;

    public List<string> Include { get; set; } = [];
    public List<string> Exclude { get; set; } = [];
    public DockerPortSection Ports { get; set; } = new();
    public DockerBlockIoSection BlockIo { get; set; } = new();
    public DockerLogSection Logs { get; set; } = new();
}
