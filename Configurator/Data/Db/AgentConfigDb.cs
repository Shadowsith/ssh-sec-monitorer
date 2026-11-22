namespace Configurator.Data.Db;

/// <summary>Table "agent_config" (SQLite): one saved agent configuration.</summary>
public class AgentConfigDb
{
    public int Id { get; set; }                         // id
    public string Name { get; set; } = "";              // name
    public string Description { get; set; } = "";       // description
    public string SettingsJson { get; set; } = "{}";    // settings_json (AgentSettings as JSON)
    public string CreatedAt { get; set; } = "";         // created_at (ISO 8601 UTC)
    public string UpdatedAt { get; set; } = "";         // updated_at (ISO 8601 UTC)
}
