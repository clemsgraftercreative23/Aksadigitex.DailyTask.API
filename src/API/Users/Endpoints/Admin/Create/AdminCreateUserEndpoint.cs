using API.Auth;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Users;

public class AdminCreateUserEndpoint : RoleAuthorizedEndpoint<CreateUserRequest, UserDetailResponse>
{
    private readonly AppDbContext _db;

    public AdminCreateUserEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("v1/admin/users");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Users Admin"));
        Summary(s =>
        {
            s.Summary = "Create user (superduperadmin only)";
            s.Description = "Creates a new user account managed by superduperadmin.";
        });
    }

    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (!await ValidateRoleAsync(ct))
            return;

        if (string.IsNullOrWhiteSpace(req.FullName) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
        {
            AddError("FullName, Email, and Password are required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (!Enum.IsDefined(typeof(UserRole), req.RoleId))
        {
            AddError("RoleId is invalid.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists)
        {
            AddError("Email is already used by another user.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(req.Password),
            Role = (UserRole)req.RoleId,
            Position = req.Position?.Trim(),
            CompanyId = req.CompanyId,
            DepartmentId = req.DepartmentId,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            MfaEnabled = req.MfaEnabled,
            HighValueThreshold = req.HighValueThreshold
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Users
            .AsNoTracking()
            .Include(x => x.RoleRef)
            .FirstAsync(x => x.Id == user.Id, ct);

        await SendAsync(new UserDetailResponse
        {
            Item = created.ToUserItemResponse()
        }, StatusCodes.Status201Created, ct);
    }
}
