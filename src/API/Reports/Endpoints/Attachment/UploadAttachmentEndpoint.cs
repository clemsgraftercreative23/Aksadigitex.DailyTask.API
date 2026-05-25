using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
using API.Users;
using Domain;

namespace API.Reports;

public class UploadAttachmentEndpoint : RoleAuthorizedEndpoint<UploadAttachmentRequest, UploadAttachmentResponse>
{
    private readonly ReportStore _store;
    private readonly IWebHostEnvironment _environment;

    public UploadAttachmentEndpoint(ReportStore store, IWebHostEnvironment environment)
    {
        _store = store;
        _environment = environment;
    }

    public override void Configure()
    {
        Post("v1/reports/{id}/attachments");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        AllowFileUploads();
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Upload attachment";
            s.Description = "Uploads an attachment for a report. Allowed roles: User, AdminDivisi, SuperAdmin, SuperDuperAdmin.";
        });
    }

    // Izinkan User, AdminDivisi, SuperAdmin, dan SuperDuperAdmin untuk upload attachment
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.User, UserRole.AdminDivisi, UserRole.SuperAdmin, UserRole.SuperDuperAdmin };

    public override async Task HandleAsync(UploadAttachmentRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        var reportId = Route<int>("id");
        var accountType = User.GetAccountType();

        // Determine which report table the ID belongs to
        bool isDirectorReport = false;
        Domain.DirectorReport? directorReport = null;
        var dailyReport = accountType == AuthAccountType.DirectorUser
            ? null
            : await _store.GetByIdAsync(reportId);

        if (dailyReport is null)
        {
            directorReport = await _store.GetDirectorReportByIdAsync(reportId, ct);
            if (directorReport is not null)
            {
                isDirectorReport = true;
            }
            else if (accountType == AuthAccountType.DirectorUser)
            {
                await SendNotFoundAsync(ct);
                return;
            }
            else
            {
                await SendNotFoundAsync(ct);
                return;
            }
        }

        if (req.File is null || req.File.Length == 0)
        {
            AddError("File is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        try
        {
            // Create uploads directory if not exists
            var subDir = isDirectorReport ? "director-reports" : "reports";
            var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads", subDir, reportId.ToString());
            Directory.CreateDirectory(uploadsDir);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}_{req.File.FileName}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Save file to disk
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await req.File.CopyToAsync(stream, ct);
            }

            var relativePath = $"/uploads/{subDir}/{reportId}/{fileName}";

            if (isDirectorReport)
            {
                var attachment = await _store.AddDirectorAttachmentAsync(reportId, relativePath, ct);
                if (attachment is null)
                {
                    AddError("Failed to save attachment to database.");
                    await SendErrorsAsync(cancellation: ct);
                    return;
                }
                var updatedReport = await _store.GetDirectorReportByIdAsync(reportId, ct);
                await SendAsync(new UploadAttachmentResponse
                {
                    Item = updatedReport!.ToResponse()
                }, cancellation: ct);
            }
            else
            {
                var attachment = await _store.AddAttachmentAsync(reportId, relativePath, req.File.ContentType);
                if (attachment is null)
                {
                    AddError("Failed to save attachment to database.");
                    await SendErrorsAsync(cancellation: ct);
                    return;
                }
                var updatedReport = await _store.GetByIdAsync(reportId);
                await SendAsync(new UploadAttachmentResponse
                {
                    Item = updatedReport!.ToResponse()
                }, cancellation: ct);
            }
        }
        catch (Exception ex)
        {
            AddError($"Failed to upload attachment: {ex.Message}");
            await SendErrorsAsync(cancellation: ct);
        }
    }
}
