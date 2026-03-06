using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Users;

public static class UserClaims
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub");

        return int.TryParse(value, out var id) ? id : null;
    }
}
