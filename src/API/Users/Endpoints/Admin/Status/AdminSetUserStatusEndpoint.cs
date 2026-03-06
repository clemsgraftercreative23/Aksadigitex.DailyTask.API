using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminSetUserStatusEndpoint : RoleAuthorizedEndpoint<UpdateUserStatusRequest, UserDetailResponse>
{
    private readonly AppDbContext _db;

    public AdminSetUserStatusEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Patch("v1/admin/users/{id}/status");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "Enable/disable user (superduperadmin only)";
            s.Description = "Sets is_active for an existing user.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(UpdateUserStatusRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var userId = Route<int>("id");
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        user.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        var updated = await _db.Users
            .AsNoTracking()
            .Include(x => x.RoleRef)
            .FirstAsync(x => x.Id == user.Id, ct);

        await SendAsync(new UserDetailResponse
        {
            Item = updated.ToUserItemResponse()
        }, cancellation: ct);
    }
}
