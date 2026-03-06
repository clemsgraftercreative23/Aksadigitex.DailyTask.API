using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class GetProfileEndpoint : EndpointWithoutRequest<ProfileResponse>
{
    private readonly AppDbContext _db;

    public GetProfileEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/profile");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Profile"));
        Summary(s =>
        {
            s.Summary = "Get current user profile";
            s.Description = "Returns current authenticated user profile based on JWT token.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.RoleRef)
            .FirstOrDefaultAsync(x => x.Id == userId.Value, ct);

        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new ProfileResponse
        {
            Item = user.ToUserItemResponse()
        }, cancellation: ct);
    }
}
