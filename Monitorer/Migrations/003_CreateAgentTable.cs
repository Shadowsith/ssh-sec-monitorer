using FluentMigrator;

namespace Monitorer.Migrations;

[Migration(3)]
public class CreateAgentTable : Migration
{
    public override void Up()
    {
        Create.Table("agent")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("description").AsString(500).NotNullable().WithDefaultValue("")
            .WithColumn("host").AsString(255).NotNullable()
            .WithColumn("port").AsInt32().NotNullable().WithDefaultValue(22)
            .WithColumn("ssh_credential_id").AsInt32().NotNullable()
                .ForeignKey("fk_agent_ssh_credential", "ssh_credential", "id")
            .WithColumn("remote_path").AsString(500).NotNullable()
            .WithColumn("poll_interval_seconds").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("host_key_fingerprint").AsString(200).Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);
    }

    public override void Down() => Delete.Table("agent");
}
