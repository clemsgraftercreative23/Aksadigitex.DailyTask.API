using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminUpdateUserEndpoint : RoleAuthorizedEndpoint<UpdateUserRequest, UserDetailResponse>
{
    private readonly AppDbContext _db;

    public AdminUpdateUserEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("v1/admin/users/{id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "Update user (superduperadmin only)";
            s.Description = "Updates user identity, role, and profile fields.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        var userId = Route<int>("id");

        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email))
        {
            AddError("FullName and Email are required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (!Enum.IsDefined(typeof(UserRole), req.RoleId))
        {
            AddError("RoleId is invalid.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var duplicate = await _db.Users.AnyAsync(x => x.Email == email && x.Id != userId, ct);
        if (duplicate)
        {
            AddError("Email is already used by another user.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        user.FullName = req.FullName.Trim();
        user.Email = email;
        user.Role = (UserRole)req.RoleId;
        user.Position = req.Position?.Trim();
        user.CompanyId = req.CompanyId;
        user.DepartmentId = req.DepartmentId;
        user.MfaEnabled = req.MfaEnabled;
        if (req.NotifThresholdMin.HasValue) user.NotifThresholdMin = req.NotifThresholdMin.Value;
        if (req.NotifThresholdMax.HasValue) user.NotifThresholdMax = req.NotifThresholdMax.Value;
        if (req.UrgencyEmail is not null) user.UrgencyEmail = req.UrgencyEmail;
        if (req.EnableUrgensi.HasValue) user.EnableUrgensi = req.EnableUrgensi.Value;

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(req.Password);
        }

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
