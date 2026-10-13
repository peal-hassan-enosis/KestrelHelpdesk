using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Infrastructure.Identity;

public sealed class IdentityRoleManagementService : IRoleManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserContext _currentUser;

    public IdentityRoleManagementService(
        UserManager<ApplicationUser> userManager,
        ICurrentUserContext currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RoleManagementUser>> GetAssignedUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync();
        return await GetUsersAsync(assignedOnly: true, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleManagementUser>> GetUnassignedUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync();
        return await GetUsersAsync(assignedOnly: false, cancellationToken);
    }

    public async Task AssignRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync();

        if (!IsManagedRole(role))
            throw new ArgumentException("Only Employee or Agent roles can be assigned.", nameof(role));

        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("The selected user was not found.");

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Contains(AppRoles.Manager, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("The manager account cannot be reassigned.");

        foreach (var currentRole in currentRoles.Where(IsManagedRole).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _userManager.RemoveFromRoleAsync(user, currentRole);
            EnsureSuccess(result, $"Could not remove role '{currentRole}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var assignResult = await _userManager.AddToRoleAsync(user, role);
        EnsureSuccess(assignResult, $"Could not assign role '{role}'.");
    }

    private async Task<IReadOnlyList<RoleManagementUser>> GetUsersAsync(
        bool assignedOnly,
        CancellationToken cancellationToken)
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var result = new List<RoleManagementUser>();

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(IsManagedRole);

            if (assignedOnly)
            {
                if (role is not null)
                    result.Add(new RoleManagementUser(user.Id, user.Email ?? user.UserName ?? user.Id, role));
            }
            else if (role is null && !roles.Contains(AppRoles.Manager, StringComparer.Ordinal))
            {
                result.Add(new RoleManagementUser(user.Id, user.Email ?? user.UserName ?? user.Id, "Unassigned"));
            }
        }

        return result;
    }

    private async Task EnsureManagerAsync()
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        if (currentUser is null || !currentUser.IsInRole(AppRoles.Manager))
            throw new UnauthorizedAccessException("Only managers can manage user roles.");
    }

    private static bool IsManagedRole(string role) =>
        role is AppRoles.Employee or AppRoles.Agent;

    private static void EnsureSuccess(IdentityResult result, string message)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(", ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message} {errors}");
    }
}
