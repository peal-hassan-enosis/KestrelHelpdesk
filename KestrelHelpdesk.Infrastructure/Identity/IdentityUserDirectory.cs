using KestrelHelpdesk.Core.Authorization;
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
            .Select(ToAssignableUser)
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    public async Task<IReadOnlyList<AssignableUser>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return Array.Empty<AssignableUser>();

        var users = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .ToListAsync();

        return users
            .Select(ToAssignableUser)
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    private static AssignableUser ToAssignableUser(ApplicationUser user) =>
        new(user.Id, user.UserName ?? user.Email ?? user.Id, user.Email ?? user.UserName ?? string.Empty);
}
