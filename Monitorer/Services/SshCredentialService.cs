using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Repository;

namespace Monitorer.Services;

/// <summary>SSH users with key or password that the Monitorer uses to reach the agent hosts.</summary>
public class SshCredentialService
{
    private readonly SshCredentialRepository _repo;
    private readonly AgentRepository _agents;
    private readonly SecretService _secrets;
    private readonly SshFetchService _ssh;
    private readonly MonitoringEvaluationService _evaluation;
    private readonly IStringLocalizer _l;

    public SshCredentialService(SshCredentialRepository repo, AgentRepository agents, SecretService secrets,
        SshFetchService ssh, MonitoringEvaluationService evaluation, IStringLocalizer l)
    {
        _l = l;
        _evaluation = evaluation;
        _repo = repo;
        _agents = agents;
        _secrets = secrets;
        _ssh = ssh;
    }

    public async Task<ServiceResult<List<SshCredentialDto>>> GetAll()
    {
        var list = new List<SshCredentialDto>();
        foreach (var row in await _repo.Select())
            list.Add(new SshCredentialDto(row) { UsedByAgents = await _agents.SelectCountBySshCredentialId(row.Id) });
        return ServiceResult<List<SshCredentialDto>>.Ok(list);
    }

    public async Task<ServiceResult<SshCredentialDto>> Get(int id)
    {
        var row = await _repo.SelectById(id);
        return row is null
            ? ServiceResult<SshCredentialDto>.NotFound(_l["credentials.notFound", id].Value)
            : ServiceResult<SshCredentialDto>.Ok(new SshCredentialDto(row));
    }

    public async Task<ServiceResult<SshCredentialDto>> Create(SshCredentialDto dto)
    {
        var row = new SshCredentialDb();
        var error = Apply(dto, row, isNew: true);
        if (error is not null)
            return ServiceResult<SshCredentialDto>.InvalidValue(error);
        if (await _repo.SelectByName(row.Name) is not null)
            return ServiceResult<SshCredentialDto>.Conflict(_l["credentials.nameExists"].Value);

        row.Id = await _repo.Insert(row);
        row.CreatedAt = row.UpdatedAt = DateTime.UtcNow;
        return ServiceResult<SshCredentialDto>.Created(new SshCredentialDto(row));
    }

    public async Task<ServiceResult<SshCredentialDto>> Update(int id, SshCredentialDto dto)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<SshCredentialDto>.NotFound(_l["credentials.notFound", id].Value);
        var error = Apply(dto, row, isNew: false);
        if (error is not null)
            return ServiceResult<SshCredentialDto>.InvalidValue(error);
        var sameName = await _repo.SelectByName(row.Name);
        if (sameName is not null && sameName.Id != id)
            return ServiceResult<SshCredentialDto>.Conflict(_l["credentials.nameExists"].Value);

        await _repo.Update(row);
        row.UpdatedAt = DateTime.UtcNow;
        return ServiceResult<SshCredentialDto>.Changed(new SshCredentialDto(row));
    }

    public async Task<ServiceResult<SshCredentialDto>> Delete(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return ServiceResult<SshCredentialDto>.NotFound(_l["credentials.notFound", id].Value);
        if (await _agents.SelectCountBySshCredentialId(id) > 0)
            return ServiceResult<SshCredentialDto>.NotAllowed(_l["credentials.inUse"].Value);

        await _repo.Delete(id);
        return ServiceResult<SshCredentialDto>.Ok(new SshCredentialDto(row));
    }

    /// <summary>
    /// POST …/test: tests the entered (or, with CredentialId and empty secrets, the stored) credential against a host.
    /// 200 with the step results also when the connection fails; 404 unknown credential; 406 invalid input.
    /// </summary>
    public async Task<ServiceResult<SshConnectionTestResultDto>> Test(SshConnectionTestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Host) || dto.Host.Trim().Any(char.IsWhiteSpace)
            || Uri.CheckHostName(dto.Host.Trim()) == UriHostNameType.Unknown)
            return ServiceResult<SshConnectionTestResultDto>.InvalidValue(_l["sshTest.invalidHost"].Value);
        if (dto.Port is < 1 or > 65535)
            return ServiceResult<SshConnectionTestResultDto>.InvalidValue(_l["validation.port"].Value);
        if (string.IsNullOrWhiteSpace(dto.UserName))
            return ServiceResult<SshConnectionTestResultDto>.InvalidValue(_l["sshTest.userRequired"].Value);
        if (!SshAuthTypes.IsValid(dto.AuthType))
            return ServiceResult<SshConnectionTestResultDto>.InvalidValue(_l["credentials.invalidAuthType"].Value);

        var stored = dto.CredentialId is null ? null : await GetSecrets(dto.CredentialId.Value);
        if (dto.CredentialId is not null && stored is null)
            return ServiceResult<SshConnectionTestResultDto>.NotFound(_l["credentials.notFound", dto.CredentialId].Value);

        var isKey = dto.AuthType == SshAuthTypes.Key;
        var secrets = new SshSecretsDto
        {
            UserName = dto.UserName.Trim(),
            AuthType = dto.AuthType,
            // entered secrets win; empty = the stored ones (only if the auth type did not change)
            PrivateKey = !isKey ? null : !string.IsNullOrWhiteSpace(dto.PrivateKey) ? dto.PrivateKey
                : stored?.AuthType == SshAuthTypes.Key ? stored.PrivateKey : null,
            Passphrase = !isKey ? null : !string.IsNullOrWhiteSpace(dto.PrivateKey) ? dto.Passphrase
                : stored?.AuthType == SshAuthTypes.Key ? stored.Passphrase : null,
            Password = isKey ? null : !string.IsNullOrEmpty(dto.Password) ? dto.Password
                : stored?.AuthType == SshAuthTypes.Password ? stored.Password : null
        };
        if (isKey && string.IsNullOrWhiteSpace(secrets.PrivateKey) || !isKey && string.IsNullOrEmpty(secrets.Password))
            return ServiceResult<SshConnectionTestResultDto>.InvalidValue(
                _l[isKey ? "sshTest.keyRequired" : "sshTest.passwordRequired"].Value);

        // use the fingerprint pinned by an agent on the same host / port, if any
        var fingerprint = string.IsNullOrWhiteSpace(dto.HostKeyFingerprint)
            ? (await _agents.Select()).FirstOrDefault(a => string.Equals(a.Host, dto.Host.Trim(), StringComparison.OrdinalIgnoreCase)
                                                           && a.Port == dto.Port && a.HostKeyFingerprint is not null)?.HostKeyFingerprint
            : dto.HostKeyFingerprint.Trim();

        var result = await _ssh.Test(dto.Host.Trim(), dto.Port, fingerprint, secrets, dto.RemotePath, ValidateMonitoringFile, ct);
        return ServiceResult<SshConnectionTestResultDto>.Ok(result);
    }

    /// <summary>
    /// Public key (authorized_keys line) for the setup script - from the entered or the stored private key.
    /// Only the public part leaves the server. 200, 404, 406.
    /// </summary>
    public async Task<ServiceResult<string>> GetPublicKey(SshPublicKeyRequestDto dto)
    {
        string? privateKey = dto.PrivateKey, passphrase = dto.Passphrase;
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            if (dto.CredentialId is null)
                return ServiceResult<string>.InvalidValue(_l["credentials.keyFirst"].Value);
            var stored = await GetSecrets(dto.CredentialId.Value);
            if (stored is null)
                return ServiceResult<string>.NotFound(_l["credentials.notFound", dto.CredentialId].Value);
            if (string.IsNullOrWhiteSpace(stored.PrivateKey))
                return ServiceResult<string>.InvalidValue(_l["credentials.noStoredKey"].Value);
            (privateKey, passphrase) = (stored.PrivateKey, stored.Passphrase);
        }
        var publicKey = _ssh.DerivePublicKey(privateKey, passphrase, "sshsecmonitorer");
        return publicKey is null
            ? ServiceResult<string>.InvalidValue(_l["credentials.noPublicKey"].Value)
            : ServiceResult<string>.Ok(publicKey);
    }

    private string? ValidateMonitoringFile(string content)
    {
        try
        {
            var file = _evaluation.Parse(content);
            return file.GeneratedAt is null
                ? _l["sshTest.fileNoTimestamp"].Value
                : (DateTime.UtcNow - file.GeneratedAt.Value).TotalMinutes > 10
                    ? _l["sshTest.fileStale", file.GeneratedAt.Value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)].Value
                    : null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            return _l["sshTest.fileInvalid", ex.Message].Value;
        }
    }

    /// <summary>Internal: decrypted secrets for a connection.</summary>
    public async Task<SshSecretsDto?> GetSecrets(int id)
    {
        var row = await _repo.SelectById(id);
        if (row is null)
            return null;
        return new SshSecretsDto
        {
            UserName = row.UserName,
            AuthType = row.AuthType,
            PrivateKey = _secrets.Unprotect(row.PrivateKeyEnc),
            Passphrase = _secrets.Unprotect(row.PassphraseEnc),
            Password = _secrets.Unprotect(row.PasswordEnc)
        };
    }

    /// <summary>Validates the DTO and copies it into the row. Empty secrets on update keep the stored ones.</summary>
    private string? Apply(SshCredentialDto dto, SshCredentialDb row, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return _l["validation.nameLength", 100].Value;
        if (string.IsNullOrWhiteSpace(dto.UserName) || dto.UserName.Trim().Length > 100 || dto.UserName.Any(char.IsWhiteSpace))
            return _l["credentials.invalidUserName"].Value;
        if (!SshAuthTypes.IsValid(dto.AuthType))
            return _l["credentials.invalidAuthType"].Value;

        var keyChanged = !string.IsNullOrWhiteSpace(dto.PrivateKey);
        if (dto.AuthType == SshAuthTypes.Key)
        {
            if (!keyChanged && (isNew || row.PrivateKeyEnc is null))
                return _l["credentials.keyRequired"].Value;
            if (keyChanged)
            {
                var keyError = _ssh.ValidatePrivateKey(dto.PrivateKey!, dto.Passphrase);
                if (keyError is not null)
                    return keyError;
            }
        }
        if (dto.AuthType == SshAuthTypes.Password && string.IsNullOrEmpty(dto.Password) && (isNew || row.PasswordEnc is null))
            return _l["credentials.passwordRequired"].Value;

        row.Name = dto.Name.Trim();
        row.UserName = dto.UserName.Trim();
        row.AuthType = dto.AuthType;
        if (dto.AuthType == SshAuthTypes.Key)
        {
            if (keyChanged)
            {
                row.PrivateKeyEnc = _secrets.Protect(dto.PrivateKey);
                row.PassphraseEnc = _secrets.Protect(dto.Passphrase);
            }
            row.PasswordEnc = null;
        }
        else
        {
            if (!string.IsNullOrEmpty(dto.Password))
                row.PasswordEnc = _secrets.Protect(dto.Password);
            row.PrivateKeyEnc = null;
            row.PassphraseEnc = null;
        }
        return null;
    }
}
