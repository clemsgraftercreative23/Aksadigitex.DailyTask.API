using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminDeleteUserEndpoint : RoleAuthorizedEndpointWithoutRequest<EmptyResponse>
{
    private readonly AppDbContext _db;

    public AdminDeleteUserEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("v1/admin/users/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "Delete user (superduperadmin only)";
            s.Description = "Permanently deletes a user account.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(CancellationToken ct)
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

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);

        await SendNoContentAsync(ct);
    }
}
