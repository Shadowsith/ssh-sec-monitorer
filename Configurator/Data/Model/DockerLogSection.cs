using CommunityToolkit.Mvvm.ComponentModel;

namespace Configurator.Data.Model;

/// <summary>config.yml → docker.logs</summary>
public partial class DockerLogSection : ObservableObject
{
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private int _interval = 60;
    [ObservableProperty] private int _tailLines = 20;
    [ObservableProperty] private int _window = 300;
    [ObservableProperty] private int _maxScanLines = 1000;
    [ObservableProperty] private int _maxLineLength = 500;
    [ObservableProperty] private string _errorPattern = @"(?i)\b(error|fatal|panic|exception|critical)\b";
    [ObservableProperty] private string _warningPattern = @"(?i)\bwarn(ing)?\b";
}
