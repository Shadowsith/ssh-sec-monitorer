using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>TLS certificate checks (expiry of the certificate behind host:port) and the alert rule columns for them.</summary>
[Migration(10)]
public class CreateCertCheckTable : Migration
{
    public override void Up()
    {
        Create.Table("cert_check")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("host").AsString(255).NotNullable()
            .WithColumn("port").AsInt32().NotNullable().WithDefaultValue(443)
            .WithColumn("server_name").AsString(255).Nullable()
            .WithColumn("interval_seconds").AsInt32().NotNullable().WithDefaultValue(3600)
            .WithColumn("timeout_seconds").AsInt32().NotNullable().WithDefaultValue(10)
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("last_status").AsString(20).NotNullable().WithDefaultValue("Unknown")
            .WithColumn("last_checked_at").AsDateTimeOffset().Nullable()
            .WithColumn("last_message").AsCustom("text").Nullable()
            .WithColumn("not_before").AsDateTimeOffset().Nullable()
            .WithColumn("not_after").AsDateTimeOffset().Nullable()
            .WithColumn("subject").AsCustom("text").Nullable()
            .WithColumn("issuer").AsCustom("text").Nullable()
            .WithColumn("thumbprint").AsString(100).Nullable()
            .WithColumn("chain_valid").AsBoolean().Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);

        Alter.Table("alert_rule")
            .AddColumn("cert_check_id").AsInt32().Nullable()
                .ForeignKey("fk_alert_rule_cert_check", "cert_check", "id").OnDelete(System.Data.Rule.Cascade)
            .AddColumn("threshold_days").AsInt32().Nullable();

        // default rule: all certificates, 14 days before expiry, one reminder per day
        Insert.IntoTable("alert_rule")
            .Row(new { name = "SSL-Zertifikat läuft ab", scenario = "CertExpiring", threshold_days = 14, delay_minutes = 0, repeat_minutes = 1440 });
    }

    public override void Down()
    {
        Delete.FromTable("alert_rule").Row(new { scenario = "CertExpiring" });
        Delete.ForeignKey("fk_alert_rule_cert_check").OnTable("alert_rule");
        Delete.Column("cert_check_id").Column("threshold_days").FromTable("alert_rule");
        Delete.Table("cert_check");
    }
}
