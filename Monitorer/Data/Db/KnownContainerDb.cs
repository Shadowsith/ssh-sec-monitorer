namespace Monitorer.Data.Db;

/// <summary>Table "known_container": a container seen at least once on an agent.</summary>
public class KnownContainerDb
{
    public int Id { get; set; }                     // id
    public int AgentId { get; set; }                // agent_id
    public string Name { get; set; } = "";          // name
    public string? Image { get; set; }              // image
    public string? LastState { get; set; }          // last_state
    public DateTime FirstSeenAt { get; set; }       // first_seen_at
    public DateTime LastSeenAt { get; set; }        // last_seen_at
}
