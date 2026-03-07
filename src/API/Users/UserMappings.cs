using Domain;

namespace API.Users;

public static class UserMappings
{
    public static UserItemResponse ToUserItemResponse(this User user)
    {
        return new UserItemResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = user.RoleId,
            RoleName = user.RoleRef?.RoleName ?? user.Role.ToString(),
            Position = user.Position,
            CompanyId = user.CompanyId,
            DepartmentId = user.DepartmentId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            MfaEnabled = user.MfaEnabled,
            NotifThresholdMin = user.NotifThresholdMin,
            NotifThresholdMax = user.NotifThresholdMax,
            UrgencyEmail = user.UrgencyEmail,
            EnableUrgensi = user.EnableUrgensi
        };
    }
}
