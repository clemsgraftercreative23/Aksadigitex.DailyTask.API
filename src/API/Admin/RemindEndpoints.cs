using API.Services;
using API.Users;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

internal sealed record ReminderRecipient(int Id, int? CompanyId, int? DepartmentId, string FcmToken, bool IsDirectorUser);

public class RemindSingleEndpoint : Endpoint<RemindRequest>
{
    private readonly AppDbContext _db;
    private readonly IFirebasePushService _fcm;
    private readonly ILogger<RemindSingleEndpoint> _logger;
    public RemindSingleEndpoint(AppDbContext db, IFirebasePushService fcm, ILogger<RemindSingleEndpoint> logger)
    {
        _db = db;
        _fcm = fcm;
        _logger = logger;
    }

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

        string roleName;
        UserRole roleEnum;
        int? currentDepartmentId;
        int? currentCompanyId;

        if (currentUser is not null)
        {
            roleName = currentUser.RoleRef?.RoleName?.Trim().ToLowerInvariant() ?? "";
            roleEnum = currentUser.Role;
            currentDepartmentId = currentUser.DepartmentId;
            currentCompanyId = currentUser.CompanyId;
        }
        else
        {
            var directorUser = await _db.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId.Value, ct);
            if (directorUser is null) { await SendUnauthorizedAsync(ct); return; }

            roleName = await _db.Roles.AsNoTracking()
                .Where(r => r.Id == directorUser.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync(ct) ?? "";
            roleName = roleName.Trim().ToLowerInvariant();
            roleEnum = directorUser.Role;
            currentDepartmentId = null;
            currentCompanyId = directorUser.CompanyId;
        }

        var hasAccess = roleName is "admin_divisi" or "super_admin" or "super_duper_admin"
            || roleEnum is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin;
        if (!hasAccess)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Akses ditolak" }, ct);
            return;
        }

        var targetUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUserId && u.IsActive, ct);
        var targetDirectorUser = targetUser is null
            ? await _db.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUserId && u.IsActive, ct)
            : null;

        if (targetUser is null && targetDirectorUser is null)
        {
            await SendAsync(new { success = false, message = "User tidak ditemukan atau akses ditolak." }, 400, ct);
            return;
        }

        var targetCompanyId = targetUser?.CompanyId ?? targetDirectorUser?.CompanyId;
        var targetDepartmentId = targetUser?.DepartmentId;
        var targetIsDirectorUser = targetDirectorUser is not null;

        if ((roleName == "admin_divisi" || roleEnum == UserRole.AdminDivisi)
            && (targetIsDirectorUser || targetDepartmentId != currentDepartmentId || targetCompanyId != currentCompanyId))
        {
            await SendAsync(new { success = false, message = "Akses ditolak (Divisi berbeda)." }, 403, ct);
            return;
        }
        if ((roleName == "super_admin" || roleEnum == UserRole.SuperAdmin) && targetCompanyId != currentCompanyId)
        {
            await SendAsync(new { success = false, message = "Akses ditolak (Perusahaan berbeda)." }, 403, ct);
            return;
        }

        var date = string.IsNullOrWhiteSpace(req.Date) ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") : req.Date;
        var message = $"ALERT: kamu belum ada input laporan kerja untuk tanggal {date}. Mohon segera diisi.";

        try
        {
            if (targetIsDirectorUser)
            {
                _db.DirectorNotifications.Add(new Domain.DirectorNotification
                {
                    RecipientUserId = targetUserId,
                    SenderType = "admin",
                    Message = message,
                    Type = "belum_lapor",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                _db.Notifications.Add(new Domain.Notification
                {
                    RecipientUserId = targetUserId,
                    SenderType = "admin",
                    Message = message,
                    Type = "belum_lapor",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            await _db.SaveChangesAsync(ct);

            var fcmToken = targetUser?.FcmToken ?? targetDirectorUser?.FcmToken;
            await _fcm.SendAsync(fcmToken, "Pengingat Laporan", message, "belum_lapor", null, ct);

            await SendAsync(new { success = true, message = "Pengingat berhasil dikirim." }, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemindSingleEndpoint failed. targetUserId={TargetUserId}, date={Date}",
                targetUserId, date);
            await SendAsync(new { success = false, message = "Pengingat gagal dikirim. Coba lagi." }, cancellation: ct);
        }
    }
}

public class RemindAllEndpoint : Endpoint<RemindAllRequest>
{
    private readonly AppDbContext _db;
    private readonly IFirebasePushService _fcm;
    private readonly ILogger<RemindAllEndpoint> _logger;
    public RemindAllEndpoint(AppDbContext db, IFirebasePushService fcm, ILogger<RemindAllEndpoint> logger)
    {
        _db = db;
        _fcm = fcm;
        _logger = logger;
    }

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

        string roleName;
        UserRole roleEnum;
        int? currentDepartmentId;
        int? currentCompanyId;

        if (currentUser is not null)
        {
            roleName = currentUser.RoleRef?.RoleName?.Trim().ToLowerInvariant() ?? "";
            roleEnum = currentUser.Role;
            currentDepartmentId = currentUser.DepartmentId;
            currentCompanyId = currentUser.CompanyId;
        }
        else
        {
            var directorUser = await _db.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId.Value, ct);
            if (directorUser is null) { await SendUnauthorizedAsync(ct); return; }

            roleName = await _db.Roles.AsNoTracking()
                .Where(r => r.Id == directorUser.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync(ct) ?? "";
            roleName = roleName.Trim().ToLowerInvariant();
            roleEnum = directorUser.Role;
            currentDepartmentId = null;
            currentCompanyId = directorUser.CompanyId;
        }

        var hasAccess = roleName is "admin_divisi" or "super_admin" or "super_duper_admin"
            || roleEnum is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin;
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

        var requestedIds = req.UserIds.Distinct().ToList();

        IQueryable<Domain.User> verifyQuery = _db.Users.Where(u => requestedIds.Contains(u.Id) && u.IsActive);
        if (roleName == "admin_divisi" || roleEnum == UserRole.AdminDivisi)
            verifyQuery = verifyQuery.Where(u => u.DepartmentId == currentDepartmentId && u.CompanyId == currentCompanyId);
        else if (roleName == "super_admin" || roleEnum == UserRole.SuperAdmin)
            verifyQuery = verifyQuery.Where(u => u.CompanyId == currentCompanyId);

        var regularRecipients = await verifyQuery
            .Select(u => new ReminderRecipient(u.Id, u.CompanyId, u.DepartmentId, u.FcmToken ?? string.Empty, false))
            .ToListAsync(ct);

        var regularIds = regularRecipients.Select(u => u.Id).ToHashSet();
        IQueryable<DirectorUser> directorQuery = _db.DirectorUsers
            .Where(u => requestedIds.Contains(u.Id) && !regularIds.Contains(u.Id) && u.IsActive);

        if (roleName == "admin_divisi" || roleEnum == UserRole.AdminDivisi)
            directorQuery = directorQuery.Where(u => false);
        else if (roleName == "super_admin" || roleEnum == UserRole.SuperAdmin)
            directorQuery = directorQuery.Where(u => u.CompanyId == currentCompanyId);

        var directorRecipients = await directorQuery
            .Select(u => new ReminderRecipient(u.Id, u.CompanyId, null, u.FcmToken, true))
            .ToListAsync(ct);

        var validRecipients = regularRecipients.Concat(directorRecipients).ToList();
        if (validRecipients.Count == 0)
        {
            await SendAsync(new { success = false, message = "Tidak ada personil valid yang bisa diingatkan." }, 400, ct);
            return;
        }

        var date = string.IsNullOrWhiteSpace(req.Date) ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") : req.Date;
        var message = $"ALERT: kamu belum ada input laporan kerja untuk tanggal {date}. Mohon segera diisi.";

        try
        {
            foreach (var u in validRecipients)
            {
                if (u.IsDirectorUser)
                {
                    _db.DirectorNotifications.Add(new Domain.DirectorNotification
                    {
                        RecipientUserId = u.Id,
                        SenderType = "admin",
                        Message = message,
                        Type = "belum_lapor",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    _db.Notifications.Add(new Domain.Notification
                    {
                        RecipientUserId = u.Id,
                        SenderType = "admin",
                        Message = message,
                        Type = "belum_lapor",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
                await _fcm.SendAsync(u.FcmToken, "Pengingat Laporan", message, "belum_lapor", null, ct);
            }
            await _db.SaveChangesAsync(ct);

            await SendAsync(new { success = true, message = $"{validRecipients.Count} personil telah diingatkan." }, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemindAllEndpoint failed. currentUserId={CurrentUserId}, date={Date}, targetCount={TargetCount}",
                currentUserId, date, validRecipients.Count);
            await SendAsync(new { success = false, message = "Pengingat gagal dikirim. Coba lagi." }, cancellation: ct);
        }
    }
}
