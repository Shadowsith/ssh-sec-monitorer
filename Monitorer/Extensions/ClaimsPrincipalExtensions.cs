using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Monitorer.Data;
using Monitorer.Services;

namespace Monitorer.Extensions;

/// <summary>Reads the claims written by TokenService.</summary>
public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : 0;

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(TokenService.DisplayNameClaim) ?? user.Identity?.Name ?? "";

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(UserRoles.Admin);
}
