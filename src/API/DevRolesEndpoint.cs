using FastEndpoints;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Domain;

namespace API.Dev;

public class GetRolesEndpoint : EndpointWithoutRequest<List<RoleDto>>
{
    private readonly AppDbContext _db;

    public GetRolesEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("v1/dev/roles");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.OrderBy(r => r.Id).Select(r => new RoleDto { Id = r.Id, Name = r.RoleName }).ToListAsync(ct);
        await SendAsync(roles, cancellation: ct);
    }
}

public class RoleDto { public int Id { get; set; } public string Name { get; set; } = ""; }
