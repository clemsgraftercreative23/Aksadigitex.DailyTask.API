#nullable enable
using API.Users;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Reports;

public class ListReportsEndpoint : EndpointWithoutRequest<ListReportsResponse>
{
    private readonly AppDbContext _db;

    public ListReportsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("v1/reports");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Get reports with pagination, filters, and role-based scoping";
            s.Description =
                "Returns paginated daily reports filtered by role scope, status, date range, and search. " +
                "For Super Duper Admin only: rows are excluded if any of task (tugas), issue (masalah), solution (solusi), or result (hasil) is empty or whitespace.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var accountType = User.GetAccountType();

        // Parse query parameters once at the top
        var page = Query<int?>("page", isRequired: false).GetValueOrDefault(1);
        var pageSize = Query<int?>("pageSize", isRequired: false).GetValueOrDefault(20);
        var status = Query<string?>("status", isRequired: false);
        var dateFrom = Query<string?>("dateFrom", isRequired: false);
        var dateTo = Query<string?>("dateTo", isRequired: false);
        var search = Query<string?>("search", isRequired: false);
        var filterUserId = Query<int?>("userId", isRequired: false);

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        // ---------------------------------------------------------
        // DIRECTOR USER FLOW: Query DirectorReports
        // ---------------------------------------------------------
        if (accountType == API.Auth.AuthAccountType.DirectorUser)
        {
            var directorUser = await _db.DirectorUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
            if (directorUser is null) { await SendUnauthorizedAsync(ct); return; }

            IQueryable<DirectorReport> dirQuery = _db.DirectorReports.AsNoTracking().Include(r => r.Attachments);

            // DirectorUser only sees their own reports for now in this endpoint 
            // (or if they are SDA viewing other directors, we scope it)
            if (directorUser.Role == UserRole.SuperDuperAdmin)
            {
                // SDA sees all directors' reports that are not draft
                dirQuery = dirQuery.Where(r => 
                    r.UserId == userId.Value || 
                    (r.Status != null && r.Status.ToLower() != "draft"));
            }
            else
            {
                dirQuery = dirQuery.Where(r => r.UserId == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
                dirQuery = dirQuery.Where(r => r.Status == status.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var dirDf))
                dirQuery = dirQuery.Where(r => r.ReportDate >= dirDf);

            if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var dirDt))
                dirQuery = dirQuery.Where(r => r.ReportDate <= dirDt);

            if (filterUserId.HasValue)
                dirQuery = dirQuery.Where(r => r.UserId == filterUserId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var dirS = search.ToLower();
                dirQuery = dirQuery.Where(r =>
                    r.TaskDescription.ToLower().Contains(dirS) ||
                    r.Issue.ToLower().Contains(dirS) ||
                    r.Solution.ToLower().Contains(dirS));
            }

            var dirTotalCount = await dirQuery.CountAsync(ct);
            var dirItems = await dirQuery
                .OrderByDescending(x => x.ReportDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Enrich
            var dirReportUserIds = dirItems.Select(r => r.UserId).Distinct().ToList();
            var dUsers = await _db.DirectorUsers.AsNoTracking().Where(u => dirReportUserIds.Contains(u.Id)).ToListAsync(ct);
            var dirCompanies = await _db.Companies.AsNoTracking().ToListAsync(ct);

            var dirResponseItems = dirItems.Select(r =>
            {
                var resp = r.ToResponse();
                var u = dUsers.FirstOrDefault(x => x.Id == r.UserId);
                if (u != null)
                {
                    resp.UserFullName = u.FullName;
                    resp.UserEmail = u.Email;
                    resp.UserPosition = "Director";
                    resp.CompanyId = u.CompanyId;
                    resp.CompanyName = dirCompanies.FirstOrDefault(c => c.Id == u.CompanyId)?.CompanyName;
                    resp.UserRoleName = "director";
                }
                return resp;
            }).ToList();

            await SendAsync(new ListReportsResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = dirTotalCount,
                Items = dirResponseItems
            }, cancellation: ct);
            return;
        }

        // ---------------------------------------------------------
        // REGULAR USER FLOW: Query DailyReports
        // ---------------------------------------------------------
        UserRole role;
        int? currentDepartmentId;
        int? currentCompanyId;

        var currentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (currentUser is not null)
        {
            role = currentUser.Role;
            currentDepartmentId = currentUser.DepartmentId;
            currentCompanyId = currentUser.CompanyId;
        }
        else
        {
            await SendUnauthorizedAsync(ct); 
            return;
        }



        IQueryable<DailyReport> query = _db.DailyReports.AsNoTracking().Include(r => r.Attachments);

        // Role-based scoping per about.md
        // admin_divisi: hanya laporan sendiri + laporan bawahan (user di departemen yang sama)
        if (role == UserRole.User)
        {
            query = query.Where(r => r.UserId == userId.Value);
        }
        else if (role == UserRole.AdminDivisi)
        {
            var subordinateUserIds = await _db.Users.AsNoTracking()
                .Where(u => u.DepartmentId == currentDepartmentId && u.RoleId == (int)UserRole.User)
                .Select(u => u.Id)
                .ToListAsync(ct);
            var allowedUserIds = subordinateUserIds.Concat(new[] { userId!.Value }).Distinct().ToList();
            query = query.Where(r => allowedUserIds.Contains(r.UserId));
        }
        else if (role == UserRole.SuperAdmin)
        {
            // Super Admin hanya melihat laporan admin_divisi + laporan sendiri (tidak termasuk laporan user)
            var companyAdminDivisiUserIds = await _db.Users.AsNoTracking()
                .Where(u => u.CompanyId == currentCompanyId && u.RoleId == (int)UserRole.AdminDivisi)
                .Select(u => u.Id)
                .ToListAsync(ct);
            var allowedUserIds = companyAdminDivisiUserIds.Concat(new[] { userId!.Value }).Distinct().ToList();
            query = query.Where(r => allowedUserIds.Contains(r.UserId));
        }
        // SuperDuperAdmin sees all (scoped later for draft visibility)

        // Visibility rule: laporan bawahan tidak boleh menampilkan status draft.
        // Hanya pemilik laporan yang boleh melihat draft miliknya sendiri.
        if (role is UserRole.AdminDivisi or UserRole.SuperAdmin or UserRole.SuperDuperAdmin)
        {
            var currentUserId = userId!.Value;
            query = query.Where(r =>
                r.UserId == currentUserId ||
                (r.Status == null || r.Status.ToLower() != "draft"));
        }

        // Super Duper Admin: list hanya laporan dengan Tugas, Masalah, Solusi, Hasil terisi (bukan kosong / whitespace).
        if (role == UserRole.SuperDuperAdmin)
        {
            query = query.Where(r =>
                !string.IsNullOrWhiteSpace(r.TaskDescription) &&
                !string.IsNullOrWhiteSpace(r.Issue) &&
                !string.IsNullOrWhiteSpace(r.Solution) &&
                !string.IsNullOrWhiteSpace(r.Result));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status.ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var df))
            query = query.Where(r => r.ReportDate >= df);

        if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var dt))
            query = query.Where(r => r.ReportDate <= dt);

        if (filterUserId.HasValue)
            query = query.Where(r => r.UserId == filterUserId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                r.TaskDescription.ToLower().Contains(s) ||
                r.Issue.ToLower().Contains(s) ||
                r.Solution.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.ReportDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Enrich with user info (check both users and director_users tables)
        var reportUserIds = items.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => reportUserIds.Contains(u.Id))
            .Include(u => u.RoleRef)
            .ToListAsync(ct);

        // Find user_ids that are NOT in the users table — they may be in director_users
        var foundUserIds = users.Select(u => u.Id).ToHashSet();
        var missingUserIds = reportUserIds.Where(id => !foundUserIds.Contains(id)).ToList();
        var directorUsers = missingUserIds.Count > 0
            ? await _db.DirectorUsers.AsNoTracking()
                .Where(u => missingUserIds.Contains(u.Id))
                .ToListAsync(ct)
            : new List<DirectorUser>();

        var departments = await _db.Departments.AsNoTracking().ToListAsync(ct);
        var companies = await _db.Companies.AsNoTracking().ToListAsync(ct);

        var responseItems = items.Select(r =>
        {
            var resp = r.ToResponse();
            var u = users.FirstOrDefault(x => x.Id == r.UserId);
            if (u != null)
            {
                resp.UserFullName = u.FullName;
                resp.UserEmail = u.Email;
                resp.UserPosition = u.Position;
                resp.DepartmentName = departments.FirstOrDefault(d => d.Id == u.DepartmentId)?.DepartmentName;
                resp.CompanyName = companies.FirstOrDefault(c => c.Id == u.CompanyId)?.CompanyName;
                resp.DepartmentId = u.DepartmentId;
                resp.CompanyId = u.CompanyId;
            }
            else
            {
                // Fallback: check director_users table
                var du = directorUsers.FirstOrDefault(x => x.Id == r.UserId);
                if (du != null)
                {
                    resp.UserFullName = du.FullName;
                    resp.UserEmail = du.Email;
                    resp.CompanyName = companies.FirstOrDefault(c => c.Id == du.CompanyId)?.CompanyName;
                    resp.CompanyId = du.CompanyId;
                }
            }
            return resp;
        }).ToList();

        await SendAsync(new ListReportsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = responseItems
        }, cancellation: ct);
    }
}
