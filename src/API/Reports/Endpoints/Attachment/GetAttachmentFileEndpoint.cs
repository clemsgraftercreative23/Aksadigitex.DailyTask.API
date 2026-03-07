using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Reports;

public class GetAttachmentFileEndpoint : EndpointWithoutRequest
{
    private readonly ReportStore _store;
    private readonly IWebHostEnvironment _environment;

    public GetAttachmentFileEndpoint(ReportStore store, IWebHostEnvironment environment)
    {
        _store = store;
        _environment = environment;
    }

    public override void Configure()
    {
        Get("v1/reports/{reportId}/attachments/{attachmentId}/file");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
        Summary(s =>
        {
            s.Summary = "Get attachment file";
            s.Description = "Streams the attachment file. Requires authentication.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reportId = Route<int>("reportId");
        var attachmentId = Route<int>("attachmentId");

        var report = await _store.GetByIdAsync(reportId);
        if (report is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var attachment = report.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Build physical path: attachmentPath is e.g. "/uploads/reports/40/file.jpg" or "uploads/reports/40/file.jpg"
        var path = attachment.AttachmentPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(_environment.ContentRootPath, path);

        // Fallback: if path doesn't start with "uploads", try under uploads folder
        if (!System.IO.File.Exists(physicalPath) && !path.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
        {
            physicalPath = Path.Combine(_environment.ContentRootPath, "uploads", path);
        }

        if (!System.IO.File.Exists(physicalPath))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachment.AttachmentPath, out var contentType))
            contentType = attachment.FileType ?? "application/octet-stream";

        HttpContext.Response.ContentType = contentType;
        await HttpContext.Response.SendFileAsync(physicalPath, ct);
    }
}
