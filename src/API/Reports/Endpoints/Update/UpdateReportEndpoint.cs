using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Reports.Endpoints;

public class UpdateReportRequest
{
    public int Id { get; set; }
    public string? TaskDescription { get; set; }
    public string? Issue { get; set; }
    public string? Solution { get; set; }
    public string? Result { get; set; }
    public DateOnly? ReportDate { get; set; }
    public TimeOnly? ReportTime { get; set; }
}

public class UpdateReportEndpoint : Endpoint<UpdateReportRequest, UpdateReportStatusResponse>
{
    private readonly AppDbContext _db;
    public UpdateReportEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("v1/reports/{Id}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Reports"));
    }

    public override async Task HandleAsync(UpdateReportRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var report = await _db.DailyReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == req.Id, ct);

        if (report is null) { await SendNotFoundAsync(ct); return; }

        if (report.UserId != userId.Value)
        {
            HttpContext.Response.StatusCode = 403;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Hanya pemilik yang bisa mengedit" }, ct);
            return;
        }

        if (report.Status == "approved")
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Laporan yang sudah disetujui tidak bisa diedit" }, ct);
            return;
        }

        if (req.TaskDescription != null) report.TaskDescription = req.TaskDescription;
        if (req.Issue != null) report.Issue = req.Issue;
        if (req.Solution != null) report.Solution = req.Solution;
        if (req.Result != null) report.Result = req.Result;
        if (req.ReportDate.HasValue) report.ReportDate = req.ReportDate.Value;
        if (req.ReportTime.HasValue) report.ReportTime = req.ReportTime.Value;

        await _db.SaveChangesAsync(ct);

        await SendAsync(new UpdateReportStatusResponse { Item = report.ToResponse() }, cancellation: ct);
    }
}
