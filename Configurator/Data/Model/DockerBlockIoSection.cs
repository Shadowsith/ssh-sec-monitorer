using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → docker.block_io</summary>
public partial class DockerBlockIoSection : ObservableObject
{
    /// <summary>Current disk read / write rate per container.</summary>
    [ObservableProperty] private bool _enabled = true;

    /// <summary>Also the cumulative bytes since container start.</summary>
    [ObservableProperty] private bool _totals = true;
}
