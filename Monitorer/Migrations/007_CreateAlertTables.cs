using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>Alert rules (what to watch, delay, repeat interval) and the state of active alerts.</summary>
[Migration(7)]
public class CreateAlertTables : Migration
{
    public override void Up()
    {
        Create.Table("alert_rule")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("scenario").AsString(40).NotNullable()
            .WithColumn("agent_id").AsInt32().Nullable()
                .ForeignKey("fk_alert_rule_agent", "agent", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("live_check_id").AsInt32().Nullable()
                .ForeignKey("fk_alert_rule_live_check", "live_check", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("container_pattern").AsString(255).Nullable()
            .WithColumn("threshold_minutes").AsInt32().Nullable()
            .WithColumn("min_status").AsString(20).Nullable()
            .WithColumn("ignore_clean_exit").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("delay_minutes").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("repeat_minutes").AsInt32().NotNullable().WithDefaultValue(60)
            .WithColumn("notify_recovery").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("recipients").AsString(1000).NotNullable().WithDefaultValue("")
            .WithColumn("enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTimeOffset);

        Create.Table("alert_state")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("rule_id").AsInt32().NotNullable()
                .ForeignKey("fk_alert_state_rule", "alert_rule", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("subject_key").AsString(400).NotNullable()
            .WithColumn("subject_name").AsString(400).NotNullable()
            .WithColumn("message").AsCustom("text").Nullable()
            .WithColumn("first_detected_at").AsDateTimeOffset().NotNullable()
            .WithColumn("last_sent_at").AsDateTimeOffset().Nullable()
            .WithColumn("send_count").AsInt32().NotNullable().WithDefaultValue(0);

        Create.Index("ux_alert_state_rule_subject").OnTable("alert_state")
            .OnColumn("rule_id").Ascending()
            .OnColumn("subject_key").Ascending()
            .WithOptions().Unique();

        // sensible default rules - all VMs / live checks / containers, mails to Mail:Recipients
        Insert.IntoTable("alert_rule")
            .Row(new { name = "VM nicht erreichbar", scenario = "AgentUnreachable", delay_minutes = 5, repeat_minutes = 60 })
            .Row(new { name = "Agent meldet sich nicht mehr", scenario = "AgentStale", threshold_minutes = 10, delay_minutes = 0, repeat_minutes = 60 })
            .Row(new { name = "Live-Dienst nicht erreichbar", scenario = "LiveCheckDown", delay_minutes = 2, repeat_minutes = 30 })
            .Row(new { name = "Container gestoppt / unhealthy", scenario = "ContainerDown", delay_minutes = 5, repeat_minutes = 120 })
            .Row(new { name = "Container verschwunden", scenario = "ContainerMissing", delay_minutes = 10, repeat_minutes = 0 });
    }

    public override void Down()
    {
        Delete.Table("alert_state");
        Delete.Table("alert_rule");
    }
}
