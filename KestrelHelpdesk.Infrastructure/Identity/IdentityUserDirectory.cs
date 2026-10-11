using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace KestrelHelpdesk.Infrastructure.Identity;

public sealed class IdentityUserDirectory : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityUserDirectory(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AssignableUser>> GetAgentsAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(AppRoles.Agent);
        return users
            .Select(u => new AssignableUser(u.Id, u.UserName ?? u.Email ?? u.Id, u.Email ?? u.UserName ?? string.Empty))
            .OrderBy(u => u.DisplayName)
            .ToList();
    }
}
