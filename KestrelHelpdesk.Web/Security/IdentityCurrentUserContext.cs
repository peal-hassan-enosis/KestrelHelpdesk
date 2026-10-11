using System.Security.Claims;
using KestrelHelpdesk.Core.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace KestrelHelpdesk.Web.Security;

public sealed class IdentityCurrentUserContext : ICurrentUserContext
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public IdentityCurrentUserContext(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<CurrentUserInfo?> GetCurrentUserAsync()
    {
        var principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.Identity.Name
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        return new CurrentUserInfo(userId, email, roles);
    }
}
