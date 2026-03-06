using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.Auth;
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
            s.Description = "Uploads an attachment for a report. Only User role can upload.";
        });
    }

    // Hanya user dengan role User yang bisa upload attachment
    protected override UserRole[] GetAllowedRoles() =>
        new[] { UserRole.User };

    public override async Task HandleAsync(UploadAttachmentRequest req, CancellationToken ct)
    {
        // Validasi role terlebih dahulu
        if (!await ValidateRoleAsync(ct))
            return;

        var reportId = Route<int>("id");
        var report = await _store.GetByIdAsync(reportId);
        
        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
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
            var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads", "reports", reportId.ToString());
            Directory.CreateDirectory(uploadsDir);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}_{req.File.FileName}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Save file to disk
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await req.File.CopyToAsync(stream, ct);
            }

            // Save attachment to database
            var relativePath = $"/uploads/reports/{reportId}/{fileName}";
            var attachment = await _store.AddAttachmentAsync(
                reportId,
                relativePath,
                req.File.ContentType
            );

            if (attachment is null)
            {
                AddError("Failed to save attachment to database.");
                await SendErrorsAsync(cancellation: ct);
                return;
            }

            // Return updated report with all attachments
            var updatedReport = await _store.GetByIdAsync(reportId);
            await SendAsync(new UploadAttachmentResponse
            {
                Item = updatedReport!.ToResponse()
            }, cancellation: ct);
        }
        catch (Exception ex)
        {
            AddError($"Failed to upload attachment: {ex.Message}");
            await SendErrorsAsync(cancellation: ct);
        }
    }
}
