using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class ListUsersEndpoint : EndpointWithoutRequest<ListUsersResponse>
{
    private readonly AppDbContext _db;

    public ListUsersEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/users");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users"));
        Summary(s =>
        {
            s.Summary = "Get users list (paginated)";
            s.Description = "Returns paginated users with page metadata.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = await _db.Users
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new UserItemResponse
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                RoleId = x.RoleId,
                RoleName = x.RoleRef != null ? x.RoleRef.RoleName : x.RoleId.ToString(),
                Position = x.Position,
                CompanyId = x.CompanyId,
                DepartmentId = x.DepartmentId,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                MfaEnabled = x.MfaEnabled,
                HighValueThreshold = x.HighValueThreshold
            })
            .ToListAsync(ct);

        await SendAsync(new ListUsersResponse
        {
            Items = items
        }, cancellation: ct);
    }
}
