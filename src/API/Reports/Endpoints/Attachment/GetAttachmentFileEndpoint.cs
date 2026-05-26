using API.Auth;
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
        if (!await User.ValidateClientScopeAsync(HttpContext, ct, OAuthScopes.ReportsRead)) return;

        var reportId = Route<int>("reportId");
        var attachmentId = Route<int>("attachmentId");

        var report = await _store.GetByIdAsync(reportId);
        string? attachmentPath = null;
        string? fileType = null;

        if (report is not null)
        {
            var att = report.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            if (att is not null)
            {
                attachmentPath = att.AttachmentPath;
                fileType = att.FileType;
            }
        }

        if (attachmentPath is null)
        {
            // Fallback: check director_reports
            var dirReport = await _store.GetDirectorReportByIdAsync(reportId);
            if (dirReport is not null)
            {
                var att = dirReport.Attachments.FirstOrDefault(a => a.Id == attachmentId);
                if (att is not null)
                {
                    attachmentPath = att.AttachmentPath;
                    fileType = null; // director_report_attachments has no file_type column
                }
            }
        }

        if (attachmentPath is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Build physical path: attachmentPath can be:
        // - "/uploads/reports/40/file.jpg"
        // - "uploads/reports/40/file.jpg"
        // - "reports/40/file.jpg" (from Backoffice)
        // - "file.jpg" (filename only)
        var rawPath = (attachmentPath ?? "").Trim().TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(rawPath))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var physicalPath = Path.Combine(_environment.ContentRootPath, rawPath);
        if (!System.IO.File.Exists(physicalPath))
        {
            // Fallback 1: path without "uploads" prefix
            if (!rawPath.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
                physicalPath = Path.Combine(_environment.ContentRootPath, "uploads", rawPath);
        }
        if (!System.IO.File.Exists(physicalPath))
        {
            // Fallback 2: path without "reports" - try uploads/reports/{reportId}/{path}
            physicalPath = Path.Combine(_environment.ContentRootPath, "uploads", "reports", reportId.ToString(), Path.GetFileName(rawPath));
        }

        if (!System.IO.File.Exists(physicalPath))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(attachmentPath!, out var contentType))
            contentType = fileType ?? "application/octet-stream";

        HttpContext.Response.ContentType = contentType;
        await HttpContext.Response.SendFileAsync(physicalPath, ct);
    }
}
