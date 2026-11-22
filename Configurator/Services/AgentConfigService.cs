using Configurator.Localization;
using System.Text.Json;
using Configurator.Data.Db;
using Configurator.Data.Model;
using Configurator.Data.Result;
using Configurator.Repository;

namespace Configurator.Services;

/// <summary>Saves / loads agent configurations in the local SQLite database.</summary>
public class AgentConfigService
{
    private readonly AgentConfigRepository _repo;
    private readonly ConfigYamlService _yaml;

    public AgentConfigService(AgentConfigRepository repo, ConfigYamlService yaml)
    {
        _repo = repo;
        _yaml = yaml;
    }

    public async Task<List<AgentConfigDb>> GetAll() => (await _repo.Select()).ToList();

    public AgentSettings ReadSettings(AgentConfigDb row)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentSettings>(row.SettingsJson) ?? new AgentSettings();
        }
        catch (JsonException)
        {
            return new AgentSettings();
        }
    }

    /// <summary>Creates (id = null) or updates a configuration.</summary>
    public async Task<ServiceResult<AgentConfigDb>> Save(int? id, string name, string description, AgentSettings settings)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<AgentConfigDb>.Fail(Texts.Get("validation.nameRequired"));
        var errors = _yaml.Validate(settings);
        if (errors.Count > 0)
            return ServiceResult<AgentConfigDb>.Fail(string.Join(" ", errors));

        var sameName = await _repo.SelectByName(name.Trim());
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<AgentConfigDb>.Fail(Texts.Get("validation.nameExists"));

        var now = DateTime.UtcNow.ToString("O");
        var row = id is null ? new AgentConfigDb { CreatedAt = now } : await _repo.SelectById(id.Value);
        if (row is null)
            return ServiceResult<AgentConfigDb>.Fail(Texts.Get("validation.gone"));

        row.Name = name.Trim();
        row.Description = description.Trim();
        row.SettingsJson = JsonSerializer.Serialize(settings);
        row.UpdatedAt = now;
        if (id is null)
            row.Id = await _repo.Insert(row);
        else
            await _repo.Update(row);
        return ServiceResult<AgentConfigDb>.Ok(row);
    }

    public async Task<ServiceResult<AgentConfigDb>> Duplicate(AgentConfigDb source)
    {
        var name = Texts.Get("duplicate.name", source.Name);
        for (var i = 2; await _repo.SelectByName(name) is not null; i++)
            name = Texts.Get("duplicate.nameNumbered", source.Name, i);
        return await Save(null, name, source.Description, ReadSettings(source));
    }

    public async Task<bool> Delete(int id) => await _repo.Delete(id);
}
