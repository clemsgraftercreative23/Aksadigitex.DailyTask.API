#nullable enable
using API.Auth;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using API.Reports;

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

        var accountType = User.GetAccountType();
        if (accountType == AuthAccountType.DirectorUser)
        {
            var directorUser = await _db.DirectorUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId.Value, ct);

            if (directorUser is null)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            var roleName = await _db.Roles
                .AsNoTracking()
                .Where(r => r.Id == directorUser.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync(ct);

            string? directorCompanyName = null;
            if (directorUser.CompanyId.HasValue)
            {
                directorCompanyName = await _db.Companies
                    .AsNoTracking()
                    .Where(c => c.Id == directorUser.CompanyId.Value)
                    .Select(c => c.CompanyName)
                    .FirstOrDefaultAsync(ct);
            }

            await SendAsync(new ProfileResponse
            {
                Item = directorUser.ToUserItemResponse(roleName),
                CompanyName = directorCompanyName,
                DepartmentName = null,
            }, cancellation: ct);
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

        string? companyName = null;
        string? departmentName = null;

        if (user.CompanyId.HasValue)
        {
            companyName = await _db.Companies
                .AsNoTracking()
                .Where(c => c.Id == user.CompanyId.Value)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync(ct);
        }

        if (user.DepartmentId.HasValue)
        {
            departmentName = await _db.Departments
                .AsNoTracking()
                .Where(d => d.Id == user.DepartmentId.Value)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync(ct);
        }

        await SendAsync(new ProfileResponse
        {
            Item = user.ToUserItemResponse(),
            CompanyName = companyName,
            DepartmentName = departmentName,
        }, cancellation: ct);
    }
}
