using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → host.disks</summary>
public partial class DiskSection : ObservableObject
{
    [ObservableProperty] private bool _enabled = true;

    /// <summary>Host mount points; empty = auto-discover.</summary>
    public List<string> Mounts { get; set; } = [];
}
