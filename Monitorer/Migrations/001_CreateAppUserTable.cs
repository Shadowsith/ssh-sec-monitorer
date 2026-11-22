using FluentMigrator;

namespace Monitorer.Migrations;

[Migration(1)]
public class CreateAppUserTable : Migration
{
    public override void Up()
    {
        Create.Table("app_user")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("user_name").AsString(100).NotNullable().Unique()
            .WithColumn("display_name").AsString(200).NotNullable()
            .WithColumn("email").AsString(200).Nullable()
            .WithColumn("password_hash").AsString(500).Nullable()
            .WithColumn("role").AsString(20).NotNullable()
            .WithColumn("auth_source").AsString(20).NotNullable()
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("last_login_at").AsDateTimeOffset().Nullable();
    }

    public override void Down() => Delete.Table("app_user");
}
