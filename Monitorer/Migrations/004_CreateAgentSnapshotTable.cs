using FluentMigrator;

namespace Monitorer.Migrations;

[Migration(4)]
public class CreateAgentSnapshotTable : Migration
{
    public override void Up()
    {
        Create.Table("agent_snapshot")
            .WithColumn("agent_id").AsInt32().PrimaryKey()
                .ForeignKey("fk_agent_snapshot_agent", "agent", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("last_attempt_at").AsDateTimeOffset().NotNullable()
            .WithColumn("fetched_at").AsDateTimeOffset().Nullable()
            .WithColumn("generated_at").AsDateTimeOffset().Nullable()
            .WithColumn("status").AsString(20).NotNullable()
            .WithColumn("message").AsCustom("text").Nullable()
            .WithColumn("payload").AsCustom("jsonb").Nullable()
            .WithColumn("cpu_percent").AsDecimal(6, 2).Nullable()
            .WithColumn("memory_percent").AsDecimal(6, 2).Nullable()
            .WithColumn("disk_percent_max").AsDecimal(6, 2).Nullable()
            .WithColumn("containers_running").AsInt32().Nullable()
            .WithColumn("containers_total").AsInt32().Nullable();
    }

    public override void Down() => Delete.Table("agent_snapshot");
}
