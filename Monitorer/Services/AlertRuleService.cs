using Microsoft.Extensions.Localization;
using MimeKit;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>CRUD and validation of alert rules; list of active alerts.</summary>
public class AlertRuleService
{
    private readonly AlertRuleRepository _repo;
    private readonly AlertStateRepository _states;
    private readonly AgentRepository _agents;
    private readonly LiveCheckRepository _liveChecks;
    private readonly IStringLocalizer _l;

    public AlertRuleService(AlertRuleRepository repo, AlertStateRepository states, AgentRepository agents,
        LiveCheckRepository liveChecks, IStringLocalizer l)
    {
        _l = l;
        _repo = repo;
        _states = states;
        _agents = agents;
        _liveChecks = liveChecks;
    }

    public async Task<ServiceResult<List<AlertRuleDto>>> GetAll()
    {
        var agents = (await _agents.Select()).ToDictionary(a => a.Id, a => a.Name);
        var checks = (await _liveChecks.Select()).ToDictionary(c => c.Id, c => c.Name);
        var active = (await _states.Select()).GroupBy(s => s.RuleId)
            .ToDictionary(g => g.Key, g => g.Count());
        var rules = (await _repo.Select())
            .Select(r => new AlertRuleDto(r)
            {
                ScenarioName = _l[AlertScenarios.NameKey(r.Scenario)].Value,
                ScopeText = ScopeText(r, agents, checks),
                ActiveCount = active.GetValueOrDefault(r.Id)
            })
            .ToList();
        return ServiceResult<List<AlertRuleDto>>.Ok(rules);
    }

    public async Task<ServiceResult<AlertRuleDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        return row is null
            ? ServiceResult<AlertRuleDto>.NotFound(_l["alerts.ruleNotFound", id].Value)
            : ServiceResult<AlertRuleDto>.Ok(ToDto(row));
    }

    public async Task<ServiceResult<List<ActiveAlertDto>>> GetActive()
    {
        var rules = (await _repo.Select()).ToDictionary(r => r.Id);
        var list = (await _states.Select())
            .Where(s => rules.ContainsKey(s.RuleId))
            .Select(s => new ActiveAlertDto
            {
                State = s,
                RuleName = rules[s.RuleId].Name,
                Scenario = rules[s.RuleId].Scenario,
                RepeatMinutes = rules[s.RuleId].RepeatMinutes,
                DelayMinutes = rules[s.RuleId].DelayMinutes
            })
            .ToList();
        return ServiceResult<List<ActiveAlertDto>>.Ok(list);
    }

    public async Task<ServiceResult<AlertRuleDto>> Create(AlertRuleDto dto)
    {
        var row = new AlertRuleDb();
        var error = await Apply(dto, row);
        if (error is not null)
            return ServiceResult<AlertRuleDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<AlertRuleDto>.Conflict(_l["alerts.nameExists"].Value);

        row.Id = await _repo.Insert(row);
        row.CreatedAt = DateTime.UtcNow;
        return ServiceResult<AlertRuleDto>.Created(ToDto(row));
    }

    public async Task<ServiceResult<AlertRuleDto>> Update(int id, AlertRuleDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AlertRuleDto>.NotFound(_l["alerts.ruleNotFound", id].Value);
        var scopeBefore = (row.Scenario, row.AgentId, row.LiveCheckId, row.ContainerPattern);
        var error = await Apply(dto, row);
        if (error is not null)
            return ServiceResult<AlertRuleDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<AlertRuleDto>.Conflict(_l["alerts.nameExists"].Value);

        await _repo.Update(row);
        // a changed scenario / scope starts fresh - old alerts would no longer match
        if (scopeBefore != (row.Scenario, row.AgentId, row.LiveCheckId, row.ContainerPattern))
            await _states.DeleteByRuleId(id);
        return ServiceResult<AlertRuleDto>.Changed(ToDto(row));
    }

    /// <summary>PATCH …/enabled: switch a rule on / off.</summary>
    public async Task<ServiceResult<AlertRuleDto>> UpdateEnabled(int id, bool enabled)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AlertRuleDto>.NotFound(_l["alerts.ruleNotFound", id].Value);
        row.Enabled = enabled;
        await _repo.Update(row);
        return ServiceResult<AlertRuleDto>.Changed(ToDto(row));
    }

    public async Task<ServiceResult<AlertRuleDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<AlertRuleDto>.NotFound(_l["alerts.ruleNotFound", id].Value);
        await _repo.Delete(id);
        return ServiceResult<AlertRuleDto>.Ok(ToDto(row));
    }

    private async Task<string?> Apply(AlertRuleDto dto, AlertRuleDb row)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return _l["validation.nameLength", 100].Value;
        if (!AlertScenarios.IsValid(dto.Scenario))
            return _l["alerts.invalidScenario"].Value;
        if (dto.DelayMinutes is < 0 or > 1440)
            return _l["alerts.invalidDelay"].Value;
        if (dto.RepeatMinutes is < 0 or > 10080)
            return _l["alerts.invalidRepeat"].Value;
        if (dto.Scenario == AlertScenarios.AgentStale && dto.ThresholdMinutes is null or < 1 or > 10080)
            return _l["alerts.invalidThreshold"].Value;
        if (dto.Scenario == AlertScenarios.AgentStatus && dto.MinStatus is not (MonitorStatus.Warning or MonitorStatus.Critical))
            return _l["alerts.invalidMinStatus"].Value;
        foreach (var recipient in dto.Recipients.SplitList())
        {
            if (!MailboxAddress.TryParse(recipient, out _) || !recipient.Contains('@'))
                return _l["validation.invalidEmail", recipient].Value;
        }
        var usesAgent = AlertScenarios.UsesAgent(dto.Scenario);
        var usesCheck = AlertScenarios.UsesLiveCheck(dto.Scenario);
        if (usesAgent && dto.AgentId is not null && await _agents.SelectById(dto.AgentId.Value) is null)
            return _l["alerts.agentMissing"].Value;
        if (usesCheck && dto.LiveCheckId is not null && await _liveChecks.SelectById(dto.LiveCheckId.Value) is null)
            return _l["alerts.liveCheckMissing"].Value;

        row.Name = dto.Name.Trim();
        row.Scenario = dto.Scenario;
        row.AgentId = usesAgent ? dto.AgentId : null;
        row.LiveCheckId = usesCheck ? dto.LiveCheckId : null;
        row.ContainerPattern = AlertScenarios.UsesContainerPattern(dto.Scenario) && !string.IsNullOrWhiteSpace(dto.ContainerPattern)
            ? dto.ContainerPattern.Trim() : null;
        row.ThresholdMinutes = dto.Scenario == AlertScenarios.AgentStale ? dto.ThresholdMinutes : null;
        row.MinStatus = dto.Scenario == AlertScenarios.AgentStatus ? dto.MinStatus : null;
        row.IgnoreCleanExit = dto.IgnoreCleanExit;
        row.DelayMinutes = dto.DelayMinutes;
        row.RepeatMinutes = dto.RepeatMinutes;
        row.NotifyRecovery = dto.NotifyRecovery;
        row.Recipients = string.Join("; ", dto.Recipients.SplitList());
        row.Enabled = dto.Enabled;
        return null;
    }

    private AlertRuleDto ToDto(AlertRuleDb row) => new(row) { ScenarioName = _l[AlertScenarios.NameKey(row.Scenario)].Value };

    private string ScopeText(AlertRuleDb r, Dictionary<int, string> agents, Dictionary<int, string> checks)
    {
        if (AlertScenarios.UsesLiveCheck(r.Scenario))
            return r.LiveCheckId is null ? _l["alerts.allLiveChecks"].Value : checks.GetValueOrDefault(r.LiveCheckId.Value, "?");
        var vm = r.AgentId is null ? _l["alerts.allVms"].Value : agents.GetValueOrDefault(r.AgentId.Value, "?");
        return AlertScenarios.UsesContainerPattern(r.Scenario) && r.ContainerPattern is not null
            ? _l["alerts.scopeContainers", vm, r.ContainerPattern].Value
            : vm;
    }
}
