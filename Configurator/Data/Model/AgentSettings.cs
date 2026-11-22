namespace Configurator.Data.Model;

/// <summary>
/// The complete agent config.yml. Property names become snake_case keys (YamlDotNet UnderscoredNamingConvention)
/// and must match Agent/ssm_agent/config.py exactly - the agent rejects unknown keys.
/// </summary>
public class AgentSettings
{
    public AgentSection Agent { get; set; } = new();
    public HostSection Host { get; set; } = new();
    public DockerSection Docker { get; set; } = new();
}
