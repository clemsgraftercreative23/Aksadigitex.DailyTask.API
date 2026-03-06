namespace API.Users;

public class UserItemResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? MfaEnabled { get; set; }
    public decimal? HighValueThreshold { get; set; }
}

public class ListUsersResponse
{
    public IReadOnlyList<UserItemResponse> Items { get; set; } = Array.Empty<UserItemResponse>();
}

public class AdminUserListResponse
{
    public IReadOnlyList<UserItemResponse> Items { get; set; } = Array.Empty<UserItemResponse>();
}

public class UserDetailResponse
{
    public UserItemResponse Item { get; set; } = new();
}

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string? Position { get; set; }
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool? MfaEnabled { get; set; }
    public decimal? HighValueThreshold { get; set; }
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public int RoleId { get; set; }
    public string? Position { get; set; }
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public bool? MfaEnabled { get; set; }
    public decimal? HighValueThreshold { get; set; }
}

public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public class ProfileResponse
{
    public UserItemResponse Item { get; set; } = new();
}
