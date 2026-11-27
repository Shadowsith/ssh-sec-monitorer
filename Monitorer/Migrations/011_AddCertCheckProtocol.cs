using FluentMigrator;

namespace Monitorer.Migrations;

/// <summary>Certificate checks via STARTTLS (SMTP, IMAP, LDAP) besides plain TLS.</summary>
[Migration(11)]
public class AddCertCheckProtocol : Migration
{
    public override void Up() =>
        Alter.Table("cert_check").AddColumn("protocol").AsString(20).NotNullable().WithDefaultValue("Tls");

    public override void Down() => Delete.Column("protocol").FromTable("cert_check");
}
