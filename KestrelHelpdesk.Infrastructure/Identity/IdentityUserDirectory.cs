using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    public async Task<IReadOnlyList<AssignableUser>> GetUsersByIdsAsync(IReadOnlyCollection<string> userIds)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
            return Array.Empty<AssignableUser>();

        var users = await _userManager.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync();

        return users
            .Select(user => new AssignableUser(
                user.Id,
                user.UserName ?? user.Email ?? user.Id,
                user.Email ?? user.UserName ?? user.Id))
            .ToList();
    }

}
