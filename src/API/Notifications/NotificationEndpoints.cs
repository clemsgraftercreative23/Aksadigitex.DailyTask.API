using API.Reports;
using Domain;
using FastEndpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace API.Notifications;

public class NotificationItemResponse
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListNotificationsResponse
{
    public int UnreadCount { get; set; }
    public List<NotificationItemResponse> Items { get; set; } = new();
}

public class ListNotificationsEndpoint : EndpointWithoutRequest<ListNotificationsResponse>
{
    private readonly AppDbContext _db;
    public ListNotificationsEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("v1/notifications");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationItemResponse
            {
                Id = n.Id,
                Message = n.Message,
                Type = n.Type,
                ReferenceId = n.ReferenceId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync(ct);

        var unreadCount = items.Count(i => !i.IsRead);

        await SendAsync(new ListNotificationsResponse
        {
            UnreadCount = unreadCount,
            Items = items,
        }, cancellation: ct);
    }
}

public class MarkNotificationReadRequest
{
    public int Id { get; set; }
}

public class MarkNotificationReadEndpoint : Endpoint<MarkNotificationReadRequest>
{
    private readonly AppDbContext _db;
    public MarkNotificationReadEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/notifications/{Id}/read");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Notifications"));
    }

    public override async Task HandleAsync(MarkNotificationReadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        var notif = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == req.Id && n.RecipientUserId == userId.Value, ct);
        if (notif is null) { await SendNotFoundAsync(ct); return; }

        notif.IsRead = true;
        await _db.SaveChangesAsync(ct);
        await SendOkAsync(new { success = true }, ct);
    }
}

public class MarkAllReadEndpoint : EndpointWithoutRequest
{
    private readonly AppDbContext _db;
    public MarkAllReadEndpoint(AppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("v1/notifications/read-all");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(d => d.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }

        await _db.Notifications
            .Where(n => n.RecipientUserId == userId.Value && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        await SendOkAsync(new { success = true }, ct);
    }
}
