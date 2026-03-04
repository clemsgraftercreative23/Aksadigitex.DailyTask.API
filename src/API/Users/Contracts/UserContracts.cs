namespace API.Users;

public class UserItemResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool? MfaEnabled { get; set; }
}

public class ListUsersResponse
{
    public IReadOnlyList<UserItemResponse> Items { get; set; } = Array.Empty<UserItemResponse>();
}
