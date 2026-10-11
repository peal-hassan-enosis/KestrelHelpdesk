namespace KestrelHelpdesk.Core.Authorization;

public sealed record CurrentUserInfo(
    string UserId,
    string Email,
    IReadOnlySet<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role);
}
