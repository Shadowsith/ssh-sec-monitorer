using FluentMigrator;

namespace Monitorer.Migrations;

[Migration(2)]
public class CreateSshCredentialTable : Migration
{
    public override void Up()
    {
        Create.Table("ssh_credential")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("user_name").AsString(100).NotNullable()
            .WithColumn("auth_type").AsString(20).NotNullable()
            .WithColumn("private_key_enc").AsCustom("text").Nullable()
            .WithColumn("passphrase_enc").AsCustom("text").Nullable()
            .WithColumn("password_enc").AsCustom("text").Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset)
            .WithColumn("updated_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);
    }

    public override void Down() => Delete.Table("ssh_credential");
}
