namespace API.Services;

using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class DailyTaskNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyTaskNotificationService> _logger;
    private readonly DailyReportReminderOptions _options;
    private readonly HashSet<string> _processedEvents = new();

    public DailyTaskNotificationService(
        IServiceProvider serviceProvider,
        ILogger<DailyTaskNotificationService> logger,
        IOptions<DailyReportReminderOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyTaskNotificationService is starting.");
        
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1)); // Check every minute

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var nowWIB = nowUtc.AddHours(_options.TimezoneOffsetHours);
                var todayDate = DateOnly.FromDateTime(nowWIB);
                var yesterdayDate = todayDate.AddDays(-1);
                var currentTimeStr = nowWIB.ToString("HH:mm");
                var dateKey = todayDate.ToString("yyyyMMdd");

                _logger.LogInformation("Notification Tick: Current WIB={CurrentWIB}, KeyTime={CurrentTimeStr}, ConfiguredHours=[{ReminderHours}]", 
                    nowWIB.ToString("yyyy-MM-dd HH:mm:ss"), 
                    currentTimeStr, 
                    string.Join(", ", _options.ReminderHours));
                
                await using var scope = _serviceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pushService = scope.ServiceProvider.GetRequiredService<IFirebasePushService>();

                // 1. Hourly Reminder (for User, AdminDivisi, SuperAdmin)
                if (_options.ReminderHours.Contains(currentTimeStr))
                {
                    var reminderKey = $"Reminder-{currentTimeStr}-{dateKey}";
                    if (!_processedEvents.Contains(reminderKey))
                    {
                        _logger.LogInformation("Triggering reminder for {TimeStr}", currentTimeStr);
                        await ProcessHourlyReminder(dbContext, pushService, todayDate, currentTimeStr, stoppingToken);
                        _processedEvents.Add(reminderKey);
                    }
                    else
                    {
                        _logger.LogInformation("Reminder for {TimeStr} already processed.", currentTimeStr);
                    }
                }

                // 2. Boss Escalation at specific hour (e.g. 20:00)
                if (currentTimeStr == _options.BossEscalationTime)
                {
                    var bossEscalationKey = $"BossEscalation-{currentTimeStr}-{dateKey}";
                    if (!_processedEvents.Contains(bossEscalationKey))
                    {
                        await ProcessBossEscalation(dbContext, pushService, todayDate, stoppingToken);
                        _processedEvents.Add(bossEscalationKey);
                    }
                }

                // 3. Super Duper Escalation at specific hour (e.g. 08:00) for missed reports yesterday
                if (currentTimeStr == _options.SuperDuperEscalationTime)
                {
                    var superDuperKey = $"SuperDuperEscalation-{currentTimeStr}-{dateKey}";
                    if (!_processedEvents.Contains(superDuperKey))
                    {
                        await ProcessSuperDuperEscalation(dbContext, pushService, yesterdayDate, stoppingToken);
                        _processedEvents.Add(superDuperKey);
                        
                        // Cleanup old processed events from 2 days ago to prevent growing map
                        var twoDaysAgo = todayDate.AddDays(-2).ToString("yyyyMMdd");
                        _processedEvents.RemoveWhere(k => k.EndsWith(twoDaysAgo));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing DailyTaskNotificationService check.");
            }
        }
    }

    private async Task ProcessHourlyReminder(AppDbContext dbContext, IFirebasePushService pushService, DateOnly reportDate, string timeStr, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing hourly reminder for time: {TimeStr}", timeStr);
        
        var usersWithoutReport = await dbContext.Users
            .Where(u => u.IsActive && 
                        !string.IsNullOrEmpty(u.FcmToken) && 
                        (u.RoleId == (int)UserRole.User || u.RoleId == (int)UserRole.AdminDivisi || u.RoleId == (int)UserRole.SuperAdmin))
            .Where(u => !dbContext.DailyReports.Any(r => r.UserId == u.Id && r.ReportDate == reportDate))
            .ToListAsync(stoppingToken);

        foreach (var user in usersWithoutReport)
        {
            await pushService.SendAsync(
                user.FcmToken, 
                "Pengingat Daily Task", 
                $"Halo {user.FullName}, kamu belum mengisi laporan Daily Task untuk hari ini. Segera isi laporannya ya!", 
                "reminder_daily_task", 
                null, 
                stoppingToken);
        }
        
        _logger.LogInformation("Sent {Count} hourly reminders.", usersWithoutReport.Count);
    }

    private async Task ProcessBossEscalation(AppDbContext dbContext, IFirebasePushService pushService, DateOnly reportDate, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing boss escalation");

        // Users who haven't filled report today
        var lazyUsers = await dbContext.Users
            .Where(u => u.IsActive && u.RoleId == (int)UserRole.User)
            .Where(u => !dbContext.DailyReports.Any(r => r.UserId == u.Id && r.ReportDate == reportDate))
            .ToListAsync(stoppingToken);

        if (!lazyUsers.Any()) return;

        // Group by CompanyId to optimize notifications
        var usersByCompany = lazyUsers.GroupBy(u => u.CompanyId);

        int escalationCount = 0;
        foreach (var group in usersByCompany)
        {
            var companyId = group.Key;
            
            // Find AdminDivisi in the same company
            var bosses = await dbContext.Users
                .Where(u => u.IsActive && u.CompanyId == companyId && !string.IsNullOrEmpty(u.FcmToken) && u.RoleId == (int)UserRole.AdminDivisi)
                .ToListAsync(stoppingToken);

            // If no AdminDivisi found, fallback to SuperAdmin in the same company
            if (!bosses.Any())
            {
                bosses = await dbContext.Users
                    .Where(u => u.IsActive && u.CompanyId == companyId && !string.IsNullOrEmpty(u.FcmToken) && u.RoleId == (int)UserRole.SuperAdmin)
                    .ToListAsync(stoppingToken);
            }

            var lazyUserNames = string.Join(", ", group.Select(u => u.FullName));
            var message = $"Terdapat staff yang belum mengisi laporan hari ini: {lazyUserNames}. Mohon ditindaklanjuti.";

            foreach (var boss in bosses)
            {
                await pushService.SendAsync(
                    boss.FcmToken, 
                    "Eskalasi Laporan Staff", 
                    message, 
                    "escalation_daily_task", 
                    null, 
                    stoppingToken);
                escalationCount++;
            }
        }
        
        _logger.LogInformation("Sent {Count} boss escalations.", escalationCount);
    }

    private async Task ProcessSuperDuperEscalation(AppDbContext dbContext, IFirebasePushService pushService, DateOnly yesterdayDate, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing super duper escalation for date: {Date}", yesterdayDate);

        var lazyManagement = await dbContext.Users
            .Where(u => u.IsActive && (u.RoleId == (int)UserRole.AdminDivisi || u.RoleId == (int)UserRole.SuperAdmin))
            .Where(u => !dbContext.DailyReports.Any(r => r.UserId == u.Id && r.ReportDate == yesterdayDate))
            .ToListAsync(stoppingToken);

        if (!lazyManagement.Any()) return;

        var superDuperAdmins = await dbContext.Users
            .Where(u => u.IsActive && !string.IsNullOrEmpty(u.FcmToken) && u.RoleId == (int)UserRole.SuperDuperAdmin)
            .ToListAsync(stoppingToken);

        if (!superDuperAdmins.Any()) return;

        var details = string.Join("\n", lazyManagement.Select(u => $"- {u.FullName} (Role: {(UserRole)u.RoleId})"));
        var message = $"Berikut adalah daftar Admin/SuperAdmin yang belum mengisi laporan untuk tanggal {yesterdayDate:dd/MM/yyyy}:\n{details}";

        foreach (var sda in superDuperAdmins)
        {
            await pushService.SendAsync(
                sda.FcmToken, 
                "Eskalasi Keterlambatan Laporan Manajemen", 
                message, 
                "super_duper_escalation", 
                null, 
                stoppingToken);
        }
        
        _logger.LogInformation("Sent {Count} super duper escalations.", superDuperAdmins.Count);
    }
}
