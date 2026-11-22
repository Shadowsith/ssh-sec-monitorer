using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → host.network</summary>
public partial class NetworkSection : ObservableObject
{
    [ObservableProperty] private bool _enabled = true;

    public List<string> Interfaces { get; set; } = [];
    public List<string> Exclude { get; set; } = ["lo", "veth*", "docker*", "br-*"];
}
