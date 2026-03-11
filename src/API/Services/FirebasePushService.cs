using FirebaseAdmin;
using FirebaseAdmin.Messaging;

namespace API.Services;

public interface IFirebasePushService
{
    /// <summary>
    /// Send push notification to device token. No-op if Firebase not configured or token empty.
    /// </summary>
    Task SendAsync(string? token, string title, string body, string type, int? referenceId = null, CancellationToken ct = default);
}

public class FirebasePushService : IFirebasePushService
{
    private readonly ILogger<FirebasePushService> _logger;
    private readonly FirebaseOptions _options;

    public FirebasePushService(ILogger<FirebasePushService> logger, Microsoft.Extensions.Options.IOptions<FirebaseOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendAsync(string? token, string title, string body, string type, int? referenceId = null, CancellationToken ct = default)
    {
        if (!_options.Enabled || FirebaseApp.DefaultInstance == null || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var data = new Dictionary<string, string>
            {
                ["type"] = type
            };
            if (referenceId.HasValue)
                data["reference_id"] = referenceId.Value.ToString();

            var message = new Message
            {
                Token = token,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "daily_task_channel"
                    }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
            _logger.LogDebug("FCM sent to token (type={Type})", type);
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex, "FCM send failed for type={Type}: {Error}", type, ex.MessagingErrorCode);
            // Invalid/expired token - caller may want to clear fcm_token
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for type={Type}", type);
        }
    }
}
