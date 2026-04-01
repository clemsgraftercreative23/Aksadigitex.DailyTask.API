namespace API.Services;

public class DailyReportReminderOptions
{
    public const string SectionName = "DailyReportReminder";

    public string[] ReminderHours { get; set; } = Array.Empty<string>();
    
    public string BossEscalationTime { get; set; } = string.Empty;

    public string SuperDuperEscalationTime { get; set; } = string.Empty;
    
    public int TimezoneOffsetHours { get; set; }
}
