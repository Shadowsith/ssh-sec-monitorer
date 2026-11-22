using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>Agent version from monitoring.json ("agent.version") as summary column, shown on the agent cards.</summary>
[Migration(9)]
public class AddAgentVersionToSnapshot : Migration
{
    public override void Up() =>
        Alter.Table("agent_snapshot").AddColumn("agent_version").AsString(50).Nullable();

    public override void Down() => Delete.Column("agent_version").FromTable("agent_snapshot");
}
