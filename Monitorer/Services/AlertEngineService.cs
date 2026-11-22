using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>
/// Evaluates all alert rules: new conditions become active alerts, mails go out after the delay and
/// again every repeat interval while the condition lasts; a recovery mail when it is gone.
/// </summary>
public class AlertEngineService
{
    private readonly AlertRuleRepository _rules;
    private readonly AlertStateRepository _states;
    private readonly AlertConditionService _conditions;
    private readonly AlertService _mail;
    private readonly ILogger<AlertEngineService> _logger;

    public AlertEngineService(AlertRuleRepository rules, AlertStateRepository states, AlertConditionService conditions,
        AlertService mail, ILogger<AlertEngineService> logger)
    {
        _rules = rules;
        _states = states;
        _conditions = conditions;
        _mail = mail;
        _logger = logger;
    }

    public async Task Run(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var context = await _conditions.LoadContext();
        var rules = (await _rules.SelectEnabled()).ToList();
        var states = (await _states.Select()).ToList();

        // alerts of disabled / deleted rules are simply dropped (no recovery mail)
        var enabledIds = rules.Select(r => r.Id).ToHashSet();
        foreach (var orphan in states.Where(s => !enabledIds.Contains(s.RuleId)))
            await _states.Delete(orphan.Id);

        foreach (var rule in rules)
        {
            var conditions = _conditions.GetConditions(rule, context, now)
                .GroupBy(c => c.SubjectKey).ToDictionary(g => g.Key, g => g.First());
            var suppressed = _conditions.SuppressedAgents(rule, context, now);
            var active = states.Where(s => s.RuleId == rule.Id).ToDictionary(s => s.SubjectKey);

            foreach (var condition in conditions.Values)
            {
                if (!active.TryGetValue(condition.SubjectKey, out var state))
                {
                    state = new AlertStateDb
                    {
                        RuleId = rule.Id,
                        SubjectKey = condition.SubjectKey,
                        SubjectName = condition.SubjectName,
                        Message = condition.Message,
                        FirstDetectedAt = now
                    };
                    state.Id = await _states.Insert(state);
                    _logger.LogInformation("Alert {Rule}: {Subject} detected", rule.Name, condition.SubjectName);
                }
                state.SubjectName = condition.SubjectName;
                state.Message = condition.Message;
                await SendIfDue(rule, state, now, ct);
            }

            foreach (var resolved in active.Values.Where(s => !conditions.ContainsKey(s.SubjectKey)))
            {
                // on hold (VM down / data stale): neither resolved nor repeated - we simply do not know
                if (AlertConditionService.AgentIdFromKey(resolved.SubjectKey) is { } agentId && suppressed.Contains(agentId))
                    continue;
                if (rule.NotifyRecovery && resolved.SendCount > 0)
                    await _mail.Send(rule, resolved, AlertService.MailKind.Recovery, ct);
                await _states.Delete(resolved.Id);
                _logger.LogInformation("Alert {Rule}: {Subject} resolved", rule.Name, resolved.SubjectName);
            }
        }
    }

    /// <summary>POST api/alert-rules/{id}/test: sends a test mail to the rule's recipients.</summary>
    public async Task<ServiceResult<bool>> SendTest(int ruleId, CancellationToken ct)
    {
        var rule = await _rules.SelectById(ruleId);
        if (rule is null)
            return ServiceResult<bool>.NotFound($"Alarm-Regel {ruleId} nicht gefunden.");
        if (!_mail.MailEnabled)
            return ServiceResult<bool>.NotAllowed("Der Mailversand ist nicht aktiviert (Mail:Enabled / Mail:Host).");
        if (_mail.RecipientsFor(rule).Count == 0)
            return ServiceResult<bool>.InvalidValue("Keine Empfänger: weder in der Regel noch in Mail:Recipients.");

        var state = new AlertStateDb
        {
            RuleId = rule.Id,
            SubjectName = "Testnachricht",
            Message = "Dies ist eine Testnachricht. Wenn sie ankommt, funktioniert der Mailversand für diese Regel.",
            FirstDetectedAt = DateTime.UtcNow
        };
        var sent = await _mail.Send(rule, state, AlertService.MailKind.Test, ct);
        return sent
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Conflict("Die Testmail konnte nicht gesendet werden – Details im Log.");
    }

    private async Task SendIfDue(AlertRuleDb rule, AlertStateDb state, DateTime now, CancellationToken ct)
    {
        var due = state.LastSentAt is null
            ? (now - state.FirstDetectedAt).TotalMinutes >= rule.DelayMinutes
            : rule.RepeatMinutes > 0 && (now - state.LastSentAt.Value).TotalMinutes >= rule.RepeatMinutes;

        if (due)
        {
            var kind = state.SendCount == 0 ? AlertService.MailKind.Alarm : AlertService.MailKind.Reminder;
            if (await _mail.Send(rule, state, kind, ct))
            {
                state.LastSentAt = now;
                state.SendCount++;
            }
        }
        await _states.Update(state);
    }
}
