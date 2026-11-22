using FluentMigrator;

namespace Configurator.Migrations;

[Migration(1)]
public class CreateAgentConfigTable : Migration
{
    public override void Up()
    {
        Create.Table("agent_config")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(200).NotNullable().Unique()
            .WithColumn("description").AsString(1000).NotNullable().WithDefaultValue("")
            .WithColumn("settings_json").AsString(int.MaxValue).NotNullable()
            .WithColumn("created_at").AsString(40).NotNullable()
            .WithColumn("updated_at").AsString(40).NotNullable();
    }

    public override void Down() => Delete.Table("agent_config");
}
