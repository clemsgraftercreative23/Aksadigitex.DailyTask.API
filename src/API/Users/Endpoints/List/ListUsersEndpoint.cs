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
                Email = x.Email,
                IsActive = x.IsActive,
                MfaEnabled = x.MfaEnabled
            })
            .ToListAsync(ct);

        await SendAsync(new ListUsersResponse
        {
            Items = items
        }, cancellation: ct);
    }
}
