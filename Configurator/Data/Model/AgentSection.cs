using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → agent</summary>
public partial class AgentSection : ObservableObject
{
    [ObservableProperty] private string _name = "vm-example-01";
    [ObservableProperty] private string _outputFile = "/data/monitoring.json";
    [ObservableProperty] private string _outputFileMode = "0644";
    [ObservableProperty] private int _writeInterval = 15;
}
