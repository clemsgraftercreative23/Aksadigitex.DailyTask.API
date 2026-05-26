#nullable enable
using Domain;

namespace API.Auth;

public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public int AccessTokenMinutes { get; set; } = 15;
    public List<OAuthClientOptions> Clients { get; set; } = new();
}

public sealed class OAuthClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SecretSha256 { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.SuperDuperAdmin;
    public List<string> Scopes { get; set; } = new();
}

public static class OAuthScopes
{
    public const string EmployeesRead = "employees:read";
    public const string UsersRead = "users:read";
    public const string ReportsRead = "reports:read";
    public const string DashboardRead = "dashboard:read";
    public const string ActivityLogRead = "activity-log:read";
    public const string MasterDataRead = "master-data:read";
}
