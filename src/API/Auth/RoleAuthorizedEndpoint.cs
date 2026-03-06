using FastEndpoints;
using System.Security.Claims;
using Domain;

namespace API.Auth;

/// <summary>
/// Base endpoint untuk endpoint dengan request body yang memerlukan role authorization
/// Override GetAllowedRoles() dan panggil ValidateRoleAsync() di HandleAsync
/// </summary>
public abstract class RoleAuthorizedEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Override ini untuk menentukan role mana saja yang boleh mengakses endpoint.
    /// Return null atau array kosong untuk allow semua authenticated users.
    /// </summary>
    protected virtual UserRole[]? GetAllowedRoles() => null;

    /// <summary>
    /// Panggil method ini di awal HandleAsync untuk validasi role.
    /// Return true jika role valid, false jika tidak (response sudah dikirim).
    /// </summary>
    protected async Task<bool> ValidateRoleAsync(CancellationToken ct)
    {
        var allowedRoles = GetAllowedRoles();

        // Jika tidak ada restriction, allow semua authenticated users
        if (allowedRoles == null || allowedRoles.Length == 0)
        {
            return true;
        }

        // Ambil role claim dari JWT token
        var roleClaim = HttpContext.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        // Jika tidak ada role claim, tolak akses
        if (string.IsNullOrEmpty(roleClaim))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        // Parse role claim
        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var userRole))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        // Cek apakah role user termasuk dalam daftar yang diizinkan
        if (!allowedRoles.Contains(userRole))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Send forbidden response dengan pesan role tidak authorized
    /// </summary>
    protected async Task SendForbiddenAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await HttpContext.Response.WriteAsJsonAsync(
            new { message = "Akses ditolak: role Anda tidak diizinkan mengakses endpoint ini" },
            cancellationToken: ct);
    }
}

/// <summary>
/// Base endpoint untuk endpoint tanpa request body yang memerlukan role authorization
/// Override GetAllowedRoles() dan panggil ValidateRoleAsync() di HandleAsync
/// </summary>
public abstract class RoleAuthorizedEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<TResponse>
{
    /// <summary>
    /// Override ini untuk menentukan role mana saja yang boleh mengakses endpoint
    /// </summary>
    protected virtual UserRole[]? GetAllowedRoles() => null;

    /// <summary>
    /// Panggil method ini di awal HandleAsync untuk validasi role
    /// </summary>
    protected async Task<bool> ValidateRoleAsync(CancellationToken ct)
    {
        var allowedRoles = GetAllowedRoles();

        if (allowedRoles == null || allowedRoles.Length == 0)
        {
            return true;
        }

        var roleClaim = HttpContext.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(roleClaim))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = "Akses ditolak: role tidak ditemukan dalam token" },
                cancellationToken: ct);
            return false;
        }

        if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var userRole))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = "Akses ditolak: role tidak valid" },
                cancellationToken: ct);
            return false;
        }

        if (!allowedRoles.Contains(userRole))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    message = $"Akses ditolak: role '{userRole}' tidak diizinkan",
                    allowedRoles = allowedRoles.Select(r => r.ToString()).ToArray()
                },
                cancellationToken: ct);
            return false;
        }

        return true;
    }
}
