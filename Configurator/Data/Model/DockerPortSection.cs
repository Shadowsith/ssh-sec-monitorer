using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → docker.ports</summary>
public partial class DockerPortSection : ObservableObject
{
    /// <summary>Report host ports published to the outside.</summary>
    [ObservableProperty] private bool _enabled = true;

    /// <summary>Also report ports bound only to 127.0.0.1 / ::1.</summary>
    [ObservableProperty] private bool _includeLocalhost;
}
