using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>
/// Local agents: an agent on the Monitorer host is read from a mounted directory instead of via SFTP.
/// connection_type Ssh | Local; SSH fields become optional and are only required for Ssh.
/// </summary>
[Migration(8)]
public class AddLocalAgents : Migration
{
    public override void Up()
    {
        Alter.Table("agent")
            .AddColumn("connection_type").AsString(10).NotNullable().WithDefaultValue("Ssh")
            .AddColumn("local_dir").AsString(63).Nullable();

        Alter.Column("host").OnTable("agent").AsString(255).Nullable();
        Alter.Column("port").OnTable("agent").AsInt32().Nullable();
        Alter.Column("ssh_credential_id").OnTable("agent").AsInt32().Nullable();
        Alter.Column("remote_path").OnTable("agent").AsString(500).Nullable();

        Create.Index("ux_agent_local_dir").OnTable("agent").OnColumn("local_dir").Ascending().WithOptions().Unique();

        // FluentMigrator has no syntax for CHECK constraints
        Execute.Sql("""
            ALTER TABLE agent ADD CONSTRAINT ck_agent_connection CHECK (
                (connection_type = 'Ssh' AND host IS NOT NULL AND port IS NOT NULL
                     AND ssh_credential_id IS NOT NULL AND remote_path IS NOT NULL)
                OR (connection_type = 'Local' AND local_dir ~ '^[a-z0-9][a-z0-9_-]{0,62}$')
            );
            """);
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE agent DROP CONSTRAINT ck_agent_connection;");
        Execute.Sql("DELETE FROM agent WHERE connection_type = 'Local';");
        Delete.Index("ux_agent_local_dir").OnTable("agent");
        Alter.Column("host").OnTable("agent").AsString(255).NotNullable();
        Alter.Column("port").OnTable("agent").AsInt32().NotNullable();
        Alter.Column("ssh_credential_id").OnTable("agent").AsInt32().NotNullable();
        Alter.Column("remote_path").OnTable("agent").AsString(500).NotNullable();
        Delete.Column("local_dir").FromTable("agent");
        Delete.Column("connection_type").FromTable("agent");
    }
}
