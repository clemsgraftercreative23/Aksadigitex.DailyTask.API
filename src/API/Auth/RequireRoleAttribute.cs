using FastEndpoints;
using Domain;

namespace API.Auth;

/// <summary>
/// OPTIONAL: Custom authorization attribute (tidak digunakan saat ini)
/// Gunakan RoleAuthorizedEndpoint base classes sebagai gantinya
/// </summary>
/*
[AttributeUsage(AttributeTargets.Class)]
public class RequireRoleAttribute : Attribute
{
    public UserRole[] AllowedRoles { get; }

    /// <summary>
    /// Tentukan role yang diizinkan mengakses endpoint
    /// </summary>
    /// <param name="roles">Array of roles yang diizinkan (minimal 1)</param>
    public RequireRoleAttribute(params UserRole[] roles)
    {
        if (roles == null || roles.Length == 0)
        {
            throw new ArgumentException("Minimal satu role harus ditentukan", nameof(roles));
        }
        AllowedRoles = roles;
    }
}
*/
