using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Monitorer.Services;

/// <summary>Sends mails via SMTP (MailKit) - with or without SMTP authentication.</summary>
public class EmailService
{
    private readonly MailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<MailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool Enabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.Host);

    public IReadOnlyList<string> DefaultRecipients =>
        _settings.Recipients.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Sends an HTML mail. Returns false if mail is disabled or there are no recipients.</summary>
    public async Task<bool> Send(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken ct)
    {
        var to = recipients.ToList();
        if (!Enabled || to.Count == 0)
            return false;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        foreach (var recipient in to)
            message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, ParseSecurity(_settings.Security), ct);
        if (_settings.UseAuth)
            await client.AuthenticateAsync(_settings.UserName, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Mail '{Subject}' sent to {Count} recipient(s)", subject, to.Count);
        return true;
    }

    private static SecureSocketOptions ParseSecurity(string value) => value switch
    {
        "None" => SecureSocketOptions.None,
        "StartTls" => SecureSocketOptions.StartTls,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto
    };
}
