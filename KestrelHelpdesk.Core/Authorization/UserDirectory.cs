namespace KestrelHelpdesk.Core.Authorization;

public sealed record AssignableUser(string Id, string DisplayName, string Email);

public interface IUserDirectory
{
    Task<IReadOnlyList<AssignableUser>> GetAgentsAsync();
    Task<IReadOnlyList<AssignableUser>> GetUsersByIdsAsync(IReadOnlyCollection<string> userIds);
}
