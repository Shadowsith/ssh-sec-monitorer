using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>Every container ever seen per agent - so containers that disappear stay visible (greyed out).</summary>
[Migration(6)]
public class CreateKnownContainerTable : Migration
{
    public override void Up()
    {
        Create.Table("known_container")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("agent_id").AsInt32().NotNullable()
                .ForeignKey("fk_known_container_agent", "agent", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("name").AsString(255).NotNullable()
            .WithColumn("image").AsString(500).Nullable()
            .WithColumn("last_state").AsString(50).Nullable()
            .WithColumn("first_seen_at").AsDateTimeOffset().NotNullable()
            .WithColumn("last_seen_at").AsDateTimeOffset().NotNullable();

        Create.Index("ux_known_container_agent_name").OnTable("known_container")
            .OnColumn("agent_id").Ascending()
            .OnColumn("name").Ascending()
            .WithOptions().Unique();
    }

    public override void Down() => Delete.Table("known_container");
}
