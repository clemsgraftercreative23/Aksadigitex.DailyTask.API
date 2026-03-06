using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminListUsersEndpoint : RoleAuthorizedEndpointWithoutRequest<AdminUserListResponse>
{
    private readonly AppDbContext _db;

    public AdminListUsersEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/admin/users");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "List users (superduperadmin only)";
            s.Description = "Returns all users for superduperadmin management.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var users = await _db.Users
            .AsNoTracking()
            .Include(x => x.RoleRef)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        await SendAsync(new AdminUserListResponse
        {
            Items = users.Select(x => x.ToUserItemResponse()).ToList()
        }, cancellation: ct);
    }
}
