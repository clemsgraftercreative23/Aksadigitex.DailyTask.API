using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminDetailUserEndpoint : RoleAuthorizedEndpointWithoutRequest<UserDetailResponse>
{
    private readonly AppDbContext _db;

    public AdminDetailUserEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/admin/users/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "Get user detail (superduperadmin only)";
            s.Description = "Returns detail of a single user for superduperadmin.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    protected override string[] GetAllowedOAuthScopes() => new[] { OAuthScopes.UsersRead };

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var userId = Route<int>("id");

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.RoleRef)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            var directorUser = await _db.DirectorUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId, ct);

            if (directorUser is null)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            var roleName = await _db.Roles
                .AsNoTracking()
                .Where(x => x.Id == directorUser.RoleId)
                .Select(x => x.RoleName)
                .FirstOrDefaultAsync(ct);

            await SendAsync(new UserDetailResponse
            {
                Item = directorUser.ToUserItemResponse(roleName)
            }, cancellation: ct);
            return;
        }

        await SendAsync(new UserDetailResponse
        {
            Item = user.ToUserItemResponse()
        }, cancellation: ct);
    }
}
