using FastEndpoints;

namespace API.Common;

/// <summary>
/// Health check endpoint untuk verifikasi koneksi server.
/// Digunakan oleh aplikasi mobile sebelum menampilkan form login.
/// </summary>
public class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("v1/health");
        AllowAnonymous();
        Description(d => d.WithTags("Health"));
        Summary(s =>
        {
            s.Summary = "Server health check";
            s.Description = "Memeriksa apakah server dapat dijangkau. Gunakan sebelum login untuk memastikan koneksi tersedia.";
        });
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendAsync(new HealthResponse
        {
            Status = "ok",
            Message = "Server siap menerima koneksi.",
            TimestampUtc = DateTime.UtcNow
        }, cancellation: ct);
    }
}

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}
