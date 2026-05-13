using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using API.Auth;

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

    public static AuthAccountType GetAccountType(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("account_type");

        return Enum.TryParse<AuthAccountType>(value, ignoreCase: true, out var accountType)
            ? accountType
            : AuthAccountType.User;
    }
}
