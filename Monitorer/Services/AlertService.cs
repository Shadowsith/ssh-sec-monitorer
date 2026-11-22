using System.Net;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Extensions;

namespace Monitorer.Services;

/// <summary>Builds and sends the alert mails (alarm, reminder, recovery, test).</summary>
public class AlertService
{
    public enum MailKind { Alarm, Reminder, Recovery, Test }

    private static readonly string TemplatePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "MailTemplates", "alert.html");

    private readonly EmailService _email;
    private readonly ILogger<AlertService> _logger;

    public AlertService(EmailService email, ILogger<AlertService> logger)
    {
        _email = email;
        _logger = logger;
    }

    public bool MailEnabled => _email.Enabled;

    /// <summary>The rule's recipients, or Mail:Recipients if the rule has none.</summary>
    public IReadOnlyList<string> RecipientsFor(AlertRuleDb rule)
    {
        var own = rule.Recipients.SplitList();
        return own.Count > 0 ? own : _email.DefaultRecipients;
    }

    /// <summary>Sends one mail; returns false (and logs) if it could not be sent.</summary>
    public async Task<bool> Send(AlertRuleDb rule, AlertStateDb state, MailKind kind, CancellationToken ct)
    {
        var recipients = RecipientsFor(rule);
        if (!_email.Enabled || recipients.Count == 0)
            return false;
        try
        {
            var (label, color) = kind switch
            {
                MailKind.Alarm => ("Alarm", "#dc3545"),
                MailKind.Reminder => ($"Erinnerung ({state.SendCount + 1}.)", "#fd7e14"),
                MailKind.Recovery => ("Behoben", "#198754"),
                _ => ("Test", "#0d6efd")
            };
            var since = state.FirstDetectedAt.ToBerlinString("dd.MM.yyyy HH:mm");
            var repeat = kind == MailKind.Recovery ? "–"
                : rule.RepeatMinutes > 0 ? $"alle {rule.RepeatMinutes} min, solange der Zustand anhält" : "einmalig";

            var template = await File.ReadAllTextAsync(TemplatePath, ct);
            var html = template
                .Replace("{{Label}}", WebUtility.HtmlEncode(label))
                .Replace("{{Color}}", color)
                .Replace("{{Rule}}", WebUtility.HtmlEncode(rule.Name))
                .Replace("{{Scenario}}", WebUtility.HtmlEncode(AlertScenarios.DisplayName(rule.Scenario)))
                .Replace("{{Subject}}", WebUtility.HtmlEncode(state.SubjectName))
                .Replace("{{Message}}", WebUtility.HtmlEncode(state.Message ?? ""))
                .Replace("{{Since}}", WebUtility.HtmlEncode(since))
                .Replace("{{Repeat}}", WebUtility.HtmlEncode(repeat))
                .Replace("{{Time}}", DateTime.UtcNow.ToBerlinString("dd.MM.yyyy HH:mm:ss"));

            return await _email.Send(recipients, $"[SshSecMonitorer] {label}: {state.SubjectName} – {rule.Name}", html, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Alert mail for rule {Rule} / {Subject} could not be sent", rule.Name, state.SubjectName);
            return false;
        }
    }
}
