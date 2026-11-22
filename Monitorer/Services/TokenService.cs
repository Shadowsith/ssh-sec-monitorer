using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;

namespace Monitorer.Services;

/// <summary>Creates and describes the JWTs used for the browser session and API clients.</summary>
public class TokenService
{
    public const string NameClaim = "name";
    public const string RoleClaim = "role";
    public const string DisplayNameClaim = "display_name";

    private readonly JwtSettings _jwt;

    public TokenService(IOptions<JwtSettings> jwt) => _jwt = jwt.Value;

    public AuthDto CreateToken(UserDb user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.LifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(NameClaim, user.UserName),
                new Claim(DisplayNameClaim, user.DisplayName),
                new Claim(RoleClaim, user.Role)
            }),
            SigningCredentials = new SigningCredentials(CreateSigningKey(_jwt), SecurityAlgorithms.HmacSha256)
        };

        return new AuthDto
        {
            Token = new JsonWebTokenHandler().CreateToken(descriptor),
            ExpiresAt = expires,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
    }

    public static SymmetricSecurityKey CreateSigningKey(JwtSettings jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException("Jwt:Key must be set and at least 32 bytes long.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
    }

    public static TokenValidationParameters CreateValidationParameters(JwtSettings jwt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(jwt),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = NameClaim,
        RoleClaimType = RoleClaim
    };
}
