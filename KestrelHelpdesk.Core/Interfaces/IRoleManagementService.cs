using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleManagementUser>> GetAssignedUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleManagementUser>> GetUnassignedUsersAsync(
        CancellationToken cancellationToken = default);

    Task AssignRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default);
}
