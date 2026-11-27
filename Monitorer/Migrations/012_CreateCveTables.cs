using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>CVE checks (keywords + sources), the CVEs they found, and the alert rule column for them.</summary>
[Migration(12)]
public class CreateCveTables : Migration
{
    public override void Up()
    {
        Create.Table("cve_check")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("keywords").AsCustom("text").NotNullable()
            .WithColumn("providers").AsString(200).NotNullable()
            .WithColumn("interval_minutes").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("baseline_pending").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("kb_last_commit").AsString(64).Nullable()
            .WithColumn("last_status").AsString(20).NotNullable().WithDefaultValue("Unknown")
            .WithColumn("last_checked_at").AsDateTimeOffset().Nullable()
            .WithColumn("last_message").AsCustom("text").Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);

        Create.Table("cve_finding")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("check_id").AsInt32().NotNullable()
                .ForeignKey("fk_cve_finding_check", "cve_check", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("cve_id").AsString(30).NotNullable()
            .WithColumn("providers").AsString(200).NotNullable()
            .WithColumn("keyword").AsString(100).NotNullable()
            .WithColumn("title").AsCustom("text").Nullable()
            .WithColumn("description").AsCustom("text").Nullable()
            .WithColumn("severity").AsString(20).Nullable()
            .WithColumn("cvss").AsDecimal(3, 1).Nullable()
            .WithColumn("published_at").AsDateTimeOffset().Nullable()
            .WithColumn("url").AsString(1000).Nullable()
            .WithColumn("first_seen_at").AsDateTimeOffset().NotNullable()
            .WithColumn("last_seen_at").AsDateTimeOffset().NotNullable()
            .WithColumn("enriched_at").AsDateTimeOffset().Nullable()
            .WithColumn("acknowledged_at").AsDateTimeOffset().Nullable()
            .WithColumn("acknowledged_by").AsString(200).Nullable()
            .WithColumn("is_baseline").AsBoolean().NotNullable().WithDefaultValue(false);

        Create.Index("ux_cve_finding_check_cve").OnTable("cve_finding")
            .OnColumn("check_id").Ascending()
            .OnColumn("cve_id").Ascending()
            .WithOptions().Unique();
        Create.Index("ix_cve_finding_cve").OnTable("cve_finding").OnColumn("cve_id");

        Alter.Table("alert_rule")
            .AddColumn("cve_check_id").AsInt32().Nullable()
                .ForeignKey("fk_alert_rule_cve_check", "cve_check", "id").OnDelete(System.Data.Rule.Cascade);

        // default rule: all CVE checks, one mail per new CVE, no all-clear on acknowledge
        Insert.IntoTable("alert_rule")
            .Row(new { name = "Neue CVE gefunden", scenario = "CveFound", delay_minutes = 0, repeat_minutes = 0, notify_recovery = false });
    }

    public override void Down()
    {
        Delete.FromTable("alert_rule").Row(new { scenario = "CveFound" });
        Delete.ForeignKey("fk_alert_rule_cve_check").OnTable("alert_rule");
        Delete.Column("cve_check_id").FromTable("alert_rule");
        Delete.Table("cve_finding");
        Delete.Table("cve_check");
    }
}
