using API.Users;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Admin;

public class RemindRequest
{
    public string Date { get; set; } = string.Empty;
}

public class RemindAllRequest
{
    public string Date { get; set; } = string.Empty;
    public List<int> UserIds { get; set; } = new();
}

public class RemindSingleEndpoint : Endpoint<RemindRequest>
{
    private readonly AppDbContext _db;
    public RemindSingleEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/activity-log/remind/{userId:int}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Activity Log"));
    }

    public override async Task HandleAsync(RemindRequest req, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var targetUserId = Route<int>("userId");
        var currentUser = await _db.Users.AsNoTracking()
            .Include(u => u.RoleRef)
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value, ct);
        if (currentUser is null) { await SendUnauthorizedAsync(ct); return; }

        var roleName = currentUser.RoleRef?.RoleName?.Trim().ToLowerInvariant() ?? "";
        var hasAccess = roleName is "admin_divisi" or "super_admin" or "super_duper_admin"
            || currentUser.Role is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin;
        if (!hasAccess)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Akses ditolak" }, ct);
            return;
        }

        var target = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUserId && u.IsActive, ct);
        if (target is null)
        {
            await SendAsync(new { success = false, message = "User tidak ditemukan atau akses ditolak." }, 400, ct);
            return;
        }

        if ((roleName == "admin_divisi" || currentUser.Role == UserRole.AdminDivisi) && (target.DepartmentId != currentUser.DepartmentId || target.CompanyId != currentUser.CompanyId))
        {
            await SendAsync(new { success = false, message = "Akses ditolak (Divisi berbeda)." }, 403, ct);
            return;
        }
        if ((roleName == "super_admin" || currentUser.Role == UserRole.SuperAdmin) && target.CompanyId != currentUser.CompanyId)
        {
            await SendAsync(new { success = false, message = "Akses ditolak (Perusahaan berbeda)." }, 403, ct);
            return;
        }

        var date = string.IsNullOrWhiteSpace(req.Date) ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") : req.Date;
        var message = $"ALERT: kamu belum ada input laporan kerja untuk tanggal {date}. Mohon segera diisi.";

        _db.Notifications.Add(new Domain.Notification
        {
            RecipientUserId = targetUserId,
            SenderType = "admin",
            Message = message,
            Type = "belum_lapor",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        await SendAsync(new { success = true, message = "Pengingat berhasil dikirim." }, cancellation: ct);
    }
}

public class RemindAllEndpoint : Endpoint<RemindAllRequest>
{
    private readonly AppDbContext _db;
    public RemindAllEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/activity-log/remind-all");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Admin - Activity Log"));
    }

    public override async Task HandleAsync(RemindAllRequest req, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var currentUser = await _db.Users.AsNoTracking()
            .Include(u => u.RoleRef)
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value, ct);
        if (currentUser is null) { await SendUnauthorizedAsync(ct); return; }

        var roleName = currentUser.RoleRef?.RoleName?.Trim().ToLowerInvariant() ?? "";
        var hasAccess = roleName is "admin_divisi" or "super_admin" or "super_duper_admin"
            || currentUser.Role is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin;
        if (!hasAccess)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Akses ditolak" }, ct);
            return;
        }

        if (req.UserIds == null || req.UserIds.Count == 0)
        {
            await SendAsync(new { success = false, message = "Tidak ada personil untuk diingatkan." }, 400, ct);
            return;
        }

        IQueryable<Domain.User> verifyQuery = _db.Users.Where(u => req.UserIds.Contains(u.Id) && u.IsActive);
        if (roleName == "admin_divisi" || currentUser.Role == UserRole.AdminDivisi)
            verifyQuery = verifyQuery.Where(u => u.DepartmentId == currentUser.DepartmentId && u.CompanyId == currentUser.CompanyId);
        else if (roleName == "super_admin" || currentUser.Role == UserRole.SuperAdmin)
            verifyQuery = verifyQuery.Where(u => u.CompanyId == currentUser.CompanyId);

        var validIds = await verifyQuery.Select(u => u.Id).ToListAsync(ct);
        if (validIds.Count == 0)
        {
            await SendAsync(new { success = false, message = "Tidak ada personil valid yang bisa diingatkan." }, 400, ct);
            return;
        }

        var date = string.IsNullOrWhiteSpace(req.Date) ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") : req.Date;
        var message = $"ALERT: kamu belum ada input laporan kerja untuk tanggal {date}. Mohon segera diisi.";

        foreach (var targetId in validIds)
        {
            _db.Notifications.Add(new Domain.Notification
            {
                RecipientUserId = targetId,
                SenderType = "admin",
                Message = message,
                Type = "belum_lapor",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(ct);

        await SendAsync(new { success = true, message = $"{validIds.Count} personil telah diingatkan." }, cancellation: ct);
    }
}
