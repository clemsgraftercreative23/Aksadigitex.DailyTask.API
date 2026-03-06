// NOTE: This file is kept for potential future use
// Current implementation uses RoleAuthorizedEndpoint base classes instead
// which provide the same functionality with better integration to FastEndpoints

using FastEndpoints;
using System.Security.Claims;
using Domain;

namespace API.Auth;

/// <summary>
/// OPTIONAL: Post-processor untuk role authorization (tidak digunakan saat ini)
/// Gunakan RoleAuthorizedEndpoint base classes sebagai gantinya
/// </summary>
/*
public class RoleAuthorizationPostProcessor : IPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        // Implementation kept for reference
        await Task.CompletedTask;
    }
}
*/
