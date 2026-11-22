using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Extensions;

namespace Monitorer.Services;

/// <summary>
/// Builds and sends the alert mails (alarm, reminder, recovery, test). Mails are written in the current culture -
/// the default language when sent by the background service. {{t:key}} in the template is a translation.
/// </summary>
public partial class AlertService
{
    public enum MailKind { Alarm, Reminder, Recovery, Test }

    private static readonly string TemplatePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "MailTemplates", "alert.html");

    private readonly EmailService _email;
    private readonly JsonStringLocalizer _l;
    private readonly ILogger<AlertService> _logger;

    public AlertService(EmailService email, JsonStringLocalizer l, ILogger<AlertService> logger)
    {
        _email = email;
        _l = l;
        _logger = logger;
    }

    [GeneratedRegex(@"\{\{t:([A-Za-z0-9_.]+)\}\}")]
    private static partial Regex TranslationPlaceholder();

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
                MailKind.Alarm => (_l["mail.alarm"].Value, "#dc3545"),
                MailKind.Reminder => (_l["mail.reminder", state.SendCount + 1].Value, "#fd7e14"),
                MailKind.Recovery => (_l["mail.recovery"].Value, "#198754"),
                _ => (_l["mail.test"].Value, "#0d6efd")
            };
            var since = state.FirstDetectedAt.ToBerlinString(_l);
            var repeat = kind == MailKind.Recovery ? "–"
                : rule.RepeatMinutes > 0 ? _l["mail.repeatEvery", rule.RepeatMinutes].Value : _l["mail.repeatOnce"].Value;

            var template = await File.ReadAllTextAsync(TemplatePath, ct);
            var html = TranslationPlaceholder().Replace(template, m => WebUtility.HtmlEncode(_l[m.Groups[1].Value].Value))
                .Replace("{{Lang}}", _l.CurrentLanguage)
                .Replace("{{Label}}", WebUtility.HtmlEncode(label))
                .Replace("{{Color}}", color)
                .Replace("{{Rule}}", WebUtility.HtmlEncode(_l["mail.rule", rule.Name].Value))
                .Replace("{{Scenario}}", WebUtility.HtmlEncode(_l[AlertScenarios.NameKey(rule.Scenario)].Value))
                .Replace("{{Subject}}", WebUtility.HtmlEncode(state.SubjectName))
                .Replace("{{Message}}", WebUtility.HtmlEncode(state.Message ?? ""))
                .Replace("{{Since}}", WebUtility.HtmlEncode(since))
                .Replace("{{Repeat}}", WebUtility.HtmlEncode(repeat))
                .Replace("{{Time}}", DateTime.UtcNow.ToBerlinString(_l, "format.dateTimeSeconds"));

            return await _email.Send(recipients, $"[SshSecMonitorer] {label}: {state.SubjectName} – {rule.Name}", html, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Alert mail for rule {Rule} / {Subject} could not be sent", rule.Name, state.SubjectName);
            return false;
        }
    }
}
