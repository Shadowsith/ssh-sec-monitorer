using FluentMigrator;

namespace Monitorer.Migrations;

[Migration(5)]
public class CreateLiveCheckTable : Migration
{
    public override void Up()
    {
        Create.Table("live_check")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("check_type").AsString(20).NotNullable()
            .WithColumn("target").AsString(1000).NotNullable()
            .WithColumn("interval_seconds").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("timeout_seconds").AsInt32().NotNullable().WithDefaultValue(10)
            .WithColumn("expected_status_code").AsInt32().Nullable()
            .WithColumn("ignore_tls_errors").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("mqtt_user_name").AsString(200).Nullable()
            .WithColumn("mqtt_password_enc").AsCustom("text").Nullable()
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("last_status").AsString(20).NotNullable().WithDefaultValue("Unknown")
            .WithColumn("last_checked_at").AsDateTimeOffset().Nullable()
            .WithColumn("last_response_ms").AsInt32().Nullable()
            .WithColumn("last_message").AsCustom("text").Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);
    }

    public override void Down() => Delete.Table("live_check");
}
