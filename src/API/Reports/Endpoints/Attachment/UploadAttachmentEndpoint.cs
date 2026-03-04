using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace API.Reports;

public class UploadAttachmentEndpoint : Endpoint<UploadAttachmentRequest, UploadAttachmentResponse>
{
    private readonly ReportStore _store;

    public UploadAttachmentEndpoint(ReportStore store)
    {
        _store = store;
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
            s.Description = "Uploads an attachment metadata for a report.";
        });
    }

    public override async Task HandleAsync(UploadAttachmentRequest req, CancellationToken ct)
    {
        var report = _store.GetById(Route<int>("id"));
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

        var updated = _store.AddAttachment(
            report.Id,
            req.File.FileName,
            req.File.ContentType,
            req.File.Length);

        await SendAsync(new UploadAttachmentResponse
        {
            Item = updated!.ToResponse()
        }, cancellation: ct);
    }
}
